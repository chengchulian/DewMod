using UnityEngine;

namespace DewLimboConfinedAbolish.config;

public sealed class PluginConfig : ModConfig
{
    [LabelText("LabelText.AllowObliteration")]
    [Description("Description.AllowObliteration")]
    public bool AllowObliteration = true;

    [LabelText("LabelText.AllowDejavu")]
    [Description("Description.AllowDejavu")]
    public bool AllowDejavu = true;

    [LabelText("LabelText.AllowAllLucidDreams")]
    [Description("Description.AllowAllLucidDreams")]
    public bool AllowAllLucidDreams = true;

    [LabelText("LabelText.AllowMidGameJoin")]
    [Description("Description.AllowMidGameJoin")]
    public bool AllowMidGameJoin = true;

    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        LocalizationSource.LocalizeUI(parent);
    }
}
