using HarmonyLib;
using UnityEngine;

namespace DewGemSlotCount.patch;

[HarmonyPatch(typeof(HeroSkill), nameof(HeroSkill.GetMaxGemCount))]
public class HeroSkill_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(HeroSkill.GetMaxGemCount))]
    public static bool GetMaxGemCount_Prefix(HeroSkill __instance, HeroSkillLocation type, ref int __result)
    {
        
        __result = type switch
        {
            HeroSkillLocation.Q => DewGemSlotCount.Instance.Config.SkillQGemCount,
            HeroSkillLocation.W => DewGemSlotCount.Instance.Config.SkillWGemCount,
            HeroSkillLocation.E => DewGemSlotCount.Instance.Config.SkillEGemCount,
            HeroSkillLocation.R => DewGemSlotCount.Instance.Config.SkillRGemCount,
            HeroSkillLocation.Identity => DewGemSlotCount.Instance.Config.SkillIdentityGemCount,
            HeroSkillLocation.Movement => DewGemSlotCount.Instance.Config.SkillMovementGemCount,
            _ => 0
        };

        __result = Mathf.Clamp(__result, Constant.MinGemCount, Constant.MaxGemCount);
        // 跳过原方法
        return false; 
    }

}