using UnityEngine;

namespace DewGemSlotCount.config;

public class PluginConfig : ModConfig
{
    
    
    [LabelText("LabelText.SkillQGemCount")]
    public int SkillQGemCount = 3;
    [LabelText("LabelText.SkillWGemCount")]
    public int SkillWGemCount = 3;
    [LabelText("LabelText.SkillEGemCount")]
    public int SkillEGemCount = 3;
    [LabelText("LabelText.SkillRGemCount")]
    public int SkillRGemCount = 3;
    [LabelText("LabelText.SkillIdentityGemCount")]
    public int SkillIdentityGemCount = 0;
    [LabelText("LabelText.SkillMovementGemCount")]
    public int SkillMovementGemCount = 0;

    [LabelText("LabelText.SkillQCorruptedChaosMaxGemCount")]
    public int SkillQCorruptedChaosMaxGemCount = 4;
    [LabelText("LabelText.SkillWCorruptedChaosMaxGemCount")]
    public int SkillWCorruptedChaosMaxGemCount = 4;
    [LabelText("LabelText.SkillECorruptedChaosMaxGemCount")]
    public int SkillECorruptedChaosMaxGemCount = 4;
    [LabelText("LabelText.SkillRCorruptedChaosMaxGemCount")]
    public int SkillRCorruptedChaosMaxGemCount = 4;
    [LabelText("LabelText.SkillIdentityCorruptedChaosMaxGemCount")]
    public int SkillIdentityCorruptedChaosMaxGemCount = 4;
    [LabelText("LabelText.AllowMovementCorruptedChaos")]
    [Description("Description.AllowMovementCorruptedChaos")]
    public bool AllowMovementCorruptedChaos = false;
    [LabelText("LabelText.SkillMovementCorruptedChaosMaxGemCount")]
    public int SkillMovementCorruptedChaosMaxGemCount = 4;
    [LabelText("LabelText.EditIdentitySkill")]
    [Description("Description.EditIdentitySkill")]
    public bool EditIdentitySkill = false;
    [LabelText("LabelText.EditMovementSkill")]
    [Description("Description.EditMovementSkill")]
    public bool EditMovementSkill = false;

    [LabelText("LabelText.GemNoMerge")]
    [Description("Description.GemNoMerge")]
    public bool GemNoMerge = false;

    [LabelText("LabelText.OptimizeUI")]
    public bool OptimizeUI = true;

    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        // 本地化UI
        LocalizationSource.LocalizeUI(parent);
    }

    public override void CopyTo(ModConfig other)
    {
        base.CopyTo(other);
    }
}
