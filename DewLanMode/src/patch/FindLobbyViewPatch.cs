using HarmonyLib;

namespace DewLanMode.patch;

[HarmonyPatch(typeof(UI_Title_FindLobbyView))]
internal static class FindLobbyViewPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("Start")]
    private static void StartPrefix(UI_Title_FindLobbyView __instance)
    {
        FindLobbyController.Install(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnShow")]
    private static void OnShowPrefix(UI_Title_FindLobbyView __instance)
    {
        FindLobbyController.Install(__instance)?.PrepareForShow();
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(UI_Title_FindLobbyView.Refresh))]
    private static bool RefreshPrefix()
    {
        FindLobbyController controller = FindLobbyController.Current;
        if (controller?.IsLanSelected == true)
        {
            controller.RefreshLanLobbies();
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnLobbyListUpdated")]
    private static bool OnLobbyListUpdatedPrefix()
    {
        return FindLobbyController.Current?.IsLanSelected != true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("FixedUpdate")]
    private static bool FixedUpdatePrefix()
    {
        return FindLobbyController.Current?.IsLanSelected != true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("SelectedLobbyChanged")]
    private static bool SelectedLobbyChangedPrefix()
    {
        FindLobbyController controller = FindLobbyController.Current;
        if (controller?.IsLanSelected == true)
        {
            controller.HandleSelectionChanged();
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("Join")]
    private static bool JoinPrefix()
    {
        FindLobbyController controller = FindLobbyController.Current;
        if (controller?.IsLanSelected == true)
        {
            controller.JoinSelectedLanLobby();
            return false;
        }

        return true;
    }
}
