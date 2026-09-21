using DewJonasEnhance.util;
using UnityEngine;

namespace DewJonasEnhance.config;

public class PluginConfig : ModConfig
{
    [LabelText("LabelText.AddPlatinumCoin")]
    public int AddPlatinumCoin = 0;

    [LabelText("LabelText.Invincible")]
    public bool Invincible = false;

    [LabelText("LabelText.ReEnterRoomRefresh")]
    public bool ReEnterRoomRefresh = false;

    [LabelText("LabelText.Column")]
    public int Column = 3;


    public override void BuildWidgets(Transform parent, out SafeAction onChanged, out SafeAction requestUpdate)
    {
        base.BuildWidgets(parent, out onChanged, out requestUpdate);
        // 本地化UI
        LocalizationSource.LocalizeUI(parent);
    }

    public override void CopyTo(ModConfig other)
    {
       
        AddPlatinumCoin = Mathf.Clamp(AddPlatinumCoin, Constant.MinAddPlatinumCoin, Constant.MaxAddPlatinumCoin);

        Column = Mathf.Clamp(Column, Constant.MinColumn, Constant.MaxColumn);

        base.CopyTo(other);
    }
}