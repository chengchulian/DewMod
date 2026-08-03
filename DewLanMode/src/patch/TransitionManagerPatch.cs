using HarmonyLib;

namespace DewLanMode.patch;

[HarmonyPatch(typeof(TransitionManager), nameof(TransitionManager.PlayGame))]
internal static class TransitionManagerPatch
{
    private static bool Prefix(DewNetworkStartSettings settings)
    {
        if (settings?.networkMode == DewNetworkMode.MultiplayerHost)
        {
            LanLobbyController controller = LanLobbyController.Current;
            if (controller != null && controller.IsLanSelected && !controller.TryPrepareHost(out string error))
            {
                ManagerBase<MessageManager>.instance.ShowMessage(new DewMessageSettings
                {
                    rawContent = error,
                    buttons = DewMessageSettings.ButtonType.Ok
                });
                return false;
            }
        }

        LanModeRuntime.PrepareSettings(settings);
        return true;
    }
}
