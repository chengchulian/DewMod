using DewUnLock.config;
using DewUnLock.controller;
using UnityEngine;

namespace DewUnLock.ui;

/// <summary>绘制并管理 F11 打开的解锁工具窗口。</summary>
public sealed class UnlockWindow : MonoBehaviour
{
    private bool showWindow;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F11)) showWindow = !showWindow;
    }

    private void OnGUI()
    {
        if (!showWindow) return;
        GUILayout.BeginArea(new Rect(10f, 10f, 340f, 300f), LocalizationSource.Get("Window.Title"), GUI.skin.window);
        if (GUILayout.Button(LocalizationSource.Get("Button.CompleteAchievements"))) UnlockController.CompleteAllAchievements();
        if (GUILayout.Button(LocalizationSource.Get("Button.DiscoverAll"))) UnlockController.DiscoverAll();
        if (GUILayout.Button(LocalizationSource.Get("Button.UnlockItems"))) UnlockController.UnlockAllItems();
        if (GUILayout.Button(LocalizationSource.Get("Button.SetMastery"))) UnlockController.SetAllMasteryLevels();
        if (GUILayout.Button(LocalizationSource.Get("Button.UnlockDejaVu"))) UnlockController.UnlockDejaVu();
        if (GUILayout.Button(LocalizationSource.Get("Button.AddStardust"))) UnlockController.AddTenThousandStardust();
        if (GUILayout.Button(LocalizationSource.Get("Button.UnlockLimboMax"))) UnlockController.UnlockAllHeroesLimboMaxDepth();
        GUILayout.EndArea();
    }
}
