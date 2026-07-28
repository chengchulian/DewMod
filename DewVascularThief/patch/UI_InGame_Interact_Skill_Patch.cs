using DewVascularThief.localization;
using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(UI_InGame_Interact_Skill))]
internal static class UI_InGame_Interact_Skill_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnActivate")]
    private static void OnActivate_Postfix(UI_InGame_Interact_Skill __instance)
    {
        if (__instance.interactable is not SkillTrigger skill || !VascularThiefSkillMarker.IsMarked(skill))
        {
            return;
        }

        __instance.nameText.text = string.Format(DewLocalization.GetSkillLevelTemplate(skill.level), VascularThiefSkillText.Name);
        __instance.nameText.color = Dew.GetRarityColor(skill.rarity);
        __instance.shortText.text = VascularThiefSkillText.ShortDescription;
        __instance.hasShortDescObject.SetActive(true);
    }
}
