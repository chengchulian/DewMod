using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace DewLanMode;

internal struct LanSceneMessage : NetworkMessage
{
    public string sceneName;
}

internal static class LanAuthenticationSceneSync
{
    private const string LobbyScene = "PlayLobby";

    private static string _serverScene;
    private static bool _completingAuthentication;

    public static bool IsCompletingAuthentication => _completingAuthentication;

    public static void RegisterClientHandler()
    {
        Writer<LanSceneMessage>.write = WriteSceneMessage;
        Reader<LanSceneMessage>.read = ReadSceneMessage;
        _serverScene = null;
        _completingAuthentication = false;
        NetworkClient.RegisterHandler<LanSceneMessage>(HandleSceneMessage, false);
    }

    public static void UnregisterClientHandler()
    {
        NetworkClient.UnregisterHandler<LanSceneMessage>();
        Reset();
    }

    public static void SendServerScene(NetworkConnectionToClient connection)
    {
        Writer<LanSceneMessage>.write = WriteSceneMessage;
        Reader<LanSceneMessage>.read = ReadSceneMessage;
        connection.Send(new LanSceneMessage
        {
            sceneName = SceneManager.GetActiveScene().name
        });
    }

    public static bool TryDelayAuthentication(
        DewNetworkAuthenticator authenticator,
        DewAuthResponseMessage response)
    {
        if (_completingAuthentication || response.isError || string.IsNullOrEmpty(_serverScene))
        {
            return false;
        }

        string sceneName = _serverScene;
        _serverScene = null;
        if (string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.Ordinal))
        {
            NetworkClient.PrepareToSpawnSceneObjects();
            return false;
        }

        authenticator.StartCoroutine(LoadSceneAndCompleteAuthentication(authenticator, response, sceneName));
        return true;
    }

    public static void Reset()
    {
        _serverScene = null;
        _completingAuthentication = false;
    }

    private static void HandleSceneMessage(LanSceneMessage message)
    {
        if (LanModeRuntime.IsLanSession() && !string.IsNullOrEmpty(message.sceneName))
        {
            _serverScene = message.sceneName;
            Debug.Log($"[DewLanMode] Host scene received before authentication: {_serverScene}");
        }
    }

    private static IEnumerator LoadSceneAndCompleteAuthentication(
        DewNetworkAuthenticator authenticator,
        DewAuthResponseMessage response,
        string sceneName)
    {
        NetworkClient.isLoadingScene = true;
        if (ManagerBase<TransitionManager>.instance != null)
        {
            ManagerBase<TransitionManager>.instance.FadeOut(showTips: true);
        }

        if (!string.Equals(sceneName, LobbyScene, StringComparison.Ordinal) &&
            ManagerBase<GameLogicPackage>.instance == null)
        {
            yield return SceneManager.LoadSceneAsync("PlayGame", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
        }

        Debug.Log($"[DewLanMode] Loading host scene before creating the LAN player: {sceneName}");
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        yield return Resources.UnloadUnusedAssets();
        GarbageCollector.CollectIncremental(ulong.MaxValue);
        GC.Collect();

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        NetworkClient.isLoadingScene = false;
        NetworkClient.PrepareToSpawnSceneObjects();

        _completingAuthentication = true;
        try
        {
            authenticator.OnAuthResponseMessage(response);
        }
        finally
        {
            _completingAuthentication = false;
        }
    }

    private static void WriteSceneMessage(NetworkWriter writer, LanSceneMessage message)
    {
        writer.WriteString(message.sceneName);
    }

    private static LanSceneMessage ReadSceneMessage(NetworkReader reader)
    {
        return new LanSceneMessage
        {
            sceneName = reader.ReadString()
        };
    }
}
