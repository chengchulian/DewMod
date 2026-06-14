using DewIdentityChange.config;
using HarmonyLib;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(DewPlayer))]
public static class DewPlayer_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(DewPlayer.CmdSetHeroType))]
    public static void CmdSetHeroType_Prefix(
        DewPlayer __instance,
        string newType,
        ref LoadoutPageMapper.HeroLoadoutSwapState __state)
    {
        if (!LoadoutPageMapper.IsIdentityEnabled ||
            !__instance.isLocalPlayer ||
            string.IsNullOrEmpty(newType) ||
            DewSave.profileMain == null)
        {
            return;
        }

        LoadoutPageMapper.EnsureProfileLoadoutPages(DewSave.profileMain);

        var settings = NetworkedManagerBase<GameSettingsManager>.instance?.GetLocalPreferredGameSettings();
        if (settings?.heroSelectedLoadoutIndex != null)
        {
            if (!settings.heroSelectedLoadoutIndex.TryGetValue(newType, out int index))
            {
                index = 0;
            }

            settings.heroSelectedLoadoutIndex[newType] = LoadoutPageMapper.NormalizeVisibleIndex(index);
        }

        __state = LoadoutPageMapper.SwapHeroLoadoutsToStorage(DewSave.profileMain, newType);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DewPlayer.CmdSetHeroType))]
    public static void CmdSetHeroType_Postfix(LoadoutPageMapper.HeroLoadoutSwapState __state)
    {
        LoadoutPageMapper.RestoreHeroLoadouts(__state);
    }
}
