using DewUnLock.config;
using DewUnLock.ui;
using UnityEngine;

namespace DewUnLock;

/// <summary>模组入口，负责生命周期和自有 UI 的创建。</summary>
public sealed class DewUnLock : ModBehaviour
{
    private UnlockWindow window;

    private void Awake()
    {
        LocalizationSource.Init(this);
        harmony.PatchAll();
        window = gameObject.AddComponent<UnlockWindow>();
    }

    private void Start() => Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");

    private void OnDestroy()
    {
        if (window != null) Destroy(window);
        harmony.UnpatchAll(harmony.Id);
        Debug.Log($"[{mod.metadata.id}] 已卸载");
    }
}
