using DewVascularThief.localization;
using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(UI_Tooltip_ObjMemory))]
internal static class UI_Tooltip_ObjMemory_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnSetup")]
    private static void OnSetup_Postfix(UI_Tooltip_ObjMemory __instance)
    {
        if (__instance.currentObject is SkillTrigger skill && VascularThiefSkillMarker.IsMarked(skill))
        {
            __instance.text.text = VascularThiefSkillText.Memory;
            __instance.text.maxVisibleCharacters = int.MaxValue;
        }
    }
}
