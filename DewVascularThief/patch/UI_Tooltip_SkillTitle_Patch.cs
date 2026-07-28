using DewVascularThief.localization;
using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(UI_Tooltip_SkillTitle))]
internal static class UI_Tooltip_SkillTitle_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnSetup")]
    private static void OnSetup_Postfix(UI_Tooltip_SkillTitle __instance)
    {
        if (__instance.currentObject is not SkillTrigger skill || !VascularThiefSkillMarker.IsMarked(skill))
        {
            return;
        }

        string color = Dew.GetRarityColorHex(skill.rarity);
        __instance.text.text = "<color=" + color + ">" +
                               string.Format(DewLocalization.GetSkillLevelTemplate(skill.level), VascularThiefSkillText.Name) +
                               "</color>";
    }
}
