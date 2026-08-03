using HarmonyLib;
using Mirror;
using Steamworks;
using UnityEngine;

namespace DewLanMode.patch;

[HarmonyPatch(typeof(DewNetworkAuthenticator))]
internal static class DewNetworkAuthenticatorPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(DewNetworkAuthenticator.OnStartClient))]
    private static void OnStartClientPostfix()
    {
        if (LanModeRuntime.IsLanSession())
        {
            LanAuthenticationSceneSync.RegisterClientHandler();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(DewNetworkAuthenticator.OnStopClient))]
    private static void OnStopClientPrefix()
    {
        if (LanModeRuntime.IsLanSession())
        {
            LanAuthenticationSceneSync.UnregisterClientHandler();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(DewNetworkAuthenticator.OnClientAuthenticate))]
    private static bool OnClientAuthenticatePrefix()
    {
        if (!LanModeRuntime.IsLanSession())
        {
            return true;
        }

        string profileGuid = DewSave.profileMain.guid;
        ulong userId = DewSteam.steamId.m_SteamID;
        if (userId == 0)
        {
            userId = GetStableLanUserId(profileGuid);
        }

        NetworkClient.Send(new DewAuthRequestMessage
        {
            userId = new CSteamID(userId).ToString(),
            profileGuid = profileGuid,
            profileName = DewSave.profileMain.name,
            inviteCode = LanModeRuntime.CreateHandshake()
        });
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(DewNetworkAuthenticator.OnAuthRequestMessage))]
    private static bool OnAuthRequestMessagePrefix(
        DewNetworkAuthenticator __instance,
        NetworkConnectionToClient conn,
        DewAuthRequestMessage msg)
    {
        if (!LanModeRuntime.IsLanSession())
        {
            return true;
        }

        if (LanModeRuntime.IsCompatibleHandshake(msg.inviteCode))
        {
            LanAuthenticationSceneSync.SendServerScene(conn);
            return true;
        }

        Debug.LogWarning($"[DewLanMode] Rejecting {conn.address}: DewLanMode is missing or incompatible.");
        conn.Send(new DewAuthResponseMessage
        {
            isError = true,
            errorType = DewExceptionType.VersionMismatch
        });
        __instance.StartCoroutine(RejectNextFrame(conn));
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(DewNetworkAuthenticator.OnAuthResponseMessage))]
    private static bool OnAuthResponseMessagePrefix(
        DewNetworkAuthenticator __instance,
        DewAuthResponseMessage msg)
    {
        if (!LanModeRuntime.IsLanSession() || LanAuthenticationSceneSync.IsCompletingAuthentication)
        {
            return true;
        }

        return !LanAuthenticationSceneSync.TryDelayAuthentication(__instance, msg);
    }

    private static System.Collections.IEnumerator RejectNextFrame(NetworkConnectionToClient connection)
    {
        yield return null;
        connection.Disconnect();
    }

    private static ulong GetStableLanUserId(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (char character in value ?? string.Empty)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash == 0 ? 1UL : hash;
    }
}
