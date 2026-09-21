using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(UI_Collectables_MainView))]
internal static class UI_Collectables_MainView_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("RefreshObjectList")]
    private static void RefreshObjectList_Prefix()
    {
        RegisterCollectableSkill();
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(UI_Collectables_MainView.RefreshLight))]
    private static void RefreshLight_Prefix()
    {
        RegisterCollectableSkill();
    }

    private static void RegisterCollectableSkill()
    {
        VascularThiefProfileRegistry.RegisterContentSettings(DewBuildProfile.current?.content);
        VascularThiefCollectablesRegistry.Register();
        VascularThiefProfileRegistry.RegisterProfile(DewSave.profileMain);
        VascularThiefProfileRegistry.RegisterProfileStats(DewSave.profileStats);
    }
}
