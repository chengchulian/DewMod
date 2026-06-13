using System.Collections.Generic;
using UnityEngine;

namespace DewPrimusHand.config;

public class PluginConfig : ModConfig
{

    [Header("Header.DiffConfiguration")]
    [LabelText("LabelText.MaxAndSpawnedPopulationMultiplier")]
    [Description("Description.MaxAndSpawnedPopulationMultiplier")]
    public float MaxAndSpawnedPopulationMultiplier = 1.5f;

    [LabelText("LabelText.EnemyMovementSpeedMultiplier")]
    [Description("Description.EnemyMovementSpeedMultiplier")]
    public float EnemyMovementSpeedMultiplier = 1;

    [LabelText("LabelText.EnemyAttackSpeedMultiplier")]
    [Description("Description.EnemyAttackSpeedMultiplier")]
    public float EnemyAttackSpeedMultiplier = 1;

    [LabelText("LabelText.EnemyAbilityHasteFlatMultiplier")]
    [Description("Description.EnemyAbilityHasteFlatMultiplier")]
    public float EnemyAbilityHasteFlatMultiplier = 1;

    [LabelText("LabelText.EnemyHealthMultiplier")]
    [Description("Description.EnemyHealthMultiplier")]
    public float EnemyHealthMultiplier = 1;

    [LabelText("LabelText.EnemyAttackDamageMultiplier")]
    [Description("Description.EnemyAttackDamageMultiplier")]
    public float EnemyAttackDamageMultiplier = 1;

    [LabelText("LabelText.EnemyAbilityPowerMultiplier")]
    [Description("Description.EnemyAbilityPowerMultiplier")]
    public float EnemyAbilityPowerMultiplier = 1;

    [LabelText("LabelText.EnemyAddArmor")]
    [Description("Description.EnemyAddArmor")]
    public float EnemyAddArmor = 0;

    [LabelText("LabelText.EnemyArmorMultiplierAddByZone")]
    [Description("Description.EnemyArmorMultiplierAddByZone")]
    public float EnemyArmorMultiplierAddByZone = 0;

    [LabelText("LabelText.EnemyExtraHealthGrowthMultiplier")]
    [Description("Description.EnemyExtraHealthGrowthMultiplier")]
    public float EnemyExtraHealthGrowthMultiplier = 1;

    [LabelText("LabelText.EnemyExtraDamageGrowthMultiplier")]
    [Description("Description.EnemyExtraDamageGrowthMultiplier")]
    public float EnemyExtraDamageGrowthMultiplier = 1;

    [Header("Header.LittleMonsterConfiguration")]
    [LabelText("LabelText.LittleMonsterHealthMultiplier")]
    [Description("Description.LittleMonsterHealthMultiplier")]
    public float LittleMonsterHealthMultiplier = 1;

    [LabelText("LabelText.LittleMonsterDamageMultiplier")]
    [Description("Description.LittleMonsterDamageMultiplier")]
    public float LittleMonsterDamageMultiplier = 1;

    [LabelText("LabelText.LittleMonsterMirageChanceMultiplier")]
    [Description("Description.LittleMonsterMirageChanceMultiplier")]
    public float LittleMonsterMirageChanceMultiplier = 1f;

    [Header("Header.BossConfiguration")]
    [LabelText("LabelText.BossHealthMultiplier")]
    [Description("Description.BossHealthMultiplier")]
    public float BossHealthMultiplier = 1;

    [LabelText("LabelText.BossDamageMultiplier")]
    [Description("Description.BossDamageMultiplier")]
    public float BossDamageMultiplier = 1;

    [LabelText("LabelText.MiniBossHealthMultiplier")]
    [Description("Description.MiniBossHealthMultiplier")]
    public float MiniBossHealthMultiplier = 1;

    [LabelText("LabelText.MiniBossDamageMultiplier")]
    [Description("Description.MiniBossDamageMultiplier")]
    public float MiniBossDamageMultiplier = 1;

    [LabelText("LabelText.BossCount")]
    [Description("Description.BossCount")]
    public int BossCount = 1;

    [LabelText("LabelText.BossCountInRoom")]
    [Description("Description.BossCountInRoom")]
    public int BossCountInRoom = 2;

    [LabelText("LabelText.BossCountAddByLoop")]
    [Description("Description.BossCountAddByLoop")]
    public int BossCountAddByLoop = 0;

    [LabelText("LabelText.BossCountAddByZone")]
    [Description("Description.BossCountAddByZone")]
    public int BossCountAddByZone = 0;

    [LabelText("LabelText.BossMirageChance")]
    [Description("Description.BossMirageChance")]
    public float BossMirageChance = 0;

    [LabelText("LabelText.BossHunterChance")]
    [Description("Description.BossHunterChance")]
    public float BossHunterChance = 0;

    [LabelText("LabelText.BossSpawnAllOnce")]
    [Description("Description.BossSpawnAllOnce")]
    public bool BossSpawnAllOnce = false;

    [LabelText("LabelText.BossSingleInjuryHealthMultiplier")]
    [Description("Description.BossSingleInjuryHealthMultiplier")]
    public float BossSingleInjuryHealthMultiplier = 1f;

    [Header("Header.HeroConfiguration")]
    [LabelText("LabelText.HeroHealMultiplier")]
    [Description("Description.HeroHealMultiplier")]
    public int HeroHealMultiplier = 1;

    [LabelText("LabelText.HeroMaxShieldMultiplier")]
    [Description("Description.HeroMaxShieldMultiplier")]
    public float HeroMaxShieldMultiplier = -1f;

    [LabelText("LabelText.HeroShieldCoolDownSeconds")]
    [Description("Description.HeroShieldCoolDownSeconds")]
    public float HeroShieldCoolDownSeconds = -1f;

    [LabelText("LabelText.HeroIgnoreShieldCoolDownFromOthers")]
    [Description("Description.HeroIgnoreShieldCoolDownFromOthers")]
    public bool HeroIgnoreShieldCoolDownFromOthers = true;

    [Header("Header.WorldConfiguration")]
    [LabelText("LabelText.WorldReveal")]
    [Description("Description.WorldReveal")]
    public bool WorldReveal = false;

    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        // 本地化UI
        LocalizationSource.LocalizeUI(parent);
    }

    public override void CopyTo(ModConfig other)
    {
        if (WorldReveal)
        {
            NetworkedManagerBase<ZoneManager>.instance.RevealWorld(true);
        }

        base.CopyTo(other);
    }
}
