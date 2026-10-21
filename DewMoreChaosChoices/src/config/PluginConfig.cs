using UnityEngine;

namespace DewMoreChaosChoices.config;

public sealed class PluginConfig : ModConfig
{
    [Header("Header.Choices")]
    [LabelText("LabelText.ExtraCommonChaosChoices")]
    [Description("Description.ExtraCommonChaosChoices")]
    // 沿用旧配置键，将已有的普通混沌设置作为普通品质的配置。
    public int ExtraChaosChoices = 3;

    [LabelText("LabelText.ExtraRareChaosChoices")]
    [Description("Description.ExtraRareChaosChoices")]
    public int ExtraRareChaosChoices = 3;

    [LabelText("LabelText.ExtraEpicChaosChoices")]
    [Description("Description.ExtraEpicChaosChoices")]
    public int ExtraEpicChaosChoices = 3;

    [LabelText("LabelText.ExtraLegendaryChaosChoices")]
    [Description("Description.ExtraLegendaryChaosChoices")]
    public int ExtraLegendaryChaosChoices = 3;

    [LabelText("LabelText.ExtraCorruptedChaosChoices")]
    [Description("Description.ExtraCorruptedChaosChoices")]
    public int ExtraCorruptedChaosChoices = 3;

    // 按祭坛实际品质选择增量；原版没有奖励池的特殊品质保持原版数量。
    public int GetExtraChaosChoices(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Common => ExtraChaosChoices,
            Rarity.Rare => ExtraRareChaosChoices,
            Rarity.Epic => ExtraEpicChaosChoices,
            Rarity.Legendary => ExtraLegendaryChaosChoices,
            _ => 0
        };
    }

    // 沿用原版数字输入控件，并翻译配置标签及说明。
    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        LocalizationSource.LocalizeUI(parent);
    }
}
