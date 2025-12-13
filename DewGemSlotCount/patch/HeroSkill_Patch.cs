using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(HeroSkill), nameof(HeroSkill.GetMaxGemCount))]
public class HeroSkill_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(HeroSkill.GetMaxGemCount))]
    public static bool GetMaxGemCount_Prefix(HeroSkill __instance, HeroSkillLocation type, ref int __result)
    {
        __result = type switch
        {
            HeroSkillLocation.Q => DewGemSlotCount.Instance.Config.SkillQGemCount,
            HeroSkillLocation.W => DewGemSlotCount.Instance.Config.SkillWGemCount,
            HeroSkillLocation.E => DewGemSlotCount.Instance.Config.SkillEGemCount,
            HeroSkillLocation.R => DewGemSlotCount.Instance.Config.SkillRGemCount,
            HeroSkillLocation.Identity => DewGemSlotCount.Instance.Config.SkillIdentityGemCount,
            HeroSkillLocation.Movement => DewGemSlotCount.Instance.Config.SkillMovementGemCount,
            _ => 0
        };

        __result = Mathf.Clamp(__result, Constant.MinGemCount, Constant.MaxGemCount);
        // 跳过原方法
        return false;
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


        if (type == HeroSkillLocation.Identity && DewGemSlotCount.Instance.Config.EditIdentitySkill)
        {
            __result = true;
            return false;
        }

        if (type == HeroSkillLocation.Movement && DewGemSlotCount.Instance.Config.EditMovementSkill)
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
                __result = !DewGemSlotCount.Instance.Config.GemNoMerge;
                return false;
            }
        }

        loc = default;
        gem = null;
        __result = false;

        return false; // 跳过原方法
    }
}