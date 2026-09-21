using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(Actor))]
internal static class VascularThiefActorPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("PrepareAndSpawn")]
    private static void PrepareAndSpawn_Prefix(Actor __instance)
    {
        if (__instance is St_U_VascularThief && !__instance.gameObject.activeSelf)
        {
            __instance.gameObject.SetActive(true);
        }
    }
}
