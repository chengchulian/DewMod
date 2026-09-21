using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(SkillTrigger))]
internal static class SkillTriggerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(SkillTrigger.OnCastComplete))]
    private static void OnCastComplete_Postfix(SkillTrigger __instance, CastInfo info)
    {
        DewVascularThief.Instance?.Controller.HandleCastComplete(__instance, info);
    }
}
