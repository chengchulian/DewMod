using UnityEngine;

namespace DewSafeShare.config;

public class PluginConfig : ModConfig
{
    [LabelText("LabelText.VisibleSecondsAfterPing")]
    [Description("Description.VisibleSecondsAfterPing")]
    public float VisibleSecondsAfterPing = 10f;

    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        LocalizationSource.LocalizeUI(parent);
    }
}
