using System;
using DewMoreChaosChoices.config;
using UnityEngine;

namespace DewMoreChaosChoices;

public sealed class DewMoreChaosChoices : ModBehaviour
{
    public static DewMoreChaosChoices Instance;
    public PluginConfig Config = new PluginConfig();

    // 注册当前配置入口。
    private void Awake()
    {
        Instance = this;
    }

    // 配置由原版加载，奖励继续通过原版 SyncDictionary 同步。
    private void Start()
    {
        instance.isAlteringGameplay = true;
        LocalizationSource.Init(this);
        try
        {
            harmony.PatchAll(typeof(DewMoreChaosChoices).Assembly);
            Debug.Log($"[{mod.metadata.id}] 更多混沌选择已加载：仅房主安装，额外选项由原版同步。");
        }
        catch (Exception exception)
        {
            harmony.UnpatchAll(harmony.Id);
            Debug.LogError($"[{mod.metadata.id}] 混沌补丁加载失败：{exception}");
        }
    }

    // 卸载时移除本 Mod 的服务端奖励生成补丁。
    private void OnDestroy()
    {
        harmony.UnpatchAll(harmony.Id);
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
