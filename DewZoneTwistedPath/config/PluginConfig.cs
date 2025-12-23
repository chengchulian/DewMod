using DewZoneReorder.config;
using DewZoneTwistedPath.enums;
using TMPro;
using UnityEngine;

namespace DewZoneTwistedPath.config;

public class PluginConfig : ModConfig
{

    [LabelText("LabelText.zone1")]
    public ZoneName zone1 = ZoneName.Zone_None;
    [LabelText("LabelText.zone2")]
    public ZoneName zone2 = ZoneName.Zone_None;
    [LabelText("LabelText.zone3")]
    public ZoneName zone3 = ZoneName.Zone_None;
    [LabelText("LabelText.zone4")]
    public ZoneName zone4 = ZoneName.Zone_None;
   
    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        // 本地化UI
        LocalizationSource.LocalizeUI(parent);
        //本地化 区域名称
        LocalizationZoneName(parent);
    }


    private void LocalizationZoneName(Transform root)
    {
        foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            bool changed = false;

            foreach (var option in dropdown.options)
            {
                string key = option.text;
                
                // 枚举原始名通常是 Zone_xxx
                if (!key.StartsWith("Zone_"))
                {
                    continue;
                }

                if (key == "Zone_None")
                {
                    var localizationText = LocalizationSource.GetLocalizationText(key);
                    option.text = localizationText;
                    changed = true;
                    continue;
                }

                if (DewLocalization.TryGetUIValue(key + "_Name", out var localized))
                {
                    option.text = localized;
                    changed = true;
                }
            }

            if (changed)
            {
                dropdown.RefreshShownValue();
            }
        }
    }


    public override void CopyTo(ModConfig other)
    {
        base.CopyTo(other);
    }
}
