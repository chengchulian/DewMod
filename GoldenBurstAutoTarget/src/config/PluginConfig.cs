using UnityEngine;

namespace GoldenBurstAutoTarget.config;

public class PluginConfig : ModConfig
{
    [Header("Header.Input")]
    [LabelText("LabelText.CastKey")]
    [Description("Description.CastKey")]
    public KeyCode CastKey = KeyCode.A;

    [LabelText("LabelText.HoldToCast")]
    [Description("Description.HoldToCast")]
    public bool HoldToCast = true;

    [Header("Header.Targets")]
    [LabelText("LabelText.TargetAllies")]
    [Description("Description.TargetAllies")]
    public bool TargetAllies;

    [LabelText("LabelText.RequireFriendlyTeammates")]
    [Description("Description.RequireFriendlyTeammates")]
    public bool RequireFriendlyTeammates = true;

    [LabelText("LabelText.TargetMonsters")]
    [Description("Description.TargetMonsters")]
    public bool TargetMonsters = true;

    [LabelText("LabelText.TargetBosses")]
    [Description("Description.TargetBosses")]
    public bool TargetBosses = true;

    [LabelText("LabelText.SkipDamageImmuneMonsters")]
    [Description("Description.SkipDamageImmuneMonsters")]
    public bool SkipDamageImmuneMonsters = true;

    [LabelText("LabelText.TargetStones")]
    [Description("Description.TargetStones")]
    public bool TargetStones = true;

    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        LocalizationSource.LocalizeUI(parent);
    }
}
