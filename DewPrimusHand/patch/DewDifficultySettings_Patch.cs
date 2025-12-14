using System;
using System.Reflection;
using HarmonyLib;

namespace DewPrimusHand.patch;

[HarmonyPatch(typeof(DewDifficultySettings))]
public class DewDifficultySettings_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("ApplyDifficultyModifiers")]
    public static void ApplyDifficultyModifiers_Postfix(DewDifficultySettings __instance, Entity entity)
    {
        switch (entity)
        {
            case Monster monster:
            {
                // 基础数值
                monster.Status.AddStatBonus(new StatBonus
                {
                    movementSpeedPercentage = __instance.enemyMovementSpeedPercentage *
                                              (DewPrimusHand.Instance.Config.EnemyMovementSpeedMultiplier - 1),
                    attackSpeedPercentage = __instance.enemyAttackSpeedPercentage *
                                            (DewPrimusHand.Instance.Config.EnemyAttackSpeedMultiplier - 1),
                    abilityHasteFlat = __instance.enemyAbilityHasteFlat *
                                       (DewPrimusHand.Instance.Config.EnemyAbilityHasteFlatMultiplier - 1),
                    maxHealthPercentage = __instance.enemyHealthPercentage *
                                          (DewPrimusHand.Instance.Config.EnemyHealthMultiplier - 1),
                    attackDamagePercentage = __instance.enemyPowerPercentage *
                                             (DewPrimusHand.Instance.Config.EnemyAttackDamageMultiplier - 1),
                    abilityPowerPercentage = __instance.enemyPowerPercentage *
                                             (DewPrimusHand.Instance.Config.EnemyAbilityPowerMultiplier - 1)
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
                        data.ApplyRawMultiplier(DewPrimusHand.Instance.Config.HeroHealMultiplier);
                    }, 100);

                DateTime shieldTimeStamp = DateTime.Now;
                entity.takenShieldProcessor.Add(delegate(ref HealData data, Actor from, Entity to)
                {
                    if (from is Hero hero2)
                    {
                        DewPlayer dewPlayer = hero.owner;
                        DewPlayer dewPlayer2 = hero2.owner;
                        if (DewPrimusHand.Instance.Config.HeroShieldCoolDownSeconds > 0.0 &&
                            (DateTime.Now - shieldTimeStamp).Seconds <
                            DewPrimusHand.Instance.Config.HeroShieldCoolDownSeconds)
                        {
                            if ((!DewPrimusHand.Instance.Config.HeroIgnoreShieldCoolDownFromOthers ||
                                 dewPlayer == dewPlayer2) &&
                                data.currentAmount > 0.0)
                            {
                                data.ApplyReduction(1f);
                            }
                        }
                        else
                        {
                            if (DewPrimusHand.Instance.Config.HeroMaxShieldMultiplier > 0.0)
                            {
                                float num = hero.Status.maxHealth *
                                            DewPrimusHand.Instance.Config.HeroMaxShieldMultiplier -
                                            hero.Status.currentShield;
                                if (data.currentAmount > num)
                                {
                                    data.ApplyReduction((data.currentAmount - num) / data.currentAmount);
                                }
                            }

                            if (data.currentAmount > 0.0)
                            {
                                shieldTimeStamp = DateTime.Now;
                            }
                        }
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