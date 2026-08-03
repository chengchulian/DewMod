using HarmonyLib;

namespace DewLanMode.patch;

[HarmonyPatch(typeof(View), "OnHide")]
internal static class LobbyViewLifecyclePatch
{
    [HarmonyPostfix]
    private static void OnHidePostfix(View __instance)
    {
        if (__instance is UI_Title_CreateLobbyView createLobbyView)
        {
            createLobbyView.GetComponent<LanLobbyController>()?.OnViewHidden();
        }
        else if (__instance is UI_Title_FindLobbyView findLobbyView)
        {
            findLobbyView.GetComponent<FindLobbyController>()?.OnViewHidden();
        }
    }
}
