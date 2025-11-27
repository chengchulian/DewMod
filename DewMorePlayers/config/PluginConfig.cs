using UnityEngine;

namespace DewMorePlayers.config;

public class PluginConfig : ModConfig
{
    [LabelText("LabelText.MaxPlayer")] public int MaxPlayer = 4;


    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        // 本地化UI
        LocalizationSource.LocalizeUI(parent);
        
        onChanged += () =>
        {
            MaxPlayer = Mathf.Clamp(MaxPlayer, Constant.MinPlayerClamp, Constant.MaxPlayerClamp);
        };
    }
}