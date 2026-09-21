using System.Runtime.CompilerServices;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace DewAttackSpeedConvertDamage.patch;

[HarmonyPatch(typeof(EntityStatus))]
public static class EntityStatus_Patch
{
    public static readonly ConditionalWeakTable<Hero, BonusHolder> DamageBonusMap = new();

    // patch get_attackSpeedMultiplier
    [HarmonyPostfix]
    [HarmonyPatch("get_attackSpeedMultiplier")]
    public static void get_attackSpeedMultiplier_Postfix(EntityStatus __instance, ref float __result)
    {
        var maxAttackSpeed = DewAttackSpeedConvertDamage.Instance.config.MaxAttackSpeed;
        var damagePerOverflow = DewAttackSpeedConvertDamage.Instance.config.DamagePerOverflow;

        if (__instance.entity is not Hero hero) return;

        if (!NetworkServer.active)
        {
            return;
        }

        float baseAttackSpeed = GetActualBaseAttackSpeed(__instance);
        float uncappedBonusMultiplier = GetUncappedBonusMultiplier(__instance);
        float cappedMultiplier = Mathf.Max(0f, __result - uncappedBonusMultiplier);
        float maxAttackSpeedMultiplier = maxAttackSpeed / baseAttackSpeed;

        // 修正燃烧弹填装与挚爱的临时急速被错误计入攻速上限的问题。
        float overflowMultiplier = Mathf.Max(0f, cappedMultiplier - maxAttackSpeedMultiplier);
        float damageBonus = overflowMultiplier * damagePerOverflow;
        DamageBonusMap.GetOrCreateValue(hero).bonus = damageBonus;

        // 先限制其他来源，再完整保留这两个效果提供的攻速。
        __result = Mathf.Min(cappedMultiplier, maxAttackSpeedMultiplier) + uncappedBonusMultiplier;

        // 确保伤害处理器已注册
        if (hero.HasData<AttackSpeedUpperConvertDamage>())
        {
            return;
        }

        hero.AddData<AttackSpeedUpperConvertDamage>(default);
        hero.dealtDamageProcessor.Add(Processor, priority: 500);
    }

    private static float GetActualBaseAttackSpeed(EntityStatus status)
    {
        var entity = status.entity;
        if (!(entity?.Ability?.attackAbility?.configs?.Length > 0)) return 1f;

        float cd = entity.Ability.attackAbility.configs[0].cooldownTime;
        return cd > 0f ? 1f / cd : 1f;
    }

    private static float GetUncappedBonusMultiplier(EntityStatus status)
    {
        float hastePercentage = 0f;
        foreach (StatusEffect effect in status.statusEffects)
        {
            if (effect is Se_Q_IncendiaryRounds_EmpowerAttacks incendiaryRounds)
            {
                hastePercentage += Mathf.Max(0f, incendiaryRounds.GetValue(incendiaryRounds.hasteAmount));
            }
            else if (effect is Se_Gem_C_Love love)
            {
                float selfMultiplier = love.info.caster == love.victim ? 1f - love.selfReduction : 1f;
                hastePercentage += Mathf.Max(0f, love.GetValue(love.bonusAmount) * selfMultiplier);
            }
        }

        // EntityStatus 在最终属性中会让所有急速共同受到致残倍率影响。
        float crippleMultiplier = Mathf.Clamp01(1f - status.totalCripple / 100f);
        return hastePercentage / 100f * crippleMultiplier;
    }

    private static void Processor(ref DamageData data, Actor actor, Entity target)
    {
        if (!actor.IsDescendantOf(target))
        {
            Entity attacker = actor.firstEntity;
            if (attacker is not Hero hero) return;

            if (!DamageBonusMap.TryGetValue(hero, out var holder) || holder.bonus <= 0f) return;
            data.ApplyAmplification(holder.bonus);
        }
    }

    public struct AttackSpeedUpperConvertDamage
    {
    }

    public class BonusHolder
    {
        public float bonus;
    }
}
