using ExternalPropertyAttributes;
using UnityEngine;

namespace DewAnyWhereOpenModManager.config;

public class PluginConfig : ModConfig
{
    [Header("Header.Mod.Config")]
    [LabelText("LabelText.OpenKey")]
    [Description("Description.OpenKey")]
    public KeyCode OpenKey = KeyCode.F1;

    
    
    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {

        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        // 本地化UI
        LocalizationSource.LocalizeUI(parent);
    }
}