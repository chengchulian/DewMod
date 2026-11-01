using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using DewGemSlotCount.controller;
using Mirror;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(HeroSkill))]
public class HeroSkill_Patch
{
    // 服务端初始化完整槽数，通过原生字典与四槽兼容字段单向同步。
    [HarmonyPrefix]
    [HarmonyPatch(nameof(HeroSkill.OnLateStartServer))]
    public static void OnLateStartServer_Prefix(HeroSkill __instance)
    {
        GemSlotController.ApplyConfig(__instance);
    }

    // 装 Mod 的端读取房主完整槽数；原版客机继续读取最多四槽的原生字段。
    [HarmonyPostfix]
    [HarmonyPatch(nameof(HeroSkill.GetMaxGemCount))]
    public static void GetMaxGemCount_Postfix(HeroSkill __instance, HeroSkillLocation type, ref int __result)
    {
        if (DewGemSlotCount.Instance == null) return;
        if (NetworkServer.active && __instance.isServer)
        {
            if (GemSlotController.TryGetCount(__instance, type, out int count)) __result = count;
        }
        else if (GemSlotStateSync.TryRead(__instance, type, out int count)) __result = count;
    }

    // 同一次服务端写入保存完整数量，再将兼容值交给原生网络同步。
    [HarmonyPrefix]
    [HarmonyPatch(nameof(HeroSkill.SetMaxGemCount))]
    public static void SetMaxGemCount_Prefix(HeroSkill __instance, HeroSkillLocation type, ref int count)
    {
        if (DewGemSlotCount.Instance == null) return;
        if (!NetworkServer.active || !__instance.isServer) return;
        count = Mathf.Clamp(count, Constant.MinGemCount, Constant.MaxGemCount);
        GemSlotController.RecordCount(__instance, type, count);
        count = Mathf.Clamp(count, Constant.MinGemCount, GemSlotStateSync.VanillaLimit);
    }

    public static int GetConfiguredBaseGemCount(HeroSkillLocation type, HeroSkill skill = null)
    {
        var config = DewGemSlotCount.Instance?.GameplayConfig;
        if (config == null) return skill?.GetMaxGemCount(type) ?? GetVanillaBaseGemCount(type);
        int count = type switch
        {
            HeroSkillLocation.Q => config.SkillQGemCount,
            HeroSkillLocation.W => config.SkillWGemCount,
            HeroSkillLocation.E => config.SkillEGemCount,
            HeroSkillLocation.R => config.SkillRGemCount,
            HeroSkillLocation.Identity => config.SkillIdentityGemCount,
            HeroSkillLocation.Movement => config.SkillMovementGemCount,
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

    public static int GetCorruptedChaosMaxGemCount(HeroSkillLocation type, DewPlayer player = null)
    {
        if (DewGemSlotCount.Instance == null) return GemSlotStateSync.VanillaLimit;
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
        if (DewGemSlotCount.Instance == null) return true;
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
        loc = default;
        gem = null;
        if (DewGemSlotCount.Instance == null) return true;
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
