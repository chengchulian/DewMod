using DewVascularThief.config;
using UnityEngine;

namespace DewVascularThief.util;

internal static class VascularThiefUpgradeScaling
{
    private const string DamageProcessorInstalledKey = "DamageProcessorInstalled";

    private static readonly ScalingValue DamageScaling = new ScalingValue(1f, 0f, 0f, 0f, 0f, 0f, 0f, LevelScaling.SkillDefault);

    public static float GetDamageMultiplier(SkillTrigger sourceSkill)
    {
        return GetDamageMultiplier(sourceSkill?.level ?? 1);
    }

    public static float GetDamageMultiplier(int level)
    {
        return DamageScaling.GetScalingMultiplier(level);
    }

    public static int GetDamagePercent(SkillTrigger sourceSkill)
    {
        return Mathf.RoundToInt(GetDamageMultiplier(sourceSkill) * 100f);
    }

    public static int GetDamagePercent(int level)
    {
        return Mathf.RoundToInt(GetDamageMultiplier(level) * 100f);
    }

    public static void InitializeStolenAbility(SkillTrigger sourceSkill, AbilityTrigger stolenAbility)
    {
        if (sourceSkill == null || stolenAbility == null)
        {
            return;
        }

        EnsureDamageProcessor(sourceSkill, stolenAbility);
    }

    public static void SyncStolenAbility(SkillTrigger sourceSkill, AbilityTrigger stolenAbility)
    {
        EnsureDamageProcessor(sourceSkill, stolenAbility);
    }

    private static void EnsureDamageProcessor(SkillTrigger sourceSkill, AbilityTrigger stolenAbility)
    {
        string key = $"{VascularThiefText.ModKey}::{DamageProcessorInstalledKey}";
        if (stolenAbility.persistentData.GetDataOrDefault(key, false))
        {
            return;
        }

        stolenAbility.dealtDamageProcessor.Add(delegate(ref DamageData data, Actor actor, Entity target)
        {
            ApplyDamageScaling(ref data, sourceSkill, stolenAbility, target);
        });
        stolenAbility.persistentData.SetData(key, true);
    }

    private static void ApplyDamageScaling(ref DamageData data, SkillTrigger sourceSkill, AbilityTrigger stolenAbility, Entity target)
    {
        if (sourceSkill == null ||
            stolenAbility == null ||
            stolenAbility.owner == null ||
            !stolenAbility.owner.CheckEnemyOrNeutral(target) ||
            data.IsAmountModifiedBy(typeof(VascularThiefUpgradeScaling)))
        {
            return;
        }

        float multiplier = GetDamageMultiplier(sourceSkill);
        if (multiplier <= 1f)
        {
            return;
        }

        data.ApplyAmplification(multiplier - 1f);
        data.SetAmountModifiedBy(typeof(VascularThiefUpgradeScaling));
    }
}
