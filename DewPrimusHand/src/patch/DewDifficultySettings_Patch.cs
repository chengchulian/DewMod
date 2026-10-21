using System;
using System.Linq;
using System.Reflection;
using DewPrimusHand.util;
using HarmonyLib;
using UnityEngine;

namespace DewPrimusHand.patch;

[HarmonyPatch(typeof(DewDifficultySettings))]
public class DewDifficultySettings_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("ApplyDifficultyModifiers")]
    public static void ApplyDifficultyModifiers_Postfix(DewDifficultySettings __instance, Entity entity)
    {
        if (entity is IMonsterNoScaling)
            return;

        var config = DewPrimusHand.Instance.Config;
        switch (entity)
        {
            case Monster monster:
            {
                // 基础数值
                monster.Status.AddStatBonus(new StatBonus
                {
                    movementSpeedPercentage = DifficultyMath.ScaleDifficultyPercentage(
                        __instance.enemyMovementSpeedPercentage, config.EnemyMovementSpeedMultiplier),
                    attackSpeedPercentage = DifficultyMath.ScaleDifficultyPercentage(
                        __instance.enemyAttackSpeedPercentage, config.EnemyAttackSpeedMultiplier),
                    abilityHasteFlat = DifficultyMath.ScaleDifficultyPercentage(
                        __instance.enemyAbilityHasteFlat, config.EnemyAbilityHasteFlatMultiplier),
                    maxHealthPercentage = DifficultyMath.ScaleDifficultyPercentage(
                        __instance.enemyHealthPercentage, config.EnemyHealthMultiplier),
                    attackDamagePercentage = DifficultyMath.ScaleDifficultyPercentage(
                        __instance.enemyPowerPercentage, config.EnemyAttackDamageMultiplier),
                    abilityPowerPercentage = DifficultyMath.ScaleDifficultyPercentage(
                        __instance.enemyPowerPercentage, config.EnemyAbilityPowerMultiplier)
                });
                // 护甲
                var zoneIndex = GameManager.instance.difficulty.GetScaledZoneIndexForHealth();
                AddArmor(zoneIndex, monster);
                break;
            }
            case Hero hero:
            {
                entity.takenHealProcessor.Add(
                    delegate(ref HealData data, Actor actor, Entity target)
                    {
                        data.ApplyRawMultiplier(DifficultyMath.SanitizeMultiplier(
                            DewPrimusHand.Instance.Config.HeroHealMultiplier));
                    }, 100);

                float shieldTimeStamp = float.NegativeInfinity;
                entity.takenShieldProcessor.Add(delegate(ref HealData data, Actor from, Entity to)
                {
                    bool cooldownTrackedSource = from is Hero shieldSource &&
                                                 (!DewPrimusHand.Instance.Config.HeroIgnoreShieldCoolDownFromOthers ||
                                                  hero.owner == shieldSource.owner);
                    float coolDown = DewPrimusHand.Instance.Config.HeroShieldCoolDownSeconds;
                    if (cooldownTrackedSource && coolDown > 0f &&
                        Time.time - shieldTimeStamp < coolDown && data.currentAmount > 0f)
                    {
                        data.ApplyReduction(1f);
                        return;
                    }

                    // 超对称精粹降低英雄最大生命后，其他护盾也沿用精粹按原生命计算的上限。
                    Gem_L_Supersymmetry supersymmetry = hero.Skill.gems.Values
                        .OfType<Gem_L_Supersymmetry>()
                        .FirstOrDefault(gem => gem != null);
                    bool hasShieldLimit = false;
                    float maxShield = 0f;
                    if (supersymmetry != null)
                    {
                        maxShield = supersymmetry.maxHealthBeforeReduction *
                                    supersymmetry.GetValue(supersymmetry.shieldHpRatio);
                        hasShieldLimit = true;
                    }
                    else if (DewPrimusHand.Instance.Config.HeroMaxShieldMultiplier >= 0f)
                    {
                        maxShield = hero.Status.maxHealth * DifficultyMath.SanitizeMultiplier(
                            DewPrimusHand.Instance.Config.HeroMaxShieldMultiplier);
                        hasShieldLimit = true;
                    }

                    // 精粹自身通过 ProcessShieldAmount 计算此上限，避免把自己的值重复裁剪。
                    if (from is not Gem_L_Supersymmetry && hasShieldLimit)
                    {
                        float remainingShield = maxShield - hero.Status.currentShield;
                        if (data.currentAmount > remainingShield)
                        {
                            data.ApplyReduction((data.currentAmount - Mathf.Max(0f, remainingShield)) /
                                                data.currentAmount);
                        }
                    }

                    if (cooldownTrackedSource && coolDown > 0f && data.currentAmount > 0f)
                    {
                        shieldTimeStamp = Time.time;
                    }
                }, 100);
                break;
            }
        }
    }

    private static void AddArmor(float currentZoneIndex, Monster monster)
    {
        if (DewPrimusHand.Instance.Config.EnemyAddArmor > 0.000001)
        {
            float addArmor = DewPrimusHand.Instance.Config.EnemyAddArmor;

            if (DewPrimusHand.Instance.Config.EnemyArmorMultiplierAddByZone > 0.000001)
            {
                addArmor += addArmor *
                            (DewPrimusHand.Instance.Config.EnemyArmorMultiplierAddByZone * currentZoneIndex);
            }

            monster.Status.AddStatBonus(new StatBonus
            {
                armorFlat = addArmor
            });
        }
    }
}
