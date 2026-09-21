using DewSafeShare.util;
using HarmonyLib;

namespace DewSafeShare.patch;

[HarmonyPatch(typeof(HeroSkill))]
public static class HeroSkillPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(HeroSkill.UnequipSkill))]
    private static void UnequipSkill_Postfix(HeroSkill __instance, SkillTrigger __result)
    {
        SafeShareController.LockDroppedItem(__result, __instance.hero?.owner);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(HeroSkill.StopHoldInHand))]
    private static void StopHoldInHand_Prefix(HeroSkill __instance, out IItem __state)
    {
        __state = __instance.holdingObject;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(HeroSkill.StopHoldInHand))]
    private static void StopHoldInHand_Postfix(HeroSkill __instance, IItem __state)
    {
        SafeShareController.LockDroppedItem(__state, __instance.hero?.owner);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(HeroSkill.UnequipGem))]
    private static void UnequipGem_Postfix(HeroSkill __instance, Gem __result)
    {
        SafeShareController.LockDroppedItem(__result, __instance.hero?.owner);
    }
}
