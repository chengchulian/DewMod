using System;
using System.Reflection;
using DewModConfigListSupport.config;
using UnityEngine;

namespace DewModConfigListSupport;

public class DewModConfigListSupport : ModBehaviour
{
    public static DewModConfigListSupport Instance;
    public readonly PluginConfig Config = new PluginConfig();

    public void Awake()
    {
        Instance = this;
        // 在注册配置控件前加载本 Mod 文本。
        LocalizationSource.Init(this);
    }
    
    public void Start()
    {
        ListSupportHelper.InitListSupport();
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
    }

    public void OnDestroy()
    {
    }
}