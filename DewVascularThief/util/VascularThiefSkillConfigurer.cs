using DewVascularThief.config;
using UnityEngine;

namespace DewVascularThief.util;

internal static class VascularThiefSkillConfigurer
{
    public static void Configure(St_U_VascularThief skill)
    {
        if (skill == null)
        {
            return;
        }

        VascularThiefSkillMarker.Mark(skill);
        skill.rarity = Rarity.Unique;
        skill.type = SkillType.Normal;
        skill.tags = DescriptionTags.None;
        skill.isLevelUpEnabled = true;
        skill.useCustomSkillHastePerLevel = false;
        skill.startEffect = null;
        skill.endEffect = null;
        skill.excludeFromPool = false;
        skill.specialOverlayColor = new Color(0.78f, 0.06f, 0.14f, 1f);
        skill.configs = new[] { CreateMainConfig(), CreateMainConfig() };
        VascularThiefSkillIcons.ApplyIcons(skill);
        TriggerConfigRuntimeEditor.AttachAll(skill);

        VascularThiefProxyConfig.ApplyStealMode(skill);
    }

    private static TriggerConfig CreateMainConfig()
    {
        TriggerConfig config = new TriggerConfig
        {
            isActive = true,
            startCharges = 1,
            spawnedInstance = null,
            appliedStatusEffect = null,
            destroyExistingEffect = false,
            startAnim = null,
            endAnim = null,
            castVoice = null,
            effectOnCast = null,
            victim = TriggerConfig.StatusEffectVictimType.Target,
            channel = new TriggerChannelData { duration = 0.05f },
            postDelay = 0.05f,
            selfValidator = AbilitySelfValidator.Default,
            ignoreBlock = false,
            ignoreAbilityLock = false,
            faceForward = true,
            overrideRotation = false,
            canReceiveCooldownReduction = true,
            postponeBasicCommand = false,
            moveTowardsCastDirection = false,
            canConsumeCastBonus = false,
            alwaysCastImmediately = false,
            castByMoveDirectionByDefault = false,
            castByMoveDirectionGamepad = false,
            ignoreAimDirectionGamepad = false,
            unstoppableWhileCasting = false,
            targetValidator = new AbilityTargetValidator(),
            castMethod = new CastMethodData(),
            predictionSettings = new AbilityTrigger.PredictionSettings
            {
                type = AbilityTrigger.PredictionSettings.ModelType.None
            }
        };
        TriggerConfigRuntimeEditor.SetManaCost(config, 0f);
        TriggerConfigRuntimeEditor.SetMaxCharges(config, 1);
        TriggerConfigRuntimeEditor.SetAddedCharges(config, 1);
        TriggerConfigRuntimeEditor.SetCooldownTime(config, 8f);
        TriggerConfigRuntimeEditor.SetMinimumDelay(config, 0.1f);
        return config;
    }
}
