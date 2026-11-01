using DewGemSlotCount.ui;
using HarmonyLib;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(UI_InGame_SkillButtons))]
public static class UI_InGame_SkillButtons_Patch
{
    // 原版继续处理缩放、淡出和焦点，仅补充位移技能的编辑布局。
    [HarmonyPostfix]
    [HarmonyPatch("OnStateChanged")]
    public static void OnStateChanged_Postfix(UI_InGame_SkillButtons __instance, EditSkillManager.ModeType mode)
    {
        MovementEditLayout.Apply(__instance, mode);
    }

    // 由 HUD 专用 LateUpdate 在官方插值和缩放后更新布局，也支持中途启用 Mod。
    [HarmonyPostfix]
    [HarmonyPatch(nameof(UI_InGame_SkillButtons.FrameUpdate))]
    public static void FrameUpdate_Postfix(UI_InGame_SkillButtons __instance)
    {
        SkillHudLayout.Ensure(__instance);
    }

    // 在原版对象销毁前恢复本 Mod 调整过的布局。
    [HarmonyPrefix]
    [HarmonyPatch("OnDestroy")]
    public static void OnDestroy_Prefix(UI_InGame_SkillButtons __instance)
    {
        MovementEditLayout.Restore(__instance);
    }
}
