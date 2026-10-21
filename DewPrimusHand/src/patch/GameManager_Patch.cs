using DewPrimusHand.util;
using HarmonyLib;
using UnityEngine;

namespace DewPrimusHand.patch;

[HarmonyPatch(typeof(GameManager))]
public class GameManager_Patch
{
    
    [HarmonyPostfix]
    [HarmonyPatch("get_maxAndSpawnedPopulationMultiplier")]
    public static void get_maxAndSpawnedPopulationMultiplier_Postfix(GameManager __instance, ref float __result)
    {
        if (!__instance.isServer)
        {
            return;
        }

        if (__result > 1)
        {
            __result = DifficultyMath.SanitizeMultiplier(
                DewPrimusHand.Instance.Config.MaxAndSpawnedPopulationMultiplier);
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetRegularMonsterHealthMultiplierByScaling))]
    public static void GetRegularMonsterHealthMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        ApplyHealthScaling(ref __result, customZoneIndex, __instance,
            DewPrimusHand.Instance.Config.LittleMonsterHealthMultiplier);
    }


    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetRegularMonsterDamageMultiplierByScaling))]
    public static void GetRegularMonsterDamageMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        ApplyDamageScaling(ref __result, customZoneIndex, __instance,
            DewPrimusHand.Instance.Config.LittleMonsterDamageMultiplier);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetMiniBossMonsterHealthMultiplierByScaling))]
    public static void GetMiniBossMonsterHealthMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        ApplyHealthScaling(ref __result, customZoneIndex, __instance,
            DewPrimusHand.Instance.Config.MiniBossHealthMultiplier);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetMiniBossMonsterDamageMultiplierByScaling))]
    public static void GetMiniBossMonsterDamageMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        ApplyDamageScaling(ref __result, customZoneIndex, __instance,
            DewPrimusHand.Instance.Config.MiniBossDamageMultiplier);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetBossMonsterHealthMultiplierByScaling))]
    public static void GetBossMonsterHealthMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        ApplyHealthScaling(ref __result, customZoneIndex, __instance,
            DewPrimusHand.Instance.Config.BossHealthMultiplier);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetBossMonsterDamageMultiplierByScaling))]
    public static void GetBossMonsterDamageMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        ApplyDamageScaling(ref __result, customZoneIndex, __instance,
            DewPrimusHand.Instance.Config.BossDamageMultiplier);
    }

    private static void ApplyHealthScaling(ref float result, float customZoneIndex,
        GameManager gameManager, float categoryMultiplier)
    {
        float zoneIndex = gameManager.difficulty.GetScaledZoneIndexForHealth(customZoneIndex);
        result = DifficultyMath.ScaleMultiplier(result, categoryMultiplier);
        result = DifficultyMath.ExponentialGrowth(
            zoneIndex,
            result,
            DewPrimusHand.Instance.Config.EnemyExtraHealthGrowthMultiplier);
    }

    private static void ApplyDamageScaling(ref float result, float customZoneIndex,
        GameManager gameManager, float categoryMultiplier)
    {
        float zoneIndex = gameManager.difficulty.GetScaledZoneIndexForDamage(customZoneIndex);
        result = DifficultyMath.ScaleMultiplier(result, categoryMultiplier);
        result = DifficultyMath.ExponentialGrowth(
            zoneIndex,
            result,
            DewPrimusHand.Instance.Config.EnemyExtraDamageGrowthMultiplier);
    }
}
