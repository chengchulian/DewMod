using System.Collections.Generic;
using DewModConfigListSupport.attribute;
using UnityEngine;

namespace DewModConfigListSupport.config;

public class PluginConfig : ModConfig
{
    [LabelText("LabelText.TestValueList")]
    [Description("Description.TestValueList")]
    [Values(typeof(ConfigValues), nameof(ConfigValues.GetTestValues))]
    public List<string> TestValueList = new();
    
    [LabelText("LabelText.TestInputList")]
    [Description("Description.TestInputList")]
    public List<string> TestInputList = new();
    

    // 基础控件生成后统一替换配置项的本地化文本。
    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        LocalizationSource.LocalizeUI(parent);
    }
}
