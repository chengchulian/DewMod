using HarmonyLib;

namespace DewLanMode.patch;

[HarmonyPatch(typeof(UI_InGame_Interact_Rift))]
internal static class LanRiftUiPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("UpdateInteractStatus")]
    private static bool UpdateInteractStatusPrefix()
    {
        return IsWorldReady();
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(UI_InGame_Interact_Rift.GetRiftNameText))]
    private static bool GetRiftNameTextPrefix(ref string __result)
    {
        if (IsWorldReady())
        {
            return true;
        }

        __result = DewLocalization.GetUIValue("Rift_Name");
        return false;
    }

    private static bool IsWorldReady()
    {
        if (!LanModeRuntime.IsLanSession())
        {
            return true;
        }

        ZoneManager manager = NetworkedManagerBase<ZoneManager>.instance;
        return manager != null && manager.currentNodeIndex >= 0 && manager.currentNodeIndex < manager.nodes.Count;
    }
}
