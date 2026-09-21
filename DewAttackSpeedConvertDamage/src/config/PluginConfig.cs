using UnityEngine;

namespace DewAttackSpeedConvertDamage.config;

public class PluginConfig : ModConfig
{

    // 攻速上限
    [LabelText("LabelText.MaxAttackSpeed")]
    [Description("Description.MaxAttackSpeed")]
    public float MaxAttackSpeed = 5;
    // 每溢出 100% 攻速 → + x%伤害倍率
    [LabelText("LabelText.DamagePerOverflow")]
    [Description("Description.DamagePerOverflow")]
    public float DamagePerOverflow = 0.01f; 


    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        // 本地化UI
        LocalizationSource.LocalizeUI(parent);
    }
    
}