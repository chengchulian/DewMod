using UnityEngine;

namespace DewBootcamp.config;

public class PluginConfig : ModConfig
{
    [LabelText("LabelText.SpawnToCreepKey")]
    [Description("Description.SpawnToCreepKey")]
    public KeyCode SpawnToCreepKey = KeyCode.KeypadPlus;
    
    [LabelText("LabelText.SpawnToLocalLey")]
    [Description("Description.SpawnToLocalLey")]
    public KeyCode SpawnToLocalLey = KeyCode.KeypadMinus;

    
    
    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {

        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        // 本地化UI
        LocalizationSource.LocalizeUI(parent);
    }
}