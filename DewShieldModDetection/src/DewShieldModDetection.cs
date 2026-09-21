using DewShieldModDetection.config;
using UnityEngine;

namespace DewShieldModDetection;

public sealed class DewShieldModDetection : ModBehaviour
{
    // 初始化本地化并安装本 Mod 的补丁。
    private void Start()
    {
        LocalizationSource.Init(this);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] {LocalizationSource.GetLocalizationText("Log.Loaded")}");
    }

    // 卸载时仅撤销本 Mod 持有的补丁。
    private void OnDestroy()
    {
        harmony.UnpatchAll(harmony.Id);
    }
}
