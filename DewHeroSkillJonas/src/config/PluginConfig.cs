using UnityEngine;
using UnityEngine.UI;

namespace DewHeroSkillJonas.config;

public class PluginConfig : ModConfig
{
    [LabelText("LabelText.Enable")]
    public bool Enable = true;
    
    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        LocalizationSource.LocalizeUI(parent);
    }
}