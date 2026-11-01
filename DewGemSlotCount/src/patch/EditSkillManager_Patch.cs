using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using DewGemSlotCount.ui;

namespace DewGemSlotCount.patch;

[HarmonyPatch]
public static class EditSkillManager_Patch
{
    // 官方手柄遍历以身份技能为末项，扩展到位移后仍使用原版可选择性判断。
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(EditSkillManager), "TryGetNextRelevantSkillLocation");
        yield return AccessTools.Method(typeof(EditSkillManager), "TryGetNextRelevantGemSlot");
    }

    // 两个方法中的 Identity 常量仅用于边界与反向回绕，不改变编辑权限。
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_I4_4)
                instruction.opcode = OpCodes.Ldc_I4_5;
            yield return instruction;
        }
    }
}

// 隐藏位移时维持官方五技能的可选范围；显示时继续由原版判断编辑权限。
[HarmonyPatch(typeof(EditSkillManager))]
public static class EditSkillManager_Selectable_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(EditSkillManager.IsSlotSelectable), typeof(HeroSkillLocation))]
    public static bool SkillPrefix(EditSkillManager __instance, HeroSkillLocation skillLoc, ref bool __result)
    {
        if (DewGemSlotCount.Instance == null) return true;
        if (skillLoc != HeroSkillLocation.Movement || MovementEditLayout.ShouldShowMovement(__instance.mode))
            return true;
        __result = false;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(EditSkillManager.IsSlotSelectable), typeof(GemLocation))]
    public static bool GemPrefix(EditSkillManager __instance, GemLocation gemLoc, ref bool __result)
    {
        if (DewGemSlotCount.Instance == null) return true;
        if (gemLoc.skill != HeroSkillLocation.Movement || MovementEditLayout.ShouldShowMovement(__instance.mode))
            return true;
        __result = false;
        return false;
    }
}
