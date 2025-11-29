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