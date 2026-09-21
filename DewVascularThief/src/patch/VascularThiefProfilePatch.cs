using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

internal static class VascularThiefProfilePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewProfile), nameof(DewProfile.Validate))]
    private static void DewProfile_Validate_Postfix(DewProfile __instance)
    {
        VascularThiefCollectablesRegistry.Register();
        VascularThiefProfileRegistry.RegisterProfile(__instance);
        VascularThiefProfileRegistry.RegisterProfileStats(DewSave.profileStats);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewGameContentSettings), nameof(DewGameContentSettings.Init))]
    private static void ContentSettings_Init_Postfix(DewGameContentSettings __instance)
    {
        if (__instance == DewBuildProfile.current?.content)
        {
            VascularThiefProfileRegistry.RegisterContentSettings(__instance);
        }
    }
}
