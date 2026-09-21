using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(HeroSkill))]
internal static class HeroSkillPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(HeroSkill.UnequipSkill))]
    private static void UnequipSkill_Postfix(SkillTrigger __result)
    {
        if (__result != null && VascularThiefSkillMarker.IsMarked(__result))
        {
            DewVascularThief.Instance?.Controller.RemoveStolenAbility(__result);
        }
    }
}
