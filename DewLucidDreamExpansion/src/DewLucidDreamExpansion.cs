using DewLucidDreamExpansion.config;
using DewLucidDreamExpansion.controller;
using DewLucidDreamExpansion.ui;
using UnityEngine;

namespace DewLucidDreamExpansion;

public sealed class DewLucidDreamExpansion : ModBehaviour
{
    private bool initialized;

    // Mod 加载器在 AddComponent 之后设置 instance，因此在 Start 访问 Mod 元数据。
    private void Start()
    {
        instance.isAlteringGameplay = true;
        LocalizationSource.Init(this);
        harmony.PatchAll(typeof(DewLucidDreamExpansion).Assembly);
        DreamRegistry.Register();
        initialized = true;
        LimboDepthSelector.RefreshExisting();
        Debug.Log("[LucidDreamExpansion] 已加载六个清醒梦，迷失域最高深度为 9。");
    }

    private void OnDestroy()
    {
        if (!initialized) return;
        harmony.UnpatchAll(harmony.Id);
        LimboDepthSelector.Clear();
        DreamRegistry.Unregister();
    }
}
