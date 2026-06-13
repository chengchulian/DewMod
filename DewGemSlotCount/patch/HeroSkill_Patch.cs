using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(HeroSkill), nameof(HeroSkill.GetMaxGemCount))]
public class HeroSkill_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(HeroSkill.GetMaxGemCount))]
    public static void GetMaxGemCount_Postfix(HeroSkill __instance, HeroSkillLocation type, ref int __result)
    {
        int dynamicGemCount = __result - GetVanillaBaseGemCount(type);
        __result = GetConfiguredBaseGemCount(type) + dynamicGemCount;
        __result = Mathf.Clamp(__result, Constant.MinGemCount, Constant.MaxGemCount);
    }

    public static int GetConfiguredBaseGemCount(HeroSkillLocation type)
    {
        int count = type switch
        {
            HeroSkillLocation.Q => DewGemSlotCount.Instance.GameplayConfig.SkillQGemCount,
            HeroSkillLocation.W => DewGemSlotCount.Instance.GameplayConfig.SkillWGemCount,
            HeroSkillLocation.E => DewGemSlotCount.Instance.GameplayConfig.SkillEGemCount,
            HeroSkillLocation.R => DewGemSlotCount.Instance.GameplayConfig.SkillRGemCount,
            HeroSkillLocation.Identity => DewGemSlotCount.Instance.GameplayConfig.SkillIdentityGemCount,
            HeroSkillLocation.Movement => DewGemSlotCount.Instance.GameplayConfig.SkillMovementGemCount,
            _ => 0
        };

        return Mathf.Clamp(count, Constant.MinGemCount, Constant.MaxGemCount);
    }

    public static int GetVanillaBaseGemCount(HeroSkillLocation type)
    {
        return type switch
        {
            HeroSkillLocation.Q => 3,
            HeroSkillLocation.W => 3,
            HeroSkillLocation.E => 3,
            HeroSkillLocation.R => 3,
            HeroSkillLocation.Identity => 0,
            HeroSkillLocation.Movement => 0,
            _ => 0
        };
    }

    public static int GetCorruptedChaosMaxGemCount(HeroSkillLocation type)
    {
        int count = type switch
        {
            HeroSkillLocation.Q => DewGemSlotCount.Instance.GameplayConfig.SkillQCorruptedChaosMaxGemCount,
            HeroSkillLocation.W => DewGemSlotCount.Instance.GameplayConfig.SkillWCorruptedChaosMaxGemCount,
            HeroSkillLocation.E => DewGemSlotCount.Instance.GameplayConfig.SkillECorruptedChaosMaxGemCount,
            HeroSkillLocation.R => DewGemSlotCount.Instance.GameplayConfig.SkillRCorruptedChaosMaxGemCount,
            HeroSkillLocation.Identity => DewGemSlotCount.Instance.GameplayConfig.SkillIdentityCorruptedChaosMaxGemCount,
            HeroSkillLocation.Movement => DewGemSlotCount.Instance.GameplayConfig.SkillMovementCorruptedChaosMaxGemCount,
            _ => Constant.MaxGemCount
        };

        return Mathf.Clamp(count, Constant.MinGemCount, Constant.MaxGemCount);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(HeroSkill.CanReplaceSkill))]
    public static bool CanReplaceSkill_Prefix(HeroSkill __instance, HeroSkillLocation type, ref bool __result)
    {
        // 如果技能槽被锁定，直接禁止
        if (__instance.entity.Ability.IsAbilityEditLocked((int)type))
        {
            __result = false;
            return false; // 跳过原方法
        }


        if (type == HeroSkillLocation.Identity && DewGemSlotCount.Instance.GameplayConfig.EditIdentitySkill)
        {
            __result = true;
            return false;
        }

        if (type == HeroSkillLocation.Movement && DewGemSlotCount.Instance.GameplayConfig.EditMovementSkill)
        {
            __result = true;
            return false;
        }

        // 默认逻辑：Identity / Movement 禁止，其余允许
        __result = type != HeroSkillLocation.Identity && type != HeroSkillLocation.Movement;
        return false;
    }


    [HarmonyPrefix]
    [HarmonyPatch(nameof(HeroSkill.TryGetEquippedGemOfSameType))]
    public static bool TryGetEquippedGemOfSameType_Prefix(HeroSkill __instance, ref bool __result, Type type,
        out GemLocation loc, out Gem gem)
    {
        foreach (KeyValuePair<GemLocation, Gem> p in __instance.gems)
        {
            if (p.Value.GetType() == type)
            {
                loc = p.Key;
                gem = p.Value;
                __result = !DewGemSlotCount.Instance.GameplayConfig.GemNoMerge;
                return false;
            }
        }

        loc = default;
        gem = null;
        __result = false;

        return false; // 跳过原方法
    }
}
