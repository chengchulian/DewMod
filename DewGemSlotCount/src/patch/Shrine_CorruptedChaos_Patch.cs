using HarmonyLib;
using DewGemSlotCount.config;
using Mirror;
using UnityEngine;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(Shrine_CorruptedChaos))]
public static class Shrine_CorruptedChaos_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Shrine_CorruptedChaos.GetTargetInfo), typeof(DewPlayer), typeof(HeroSkillLocation), typeof(SkillTrigger))]
    public static bool GetTargetInfo_Prefix(Shrine_CorruptedChaos __instance, DewPlayer player, HeroSkillLocation loc,
        SkillTrigger target, ref EditSkillTargetInfo __result)
    {
        if (!IsAddedEssenceSlotReward(__instance, player))
        {
            return true;
        }

        int maxGemCount = player.hero.Skill.GetMaxGemCount(loc);
        int maxAllowedGemCount = HeroSkill_Patch.GetCorruptedChaosMaxGemCount(loc);
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
        if (IsAddedEssenceSlotReward(__instance, player))
        {
            if (IsMovementCorruptedChaosDisabled(loc))
            {
                __result = false;
                return false;
            }

            __instance.addedEssenceSlotMax = HeroSkill_Patch.GetCorruptedChaosMaxGemCount(loc);
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
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Se_Shrine_Chaos_StatBonus.AddEssenceSlotBonus))]
    public static bool AddEssenceSlotBonus_Prefix(Se_Shrine_Chaos_StatBonus __instance, HeroSkillLocation location)
    {
        if (location != HeroSkillLocation.Movement || !NetworkServer.active)
        {
            return true;
        }

        if (!DewGemSlotCount.Instance.GameplayConfig.AllowMovementCorruptedChaos)
        {
            return false;
        }

        if (__instance.victim is not Hero hero)
        {
            return false;
        }

        int vanillaBaseGemCount = HeroSkill_Patch.GetVanillaBaseGemCount(location);
        int configuredBaseGemCount = HeroSkill_Patch.GetConfiguredBaseGemCount(location);
        int maxAllowedGemCount = HeroSkill_Patch.GetCorruptedChaosMaxGemCount(location);
        int currentDynamicGemCount = Mathf.Max(0, hero.Skill.maxGemCountMovement - vanillaBaseGemCount);
        int maxDynamicGemCount = Mathf.Max(0, maxAllowedGemCount - configuredBaseGemCount);
        int nextDynamicGemCount = Mathf.Min(currentDynamicGemCount + __instance.addedGemSlot, maxDynamicGemCount);

        hero.Skill.SetMaxGemCount(location, vanillaBaseGemCount + nextDynamicGemCount);
        __instance.NotifyUpdate();
        return false;
    }
}
