using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace DewAttackSpeedUpperTurnHurt.patch;

[HarmonyPatch(typeof(EntityStatus))]
public static class EntityStatus_Patch
{
    private const float MAX_ATTACK_SPEED = 5f; // 攻速上限（倍数）
    private const float DAMAGE_PER_OVERFLOW = 0.01f; // 每 100% 溢出攻速 → +1% 伤害

    public static readonly ConditionalWeakTable<Hero, BonusHolder> DamageBonusMap = new();

    // patch get_attackSpeedMultiplier
    [HarmonyPostfix]
    [HarmonyPatch("get_attackSpeedMultiplier")]
    public static void get_attackSpeedMultiplier_Postfix(EntityStatus __instance, ref float __result)
    {
        if (__instance.entity is not Hero hero) return;
        
        if (!__instance.isServer)
        {
            return;
        }

        float baseAttackSpeed = GetActualBaseAttackSpeed(__instance);
        float currentAttackSpeed = baseAttackSpeed * __result;

        // 计算溢出
        float overflow = Mathf.Max(0f, currentAttackSpeed - MAX_ATTACK_SPEED);
        
        float overflowPercentage = overflow / baseAttackSpeed;
        float damageBonus = overflowPercentage * DAMAGE_PER_OVERFLOW;
        DamageBonusMap.GetOrCreateValue(hero).bonus = damageBonus;

        // 限制返回值
        if (overflow > 0f)
        {
            __result = MAX_ATTACK_SPEED / baseAttackSpeed;
        }

        // 确保伤害处理器已注册
        if (!hero.HasData<AttackSpeedUpperTurnHurt>())
        {
            hero.AddData<AttackSpeedUpperTurnHurt>(default);
            hero.dealtDamageProcessor.Add(Processor, priority: 500);
        }
    }

    private static float GetActualBaseAttackSpeed(EntityStatus status)
    {
        var entity = status.entity;
        if (!(entity?.Ability?.attackAbility?.configs?.Length > 0)) return 1f;

        float cd = entity.Ability.attackAbility.configs[0].cooldownTime;
        return cd > 0f ? 1f / cd : 1f;
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

    public struct AttackSpeedUpperTurnHurt
    {
    }

    public class BonusHolder
    {
        public float bonus;
    }
}