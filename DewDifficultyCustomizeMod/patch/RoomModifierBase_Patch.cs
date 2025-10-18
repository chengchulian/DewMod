using HarmonyLib;
using DewDifficultyCustomizeMod.config;



namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(RoomModifierBase), nameof(RoomModifierBase.GetScaledChance))]
public static class RoomModifierBase_Patch
{
    [HarmonyPostfix]
    public static void GetScaledChance_Postfix(RoomModifierBase __instance, ref float __result)
    {
        if (__instance.difficultyScaling == ScaleWithDifficultyMode.Beneficial)
        {
            float multiplier = AttrCustomizeResources.Config.beneficialNodeMultiplier;
            __result *= multiplier;
        }
    }
}