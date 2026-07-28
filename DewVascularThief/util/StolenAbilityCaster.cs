using System;
using DewVascularThief.config;
using UnityEngine;

namespace DewVascularThief.util;

internal static class StolenAbilityCaster
{
    public static void Cast(AbilityTrigger ability, Hero hero, CastInfo sourceInfo)
    {
        if (ability == null || hero == null || !ability.isActive || ability.owner != hero)
        {
            return;
        }

        if (!CanAttemptCast(ability))
        {
            Debug.Log($"[{VascularThiefText.ModKey}] Stolen ability {ability.GetType().Name} is not ready.");
            return;
        }

        try
        {
            CastInfo castInfo = BuildCastInfo(ability, hero, sourceInfo);
            hero.Control.Cast(ability, ability.currentConfigIndex, castInfo, allowMoveToCast: true);
            Debug.Log($"[{VascularThiefText.ModKey}] {hero.GetActorReadableName()} cast stolen ability {ability.GetType().Name}.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static bool CanAttemptCast(AbilityTrigger ability)
    {
        return ability.configs != null &&
               ability.currentConfigIndex >= 0 &&
               ability.currentConfigIndex < ability.configs.Length &&
               ability.currentUnscaledCooldownTimes != null &&
               ability.currentCharges != null &&
               ability.currentMinimumDelays != null &&
               ability.currentUnscaledCooldownTimes.Length > ability.currentConfigIndex &&
               ability.currentCharges.Length > ability.currentConfigIndex &&
               ability.currentMinimumDelays.Length > ability.currentConfigIndex &&
               ability.CanBeReserved();
    }

    private static CastInfo BuildCastInfo(AbilityTrigger ability, Hero hero, CastInfo sourceInfo)
    {
        TriggerConfig config = ability.currentConfig;
        Entity target = SelectTarget(ability, hero, sourceInfo);

        if (target != null)
        {
            return ability.GetCastInfoToTarget(target);
        }

        Vector3 direction = GetDirection(hero, sourceInfo);
        switch (config.castMethod.type)
        {
            case CastMethodType.None:
                return new CastInfo(hero);
            case CastMethodType.Cone:
            case CastMethodType.Arrow:
                return new CastInfo(hero, CastInfo.GetAngle(direction));
            case CastMethodType.Point:
                return new CastInfo(hero, GetPoint(hero, sourceInfo, direction, config.castMethod.pointData.range));
            case CastMethodType.Target:
                throw new InvalidOperationException($"No valid target for stolen ability {ability.GetType().Name}.");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static Entity SelectTarget(AbilityTrigger ability, Hero hero, CastInfo sourceInfo)
    {
        TriggerConfig config = ability.currentConfig;
        if (sourceInfo.target != null &&
            !sourceInfo.target.IsNullInactiveDeadOrKnockedOut() &&
            config.targetValidator != null &&
            config.targetValidator.Evaluate(hero, sourceInfo.target) &&
            (config.castMethod.type != CastMethodType.Target || config.CheckRange(hero, sourceInfo.target)))
        {
            return sourceInfo.target;
        }

        if (config.targetValidator == null)
        {
            return null;
        }

        float range = Mathf.Max(config.effectiveRange, 1f);
        ListReturnHandle<Entity> handle;
        var candidates = DewPhysics.OverlapCircleAllEntities(out handle, hero.agentPosition, range, config.targetValidator, hero, new CollisionCheckSettings
        {
            sortComparer = CollisionCheckSettings.DistanceFromCenter,
            includeUncollidable = true
        });

        try
        {
            return candidates.Count > 0 ? candidates[0] : null;
        }
        finally
        {
            handle.Return();
        }
    }

    private static Vector3 GetDirection(Hero hero, CastInfo sourceInfo)
    {
        if (sourceInfo.target != null && !sourceInfo.target.IsNullInactiveDeadOrKnockedOut())
        {
            Vector3 toTarget = sourceInfo.target.agentPosition - hero.agentPosition;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                return toTarget.Flattened().normalized;
            }
        }

        if (sourceInfo.point != Vector3.zero)
        {
            Vector3 toPoint = sourceInfo.point - hero.agentPosition;
            if (toPoint.sqrMagnitude > 0.01f)
            {
                return toPoint.Flattened().normalized;
            }
        }

        if (sourceInfo.forward.sqrMagnitude > 0.01f)
        {
            return sourceInfo.forward.Flattened().normalized;
        }

        return hero.transform.forward.Flattened().normalized;
    }

    private static Vector3 GetPoint(Hero hero, CastInfo sourceInfo, Vector3 direction, float range)
    {
        if (sourceInfo.point != Vector3.zero)
        {
            return sourceInfo.point;
        }

        if (sourceInfo.target != null && !sourceInfo.target.IsNullInactiveDeadOrKnockedOut())
        {
            return sourceInfo.target.agentPosition;
        }

        return hero.agentPosition + direction * range;
    }
}
