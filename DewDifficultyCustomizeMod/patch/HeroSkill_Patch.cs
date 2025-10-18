using System;
using System.Collections.Generic;
using DewDifficultyCustomizeMod.util;
using DewDifficultyCustomizeMod.config;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(HeroSkill), nameof(HeroSkill.GetMaxGemCount))]
public static class HeroSkill_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(HeroSkill.GetMaxGemCount))]
    public static bool GetMaxGemCount_Prefix(HeroSkill __instance, HeroSkillLocation type, ref int __result)
    {
        switch (type)
        {
            case HeroSkillLocation.Q:
                __result = AttrCustomizeResources.Config.skillQGemCount;
                break;
            case HeroSkillLocation.W:
                __result = AttrCustomizeResources.Config.skillWGemCount;
                break;
            case HeroSkillLocation.E:
                __result = AttrCustomizeResources.Config.skillEGemCount;
                break;
            case HeroSkillLocation.R:
                __result = AttrCustomizeResources.Config.skillRGemCount;
                break;
            case HeroSkillLocation.Identity:
                __result = AttrCustomizeResources.Config.skillIdentityGemCount;
                break;
            case HeroSkillLocation.Movement:
                __result = AttrCustomizeResources.Config.skillMovementGemCount;
                break;
            default:
                __result = 0;
                break;
        }

        __result = Math.Clamp(__result, AttrCustomizeConstant.MinGemCount, AttrCustomizeConstant.MaxGemCount);

        return false; // 跳过原方法
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

        // 开启“自由编辑” → 所有技能都能替换
        if (AttrCustomizeResources.Config.enableAllSkillEdit)
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
                __result = AttrCustomizeResources.Config.enableGemMerge;
                return false;
            }
        }

        loc = default;
        gem = null;
        __result = false;

        return false; // 跳过原方法
    }
}