using DewIdentityChange.config;
using HarmonyLib;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(UI_Lobby_Constellations_QuickLoadoutSwitch))]
public static class UI_Lobby_Constellations_QuickLoadoutSwitch_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("Start")]
    public static void Start_Postfix(UI_Lobby_Constellations_QuickLoadoutSwitch __instance)
    {
        foreach (var toggle in __instance.GetComponentsInChildren<UI_Toggle>(includeInactive: true))
        {
            toggle.doNotToggleOnClick = true;
            toggle.onClick.RemoveAllListeners();
            toggle.onClick.AddListener(() =>
            {
                if (DewPlayer.local == null)
                {
                    return;
                }

                if (LoadoutPageMapper.IsIdentityEnabled &&
                    LoadoutPageMapper.TrySetLocalSelectedLoadoutIndex(
                        DewPlayer.local.selectedHeroType,
                        toggle.index))
                {
                    return;
                }

                string heroType = DewPlayer.local.selectedHeroType;
                NetworkedManagerBase<GameSettingsManager>.instance
                    .GetLocalPreferredGameSettings()
                    .heroSelectedLoadoutIndex[heroType] = LoadoutPageMapper.NormalizeVisibleIndex(toggle.index);
                DewPlayer.local.CmdSetHeroLoadoutData(
                    DewSave.profileMain.heroLoadouts[heroType][LoadoutPageMapper.NormalizeVisibleIndex(toggle.index)]);
            });
        }
    }
}
