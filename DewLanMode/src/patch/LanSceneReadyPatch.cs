using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace DewLanMode.patch;

[HarmonyPatch]
internal static class LanSceneReadyPatch
{
    private static readonly HashSet<NetworkConnectionToClient> DeferredConnections = new();
    private static bool _isReleasing;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.SetClientReady), typeof(NetworkConnectionToClient))]
    private static bool SetClientReadyPrefix(NetworkConnectionToClient conn)
    {
        if (_isReleasing || !LanModeRuntime.IsLanSession() || !NetworkServer.isLoadingScene)
        {
            return true;
        }

        DeferredConnections.Add(conn);
        Debug.Log($"[DewLanMode] Deferring LAN Ready for {conn.address} until server scene spawning completes.");
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewNetworkManager), nameof(DewNetworkManager.LoadSceneAsync))]
    private static void LoadSceneAsyncPostfix(ref IEnumerator __result)
    {
        if (LanModeRuntime.IsLanSession() && __result != null)
        {
            __result = CompleteSceneLoadThenRelease(__result);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewNetworkManager), nameof(DewNetworkManager.OnServerDisconnect))]
    private static void OnServerDisconnectPostfix(NetworkConnectionToClient conn)
    {
        DeferredConnections.Remove(conn);
    }

    public static void Reset()
    {
        DeferredConnections.Clear();
        _isReleasing = false;
    }

    private static IEnumerator CompleteSceneLoadThenRelease(IEnumerator original)
    {
        while (original.MoveNext())
        {
            yield return original.Current;
        }

        if (DeferredConnections.Count == 0)
        {
            yield break;
        }

        NetworkConnectionToClient[] connections = new NetworkConnectionToClient[DeferredConnections.Count];
        DeferredConnections.CopyTo(connections);
        DeferredConnections.Clear();

        _isReleasing = true;
        try
        {
            foreach (NetworkConnectionToClient connection in connections)
            {
                if (connection != null && NetworkServer.connections.ContainsKey(connection.connectionId))
                {
                    Debug.Log($"[DewLanMode] Releasing deferred LAN Ready for {connection.address}.");
                    NetworkServer.SetClientReady(connection);
                }
            }
        }
        finally
        {
            _isReleasing = false;
        }
    }
}
