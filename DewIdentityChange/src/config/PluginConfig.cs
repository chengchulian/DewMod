using UnityEngine;

namespace DewIdentityChange.config;

public class PluginConfig : ModConfig
{
    [LabelText("LabelText.Enable")]
    public bool enable = true;

    [LabelText("LabelText.AddCharacterSkillsToLoot")]
    public bool addCharacterSkillsToLoot = true;

    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        LocalizationSource.LocalizeUI(parent);
    }

}
