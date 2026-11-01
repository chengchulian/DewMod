using HarmonyLib;
using DewGemSlotCount.config;
using Mirror;
using UnityEngine;
using DewGemSlotCount.controller;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(Shrine_CorruptedChaos))]
public static class Shrine_CorruptedChaos_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Shrine_CorruptedChaos.GetTargetInfo), typeof(DewPlayer), typeof(HeroSkillLocation), typeof(SkillTrigger))]
    public static bool GetTargetInfo_Prefix(Shrine_CorruptedChaos __instance, DewPlayer player, HeroSkillLocation loc,
        SkillTrigger target, ref EditSkillTargetInfo __result)
    {
        if (DewGemSlotCount.Instance == null) return true;
        if (!IsAddedEssenceSlotReward(__instance, player))
        {
            return true;
        }

        int maxGemCount = player.hero.Skill.GetMaxGemCount(loc);
        int maxAllowedGemCount = HeroSkill_Patch.GetCorruptedChaosMaxGemCount(loc, player);
        string actionType = DewLocalization.GetUIValue("Shrine_CorruptedChaos_AddedEssenceSlot_ActionVerb");

        if (IsMovementCorruptedChaosDisabled(loc))
        {
            __result = new EditSkillTargetInfo
            {
                rejectReasonRawText = LocalizationSource.GetLocalizationText("Message.MovementCorruptedChaosDisabled"),
                actionTypeRawText = actionType
            };
            return false;
        }

        if (maxGemCount >= maxAllowedGemCount)
        {
            __result = new EditSkillTargetInfo
            {
                rejectReasonRawText = DewLocalization.GetUIValue("Shrine_CorruptedChaos_AddedEssenceSlot_MaxReached"),
                actionTypeRawText = actionType
            };
            return false;
        }

        __result = new EditSkillTargetInfo
        {
            tooltipRawText = string.Format("{0} {1}<sprite=0>{2}",
                DewLocalization.GetUIValue("Shrine_CorruptedChaos_AddedEssenceSlot_Tooltip"),
                maxGemCount,
                maxGemCount + 1),
            actionTypeRawText = actionType
        };
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnActivateEditSkill", typeof(DewPlayer), typeof(HeroSkillLocation), typeof(SkillTrigger))]
    public static bool OnActivateEditSkill_Prefix(Shrine_CorruptedChaos __instance, DewPlayer player,
        HeroSkillLocation loc, SkillTrigger target, ref bool __result)
    {
        if (DewGemSlotCount.Instance == null) return true;
        if (IsAddedEssenceSlotReward(__instance, player))
        {
            if (IsMovementCorruptedChaosDisabled(loc))
            {
                __result = false;
                return false;
            }

            __instance.addedEssenceSlotMax = HeroSkill_Patch.GetCorruptedChaosMaxGemCount(loc, player);
        }

        return true;
    }

    private static bool IsAddedEssenceSlotReward(Shrine_CorruptedChaos shrine, DewPlayer player)
    {
        if (shrine == null || player == null || !int.TryParse(shrine.GetCustomData(player), out int index))
        {
            return false;
        }

        if (!shrine.rewards.TryGetValue(player.guid, out CorruptedChaosRewardType[] rewards))
        {
            return false;
        }

        return index >= 0 && index < rewards.Length && rewards[index] == CorruptedChaosRewardType.AddedEssenceSlot;
    }

    private static bool IsMovementCorruptedChaosDisabled(HeroSkillLocation loc)
    {
        return loc == HeroSkillLocation.Movement && !DewGemSlotCount.Instance.GameplayConfig.AllowMovementCorruptedChaos;
    }
}

[HarmonyPatch(typeof(Se_Shrine_Chaos_StatBonus))]
public static class Se_Shrine_Chaos_StatBonus_Patch
{
    // 包括读档恢复和任意混沌奖励触发的重算，均发布完整槽数与原版兼容字段。
    [HarmonyPostfix]
    [HarmonyPatch("NotifyUpdate_Corrupted")]
    public static void NotifyUpdate_Corrupted_Postfix(Se_Shrine_Chaos_StatBonus __instance)
    {
        GemSlotController.ApplyChaosBonus(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Se_Shrine_Chaos_StatBonus.AddEssenceSlotBonus))]
    public static bool AddEssenceSlotBonus_Prefix(Se_Shrine_Chaos_StatBonus __instance, HeroSkillLocation location)
    {
        if (DewGemSlotCount.Instance == null) return true;
        if (!NetworkServer.active)
        {
            return true;
        }

        // 服务端直接加槽入口也检查房主配置上限；原版客机的四槽 UI 自行限制可选范围。
        if (__instance.victim is not Hero hero) return false;
        int maxAllowedGemCount = HeroSkill_Patch.GetCorruptedChaosMaxGemCount(location, hero.owner);
        if (hero.Skill.GetMaxGemCount(location) >= maxAllowedGemCount) return false;
        if (location != HeroSkillLocation.Movement) return true;

        if (!DewGemSlotCount.Instance.GameplayConfig.AllowMovementCorruptedChaos)
        {
            return false;
        }

        int configuredBaseGemCount = HeroSkill_Patch.GetConfiguredBaseGemCount(location, hero.Skill);
        int currentDynamicGemCount = Mathf.Max(0, hero.Skill.GetMaxGemCount(location) - configuredBaseGemCount);
        int maxDynamicGemCount = Mathf.Max(0, maxAllowedGemCount - configuredBaseGemCount);
        int nextDynamicGemCount = Mathf.Min(currentDynamicGemCount + __instance.addedGemSlot, maxDynamicGemCount);

        hero.Skill.SetMaxGemCount(location, configuredBaseGemCount + nextDynamicGemCount);
        __instance.NotifyUpdate();
        return false;
    }
}
