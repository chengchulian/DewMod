using DewVascularThief.util;
using HarmonyLib;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(LootManager))]
internal static class VascularThiefLootPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(LootManager.OnStartServer))]
    private static void OnStartServer_Postfix(LootManager __instance)
    {
        VascularThiefLootRegistry.Register(__instance);
    }
}
