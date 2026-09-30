using HarmonyLib;

namespace DewLanMode.patch;

[HarmonyPatch(typeof(TransitionManager), nameof(TransitionManager.PlayGame))]
internal static class ContinueModePatch
{
    private static DewNetworkStartSettings _approvedSettings;

    // 在人数、存档版本等前置窗口完成后选择模式，避免重新进入标题页的续局补丁链。
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(TransitionManager __instance, DewNetworkStartSettings settings)
    {
        if (settings == null || ReferenceEquals(settings, _approvedSettings) ||
            settings.networkMode != DewNetworkMode.MultiplayerHost || settings.continueData == null)
        {
            return true;
        }

        if (__instance.state == TransitionManager.StateType.Loading)
        {
            return false;
        }

        ContinueModeDialog.Show(__instance, settings);
        return false;
    }

    // 仅放行这次已确认的设置；异常或其他补丁阻止加载时也不遗留放行标记。
    public static void Continue(TransitionManager transitionManager, DewNetworkStartSettings settings)
    {
        if (transitionManager == null || settings == null ||
            transitionManager.state == TransitionManager.StateType.Loading)
        {
            return;
        }

        DewNetworkStartSettings previousSettings = _approvedSettings;
        _approvedSettings = settings;
        try
        {
            transitionManager.PlayGame(settings);
        }
        finally
        {
            _approvedSettings = previousSettings;
        }
    }
}
