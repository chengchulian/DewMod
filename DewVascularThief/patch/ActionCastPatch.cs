using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(ActionCast))]
internal static class ActionCastPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(ActionCast.Tick))]
    private static bool Tick_Prefix(ActionCast __instance, ref bool __result)
    {
        if (__instance?.trigger is not SkillTrigger skill || !VascularThiefSkillMarker.IsMarked(skill))
        {
            return true;
        }

        if (DewVascularThief.Instance?.Controller.CanCastVascularThiefNow(skill, __instance.info) == true)
        {
            return true;
        }

        __result = true;
        return false;
    }
}
