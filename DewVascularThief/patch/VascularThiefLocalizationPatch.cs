using System.Collections.Generic;
using DewInternal;
using DewVascularThief.config;
using DewVascularThief.localization;
using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(DewLocalization))]
internal static class VascularThiefLocalizationPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(DewLocalization.GetSkillName), typeof(string), typeof(int))]
    private static void GetSkillName_Postfix(string key, ref string __result)
    {
        if (key == VascularThiefText.SkillKey)
        {
            __result = VascularThiefSkillText.Name;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DewLocalization.GetSkillShortDesc))]
    private static void GetSkillShortDesc_Postfix(string key, ref string __result)
    {
        if (key == VascularThiefText.SkillKey)
        {
            __result = VascularThiefSkillText.ShortDescription;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DewLocalization.GetSkillDescription))]
    private static void GetSkillDescription_Postfix(string key, ref List<LocaleNode> __result)
    {
        if (key == VascularThiefText.SkillKey)
        {
            int damagePercent = VascularThiefUpgradeScaling.GetDamagePercent(1);
            __result = new List<LocaleNode>
            {
                new LocaleNode
                {
                    type = LocaleNodeType.Text,
                    textData = VascularThiefSkillText.GetDescription(damagePercent)
                }
            };
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DewLocalization.GetSkillMemory))]
    private static void GetSkillMemory_Postfix(string key, ref string __result)
    {
        if (key == VascularThiefText.SkillKey)
        {
            __result = VascularThiefSkillText.Memory;
        }
    }
}
