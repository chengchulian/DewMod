using HarmonyLib;

namespace DewLanMode.patch;

[HarmonyPatch(typeof(DewNetworkManager))]
internal static class DewNetworkManagerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(DewNetworkManager.Awake))]
    private static void AwakePrefix(DewNetworkManager __instance)
    {
        LanModeRuntime.ConfigureTransport(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("StartSession")]
    private static void StartSessionPrefix()
    {
        if (!LanModeRuntime.IsLanSession())
        {
            return;
        }

        switch (DewNetworkManager.startSettings.networkMode)
        {
            case DewNetworkMode.MultiplayerHostRestart:
                DewNetworkManager.startSettings.networkMode = DewNetworkMode.MultiplayerHost;
                break;
            case DewNetworkMode.MultiplayerJoinRestart:
                DewNetworkManager.startSettings.networkMode = DewNetworkMode.MultiplayerJoinLobby;
                break;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DewNetworkManager.OnStartHost))]
    private static void OnStartHostPostfix()
    {
        if (LanModeRuntime.IsLanSession())
        {
            LanDiscoveryService.Instance?.StartAdvertising();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DewNetworkManager.OnDestroy))]
    private static void OnDestroyPostfix()
    {
        LanDiscoveryService.Instance?.StopAdvertising();
        LanSceneReadyPatch.Reset();
        if (DewNetworkManager.startSettings.networkMode != DewNetworkMode.MultiplayerHostRestart &&
            DewNetworkManager.startSettings.networkMode != DewNetworkMode.MultiplayerJoinRestart)
        {
            LanModeRuntime.Reset();
        }
    }
}
