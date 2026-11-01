using UnityEngine;

namespace DewBuildAssistant.config;

public sealed class PluginConfig : ModConfig
{
    // 复用游戏配置控件，生成后统一翻译标签。
    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        LocalizationSource.LocalizeUI(parent);
    }
}
