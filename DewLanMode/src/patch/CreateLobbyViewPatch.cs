using HarmonyLib;

namespace DewLanMode.patch;

[HarmonyPatch(typeof(UI_Title_CreateLobbyView))]
internal static class CreateLobbyViewPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("Awake")]
    private static void AwakePrefix(UI_Title_CreateLobbyView __instance)
    {
        LanLobbyController.Install(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnShow")]
    private static void OnShowPostfix(UI_Title_CreateLobbyView __instance)
    {
        LanLobbyController.Install(__instance)?.OnViewShown();
    }

}
