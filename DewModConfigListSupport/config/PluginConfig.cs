using System.Collections.Generic;
using DewModConfigListSupport.attribute;
using UnityEngine.UI;

namespace DewModConfigListSupport.config;

public class PluginConfig : ModConfig
{
    [Values(typeof(ConfigValues), nameof(ConfigValues.GetTestValues))]
    public List<string> TestValueList = new();
    
    public List<string> TestInputList = new();
    
}
