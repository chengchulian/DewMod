using DewJonasEnhance.util;
using HarmonyLib;

namespace DewJonasEnhance.patch;

[HarmonyPatch(typeof(EveryZoneStarEffect))]
public class EveryZoneStarEffect_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnCreate")]
    public static void OnCreate_Postfix(EveryZoneStarEffect __instance)
    {
        if (!__instance.isServer)
        {
            return;
        }
        DewJonasEnhanceUtil.ZoneManagerOnStart();

    }
    
}