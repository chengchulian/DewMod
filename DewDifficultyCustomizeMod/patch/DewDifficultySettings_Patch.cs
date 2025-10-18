using System.Reflection;
using DewDifficultyCustomizeMod.config;
using DewDifficultyCustomizeMod.util;
using HarmonyLib;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(DewDifficultySettings))]
public static class DewDifficultySettings_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("ApplyDifficultyModifiers")]
    public static bool ApplyDifficultyModifiers_Prefix(DewDifficultySettings __instance, Entity entity)
    {
        if (entity is Monster m0)
        {
            StatBonus bonus = new StatBonus
            {
                maxHealthPercentage = __instance.enemyHealthPercentage,
                attackDamagePercentage = __instance.enemyPowerPercentage,
                abilityPowerPercentage = __instance.enemyPowerPercentage,
                movementSpeedPercentage = __instance.enemyMovementSpeedPercentage *
                                          AttrCustomizeResources.Config.enemyMovementSpeedPercentage,
                attackSpeedPercentage = __instance.enemyAttackSpeedPercentage *
                                        AttrCustomizeResources.Config.enemyAttackSpeedPercentage,
                abilityHasteFlat = __instance.enemyAbilityHasteFlat *
                                   AttrCustomizeResources.Config.enemyAbilityHasteFlat
            };
            if (m0.type == Monster.MonsterType.Boss)
            {
                bonus.armorFlat = __instance.heroicBossArmor;
            }
            else if (m0.type == Monster.MonsterType.MiniBoss)
            {
                bonus.armorFlat = __instance.miniBossArmor;
            }

            entity.Status.AddStatBonus(bonus);
        }

        if (entity is Monster m1)
        {
            float healthMultiplier;
            float damageMultiplier;
            if (m1 is BossMonster)
            {
                healthMultiplier = NetworkedManagerBase<GameManager>.instance
                    .GetBossMonsterHealthMultiplierByScaling();
                damageMultiplier = NetworkedManagerBase<GameManager>.instance
                    .GetBossMonsterDamageMultiplierByScaling();
                healthMultiplier *= AttrCustomizeResources.Config.bossHealthMultiplier;
                damageMultiplier *= AttrCustomizeResources.Config.bossDamageMultiplier;
            }
            else if (m1.type == Monster.MonsterType.MiniBoss)
            {
                healthMultiplier = NetworkedManagerBase<GameManager>.instance
                    .GetMiniBossMonsterHealthMultiplierByScaling();
                damageMultiplier = NetworkedManagerBase<GameManager>.instance
                    .GetMiniBossMonsterDamageMultiplierByScaling();
                healthMultiplier *= AttrCustomizeResources.Config.miniBossHealthMultiplier;
                damageMultiplier *= AttrCustomizeResources.Config.miniBossDamageMultiplier;
            }
            else
            {
                healthMultiplier = NetworkedManagerBase<GameManager>.instance
                    .GetRegularMonsterHealthMultiplierByScaling();
                damageMultiplier = NetworkedManagerBase<GameManager>.instance
                    .GetRegularMonsterDamageMultiplierByScaling();
                healthMultiplier *= AttrCustomizeResources.Config.littleMonsterHealthMultiplier;
                damageMultiplier *= AttrCustomizeResources.Config.littleMonsterDamageMultiplier;
            }

            int instanceCurrentZoneIndex = NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex;

            healthMultiplier = AttrCustomizeUtil.ExponentialGrowth(instanceCurrentZoneIndex,
                healthMultiplier, AttrCustomizeResources.Config.extraHealthGrowthMultiplier);
            damageMultiplier = AttrCustomizeUtil.ExponentialGrowth(instanceCurrentZoneIndex,
                damageMultiplier, AttrCustomizeResources.Config.extraDamageGrowthMultiplier);

            AddArmor(instanceCurrentZoneIndex, m1);

            entity.Status.AddStatBonus(new StatBonus
            {
                maxHealthPercentage = (healthMultiplier - 1f) * 100f,
                attackDamagePercentage = (damageMultiplier - 1f) * 100f,
                abilityPowerPercentage = (damageMultiplier - 1f) * 100f
            });
        }

        else if (entity is Hero)
        {
            entity.takenHealProcessor.Add(
                delegate(ref HealData data, Actor actor, Entity target)
                {
                    data.ApplyRawMultiplier(
                        NetworkedManagerBase<GameManager>.instance.difficulty.healRawMultiplier *
                        AttrCustomizeResources.Config.healRawMultiplier);
                }, 100);
        }

        // 跳过原始方法
        return false;
    }

    private static void AddArmor(int currentZoneIndex, Monster monster)
    {
        if (AttrCustomizeResources.Config.monsterBaseArmor > 0.000001)
        {
            float addArmor = AttrCustomizeResources.Config.monsterBaseArmor;

            if (AttrCustomizeResources.Config.monsterArmorPercentageAddByZone > 0.000001)
            {
                addArmor += addArmor *
                            (AttrCustomizeResources.Config.monsterArmorPercentageAddByZone * currentZoneIndex);
            }

            monster.Status.baseStats.armor += addArmor;
        }
    }
}