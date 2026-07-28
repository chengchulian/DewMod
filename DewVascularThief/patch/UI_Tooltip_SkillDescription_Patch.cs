using DewVascularThief.localization;
using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(UI_Tooltip_SkillDescription))]
internal static class UI_Tooltip_SkillDescription_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnSetup")]
    private static void OnSetup_Postfix(UI_Tooltip_SkillDescription __instance)
    {
        if (__instance.currentObject is not SkillTrigger skill || !VascularThiefSkillMarker.IsMarked(skill))
        {
            return;
        }

        __instance.text.text = DewVascularThief.Instance?.Controller.GetDescription(skill) ?? VascularThiefSkillText.GetDescription(100);
    }
}
