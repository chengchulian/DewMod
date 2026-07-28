using UnityEngine;

namespace DewSuperSmart.config;

public enum AutoDodgeThreatLevel
{
    Red,
    Yellow,
    Green
}

public class PluginConfig : ModConfig
{
    [Header("Header.Display")]
    [LabelText("LabelText.ShowAttackRange")]
    [Description("Description.ShowAttackRange")]
    public bool ShowAttackRange = true;

    [LabelText("LabelText.ShowQRange")]
    [Description("Description.ShowQRange")]
    public bool ShowQRange = true;

    [LabelText("LabelText.ShowWRange")]
    [Description("Description.ShowWRange")]
    public bool ShowWRange = true;

    [LabelText("LabelText.ShowERange")]
    [Description("Description.ShowERange")]
    public bool ShowERange = true;

    [LabelText("LabelText.ShowRRange")]
    [Description("Description.ShowRRange")]
    public bool ShowRRange = true;

    [LabelText("LabelText.ShowMovementRange")]
    [Description("Description.ShowMovementRange")]
    public bool ShowMovementRange = true;

    [LabelText("LabelText.ShowIdentityRange")]
    [Description("Description.ShowIdentityRange")]
    public bool ShowIdentityRange = true;

    [LabelText("LabelText.ShowMonsterThreatRanges")]
    [Description("Description.ShowMonsterThreatRanges")]
    public bool ShowMonsterThreatRanges = true;

    [LabelText("LabelText.ShowProjectileThreatRanges")]
    [Description("Description.ShowProjectileThreatRanges")]
    public bool ShowProjectileThreatRanges = true;

    [Header("Header.AutoDodge")]
    [LabelText("LabelText.EnableAutoDodge")]
    [Description("Description.EnableAutoDodge")]
    public bool EnableAutoDodge = true;

    [LabelText("LabelText.AutoDodgeUseMovementSkill")]
    [Description("Description.AutoDodgeUseMovementSkill")]
    public bool AutoDodgeUseMovementSkill = true;

    [LabelText("LabelText.AutoDodgeLevel")]
    [Description("Description.AutoDodgeLevel")]
    public AutoDodgeThreatLevel AutoDodgeLevel = AutoDodgeThreatLevel.Green;

    [LabelText("LabelText.AutoDodgeCommandInterval")]
    [Description("Description.AutoDodgeCommandInterval")]
    public float AutoDodgeCommandInterval = 0.1f;

    [LabelText("LabelText.AutoDodgeKey")]
    [Description("Description.AutoDodgeKey")]
    public KeyCode AutoDodgeKey = KeyCode.LeftShift;

    internal float AutoDodgeHoldDelay => 0.12f;
    internal bool AutoDodgeMoveFallback => true;
    internal bool AutoDodgeReadyThreatsOnly => true;
    internal float AutoDodgeRiskThreshold => 0.9f;
    internal float ThreatScanRange => 24f;
    internal float AutoDodgeSearchRadius => 7f;
    internal float AutoDodgeFallbackDistance => 4.5f;
    internal float ThreatPadding => 0.35f;
    internal float ThreatPredictionStrength => 0.65f;
    internal bool DrawUnknownSourceThreats => true;
    internal bool DrawReadyThreatsOnly => false;
    internal float ProjectileLookAheadDistance => 12f;
    internal float DefaultThreatLineWidth => 1.1f;
    internal float DefaultThreatAreaRadius => 1.2f;
    internal int MaxThreatRenderers => 48;

    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        LocalizationSource.LocalizeUI(parent);
    }
}
