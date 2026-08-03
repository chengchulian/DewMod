using HarmonyLib;

namespace DewMorePlayers.patch;

[HarmonyPatch(typeof(TitleManager), nameof(TitleManager.EnterContinueDreaming))]
internal static class TitleManager_EnterContinueDreaming_Patch
{
    private static bool _continueWithoutDialog;

    [HarmonyPrefix]
    private static bool Prefix(TitleManager __instance)
    {
        if (_continueWithoutDialog)
        {
            _continueWithoutDialog = false;
            return true;
        }

        DewPersistence.GameData gameData;
        try
        {
            gameData = DewPersistence.FromJson<DewPersistence.GameData>(DewSave.profileContinue.continueData);
        }
        catch
        {
            return true;
        }

        if (gameData == null || !gameData.isMultiplayer)
        {
            return true;
        }

        if (ManagerBase<TransitionManager>.instance.state != TransitionManager.StateType.Loading)
        {
            ContinuePlayerCountDialog.Show(__instance);
        }

        return false;
    }

    public static void Continue(TitleManager titleManager)
    {
        if (titleManager == null)
        {
            return;
        }

        _continueWithoutDialog = true;
        titleManager.EnterContinueDreaming();
    }
}
