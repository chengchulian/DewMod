using HarmonyLib;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(StarEffect))]
public static class StarEffect_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(StarEffect.IsIncompatibleWith))]
    public static bool IsIncompatibleWith_Prefix(ref bool __result)
    {
        if (DewIdentityChange.Instance?.IsIdentityEnabled != true)
        {
            return true;
        }

        __result = false;
        return false;
    }
}
