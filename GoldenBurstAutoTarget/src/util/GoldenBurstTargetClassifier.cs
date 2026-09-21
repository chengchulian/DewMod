using System;
using GoldenBurstAutoTarget.config;

namespace GoldenBurstAutoTarget;

internal sealed class GoldenBurstTargetClassifier
{
    private readonly PluginConfig _config;

    public GoldenBurstTargetClassifier(PluginConfig config)
    {
        _config = config;
    }

    public bool IsAllowed(Hero hero, Entity target)
    {
        if (target == hero)
        {
            return false;
        }

        if (target is Hero)
        {
            return _config.TargetAllies &&
                   (!_config.RequireFriendlyTeammates || IsAlly(hero, target));
        }

        if (IsAlly(hero, target))
        {
            return _config.TargetAllies;
        }

        if (target is Monster monster)
        {
            if (_config.SkipDamageImmuneMonsters && monster.Status.hasDamageImmunity)
            {
                return false;
            }

            return monster.IsAnyBoss() ? _config.TargetBosses : _config.TargetMonsters;
        }

        if (IsStone(target))
        {
            return _config.TargetStones;
        }

        return false;
    }

    private static bool IsAlly(Hero hero, Entity target)
    {
        return hero.GetRelation(target) == EntityRelation.Ally;
    }

    private static bool IsStone(Entity target)
    {
        return target is PropEntity &&
               target.GetType().Name.StartsWith("PropEnt_Stone_", StringComparison.Ordinal);
    }
}
