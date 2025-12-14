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
            __result = DewPrimusHand.Instance.Config.MaxAndSpawnedPopulationMultiplier;
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetRegularMonsterHealthMultiplierByScaling))]
    public static void GetRegularMonsterHealthMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        float zi =
            __instance.difficulty.GetScaledZoneIndexForHealth(customZoneIndex);

        __result *= DewPrimusHand.Instance.Config.LittleMonsterHealthMultiplier;

        __result = DifficultyMath.ExponentialGrowth(
            zi,
            __result,
            DewPrimusHand.Instance.Config.EnemyExtraHealthGrowthMultiplier);
    }


    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetRegularMonsterDamageMultiplierByScaling))]
    public static void GetRegularMonsterDamageMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        float zi =
            __instance.difficulty.GetScaledZoneIndexForDamage(customZoneIndex);

        __result *= DewPrimusHand.Instance.Config.LittleMonsterDamageMultiplier;

        __result = DifficultyMath.ExponentialGrowth(
            zi,
            __result,
            DewPrimusHand.Instance.Config.EnemyExtraDamageGrowthMultiplier);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetMiniBossMonsterHealthMultiplierByScaling))]
    public static void GetMiniBossMonsterHealthMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        float zi =
            __instance.difficulty.GetScaledZoneIndexForHealth(customZoneIndex);

        __result *= DewPrimusHand.Instance.Config.MiniBossHealthMultiplier;

        __result = DifficultyMath.ExponentialGrowth(
            zi,
            __result,
            DewPrimusHand.Instance.Config.EnemyExtraHealthGrowthMultiplier);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetMiniBossMonsterDamageMultiplierByScaling))]
    public static void GetMiniBossMonsterDamageMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        float zi =
            __instance.difficulty.GetScaledZoneIndexForDamage(customZoneIndex);

        __result *= DewPrimusHand.Instance.Config.MiniBossDamageMultiplier;

        __result = DifficultyMath.ExponentialGrowth(
            zi,
            __result,
            DewPrimusHand.Instance.Config.EnemyExtraDamageGrowthMultiplier);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetBossMonsterHealthMultiplierByScaling))]
    public static void GetBossMonsterHealthMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        float zi =
            __instance.difficulty.GetScaledZoneIndexForHealth(customZoneIndex);

        __result *= DewPrimusHand.Instance.Config.BossHealthMultiplier;

        __result = DifficultyMath.ExponentialGrowth(
            zi,
            __result,
            DewPrimusHand.Instance.Config.EnemyExtraHealthGrowthMultiplier);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.GetBossMonsterDamageMultiplierByScaling))]
    public static void GetBossMonsterDamageMultiplierByScaling_Postfix(
        float customZoneIndex,
        GameManager __instance,
        ref float __result)
    {
        float zi =
            __instance.difficulty.GetScaledZoneIndexForDamage(customZoneIndex);

        __result *= DewPrimusHand.Instance.Config.BossDamageMultiplier;

        __result = DifficultyMath.ExponentialGrowth(
            zi,
            __result,
            DewPrimusHand.Instance.Config.EnemyExtraDamageGrowthMultiplier);
    }
}