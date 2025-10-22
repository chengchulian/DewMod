using DewDifficultyCustomizeMod.controller;
using DewDifficultyCustomizeMod.util;
using UnityEngine;
using UnityEngine.Serialization;

namespace DewDifficultyCustomizeMod;

public class DewDifficultyCustomizeMod : ModBehaviour
{
    public static DewDifficultyCustomizeMod Instance;

    public UIStateController uiStateController;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        harmony.PatchAll();
        uiStateController = gameObject.AddComponent<UIStateController>();
        Debug.Log($"[{mod.metadata.name} v{mod.metadata.modVer}] 已加载 by {mod.metadata.author}");
    }

    private void OnDestroy()
    {
        
        // 卸载 Harmony 补丁
        harmony.UnpatchAll(harmony.Id);
        GameManagerUtil.UnLoadThisModBehavior();
        // 清理 UI 控制器
        if (uiStateController != null)
        {
            Destroy(uiStateController);
            uiStateController = null;
        }
        

        
        Debug.Log($"[{mod.metadata.name} v{mod.metadata.modVer}] 已卸载");
    }
}