using DewGemSlotCount.ui;
using HarmonyLib;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(UI_InGame_Scoreboard_PlayerItem_Skill))]
public static class UI_InGame_Scoreboard_PlayerItem_Skill_Patch
{
    // 保留原版图标、充能、按键及提示框刷新，只扩展容量。
    [HarmonyPrefix]
    [HarmonyPatch("UpdateInfo")]
    public static void UpdateInfo_Prefix(UI_InGame_Scoreboard_PlayerItem_Skill __instance)
    {
        GemSlotLayout.Extend<UI_InGame_Scoreboard_PlayerItem_Skill_Gem>(ref __instance.gemObjects234,
            2, __instance.skillIcon.transform, slot => slot.index, (slot, index) => slot.index = index);
    }

    // 原版数组从两槽开始，补齐身份和移动技能可能使用的单槽。
    [HarmonyPostfix]
    [HarmonyPatch("UpdateInfo")]
    public static void UpdateInfo_Postfix(UI_InGame_Scoreboard_PlayerItem_Skill __instance)
    {
        Hero hero = __instance.hero;
        if (hero == null || !hero.isActive) return;
        GemSlotLayout.ShowSingle<UI_InGame_Scoreboard_PlayerItem_Skill_Gem>(__instance.gemObjects234,
            __instance.skillIcon.transform, hero.Skill.GetMaxGemCount(__instance.type) == 1,
            slot => slot.index, (slot, index) => slot.index = index);
    }
}
