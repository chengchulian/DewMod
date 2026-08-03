using HarmonyLib;

namespace DewMorePlayers.patch;

[HarmonyPatch(typeof(UI_Title_CreateLobbyView))]
internal static class UI_Title_CreateLobbyView_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("Awake")]
    private static void AwakePrefix(UI_Title_CreateLobbyView __instance)
    {
        MorePlayersLobbyController.Install(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnShow")]
    private static void OnShowPostfix(UI_Title_CreateLobbyView __instance)
    {
        MorePlayersLobbyController.Install(__instance)?.OnViewShown();
    }

    [HarmonyPostfix]
    [HarmonyPatch("UpdateUIState")]
    private static void UpdateUIStatePostfix(UI_Title_CreateLobbyView __instance)
    {
        __instance.GetComponent<MorePlayersLobbyController>()?.RefreshCreateButton();
    }
}
