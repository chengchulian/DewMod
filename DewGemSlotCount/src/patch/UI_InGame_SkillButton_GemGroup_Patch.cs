using DewGemSlotCount.ui;
using HarmonyLib;
using UnityEngine;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(UI_InGame_SkillButton_GemGroup))]
public static class UI_InGame_SkillButton_GemGroup_Patch
{
    // 首次刷新槽数前准备所有容量，沿用原版动画和交互。
    [HarmonyPostfix]
    [HarmonyPatch("Awake")]
    public static void Awake_Postfix(UI_InGame_SkillButton_GemGroup __instance)
    {
        GemSlotLayout.EnsureHud(__instance);
    }

    // 本地切换优化选项时刷新布局，无需重新进房。
    [HarmonyPrefix]
    [HarmonyPatch(nameof(UI_InGame_SkillButton_GemGroup.LogicUpdate))]
    public static void LogicUpdate_Prefix(UI_InGame_SkillButton_GemGroup __instance)
    {
        GemSlotLayout.EnsureHud(__instance);
        GemSlotLayout.UpdateLayout(__instance.groups, 1);
    }
}
