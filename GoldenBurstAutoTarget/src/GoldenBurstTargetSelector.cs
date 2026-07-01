using System.Collections.Generic;
using UnityEngine;

namespace GoldenBurstAutoTarget;

internal sealed class GoldenBurstTargetSelector
{
    private readonly GoldenBurstTargetClassifier _targetClassifier;

    public GoldenBurstTargetSelector(GoldenBurstTargetClassifier targetClassifier)
    {
        _targetClassifier = targetClassifier;
    }

    public Entity FindNearestTarget(Hero hero, SkillTrigger skill)
    {
        TriggerConfig config = skill.currentConfig;
        float range = Mathf.Max(config.effectiveRange, 1f);
        ListReturnHandle<Entity> handle;
        List<Entity> candidates = DewPhysics.OverlapCircleAllEntities(
            out handle,
            hero.agentPosition,
            range,
            entity => IsValidTarget(hero, config, entity),
            new CollisionCheckSettings
            {
                sortComparer = CollisionCheckSettings.DistanceFromCenter
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

    private bool IsValidTarget(Hero hero, TriggerConfig config, Entity entity)
    {
        return entity != null &&
               !entity.IsNullInactiveDeadOrKnockedOut() &&
               config.CheckRange(hero, entity) &&
               _targetClassifier.IsAllowed(hero, entity) &&
               !entity.Status.hasInvisible &&
               !entity.Status.hasUntargetable &&
               !entity.Status.isUndetectableByNonAllies;
    }
}
