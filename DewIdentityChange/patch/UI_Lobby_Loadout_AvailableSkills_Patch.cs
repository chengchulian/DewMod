using DewIdentityChange.config;
using HarmonyLib;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(UI_Lobby_Loadout_AvailableSkills))]
public static class UI_Lobby_Loadout_AvailableSkills_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(UI_Lobby_Loadout_AvailableSkills.ClickOnItem))]
    public static bool ClickOnItem_Prefix(UI_Lobby_Loadout_AvailableSkills __instance, int index)
    {
        if (!LoadoutPageMapper.IsIdentityEnabled || DewPlayer.local == null)
        {
            return true;
        }

        var loadout = new HeroLoadoutData(DewPlayer.local.selectedLoadout);
        loadout.SetSkill(__instance.type, index);
        if (!LoadoutPageMapper.TryStoreLocalLoadout(loadout))
        {
            return true;
        }

        DewPlayer.local.CmdSetHeroLoadoutData(loadout);
        __instance.gameObject.SetActive(false);
        return false;
    }
}
