using System;
using Mirror;

namespace DewVascularThief.util;

internal static class VascularThiefProxyConfig
{
    public static void ApplyStealMode(SkillTrigger skill)
    {
        if (!TryGetConfig(skill, VascularThiefSkillMode.Steal, out TriggerConfig config))
        {
            return;
        }

        VascularThiefSkillIcons.ApplyIcons(skill);
        config.alwaysCastImmediately = false;
        config.castByMoveDirectionByDefault = false;
        config.castByMoveDirectionGamepad = false;
        config.ignoreAimDirectionGamepad = false;
        config.targetValidator = new AbilityTargetValidator { targets = EntityRelation.Enemy | EntityRelation.Neutral };
        config.castMethod = new CastMethodData
        {
            type = CastMethodType.Target,
            _range = 12f,
            _radius = 0f
        };
        SetCurrentConfig(skill, VascularThiefSkillMode.Steal);
        SyncCastMethodIfSpawned(skill, VascularThiefSkillMode.Steal);
    }

    public static void ApplyStolenAbilityMode(SkillTrigger skill, AbilityTrigger stolenAbility)
    {
        if (!TryGetConfig(skill, VascularThiefSkillMode.Stolen, out TriggerConfig config) ||
            stolenAbility == null ||
            stolenAbility.configs == null ||
            stolenAbility.currentConfigIndex < 0 ||
            stolenAbility.currentConfigIndex >= stolenAbility.configs.Length)
        {
            return;
        }

        TriggerConfig stolenConfig = stolenAbility.currentConfig;
        config.castMethod = new CastMethodData(stolenConfig.castMethod);
        config.targetValidator = stolenConfig.targetValidator ?? new AbilityTargetValidator();
        config.alwaysCastImmediately = stolenConfig.castMethod.type == CastMethodType.None ||
                                       stolenConfig.alwaysCastImmediately;
        config.castByMoveDirectionByDefault = stolenConfig.castByMoveDirectionByDefault;
        config.castByMoveDirectionGamepad = stolenConfig.castByMoveDirectionGamepad;
        config.ignoreAimDirectionGamepad = stolenConfig.ignoreAimDirectionGamepad;
        SetCurrentConfig(skill, VascularThiefSkillMode.Stolen);
        SyncCastMethodIfSpawned(skill, VascularThiefSkillMode.Stolen);
    }

    private static bool TryGetConfig(SkillTrigger skill, int configIndex, out TriggerConfig config)
    {
        config = null;
        if (skill == null ||
            skill.configs == null ||
            configIndex < 0 ||
            configIndex >= skill.configs.Length ||
            skill.configs[configIndex] == null)
        {
            return false;
        }

        config = skill.configs[configIndex];
        return true;
    }

    private static void SetCurrentConfig(SkillTrigger skill, int configIndex)
    {
        if (skill == null || skill.currentConfigIndex == configIndex)
        {
            return;
        }

        skill.currentConfigIndex = configIndex;
    }

    private static void SyncCastMethodIfSpawned(SkillTrigger skill, int configIndex)
    {
        if (skill == null)
        {
            return;
        }

        NetworkIdentity identity = skill.GetComponent<NetworkIdentity>();
        if (identity == null || !identity.isServer || identity.netId == 0)
        {
            return;
        }

        try
        {
            skill.SyncCastMethodChanges(configIndex);
        }
        catch (NullReferenceException)
        {
            // Runtime prefabs can have a NetworkIdentity before Mirror wires the NetworkBehaviour.
        }
    }
}
