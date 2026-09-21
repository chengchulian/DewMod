using DewIdentityChange.config;
using HarmonyLib;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(LootManager))]
public static class LootManager_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(LootManager.OnStartServer))]
    public static void OnStartServer_Postfix(LootManager __instance)
    {
        if (DewIdentityChange.Instance?.IsCharacterSkillLootEnabled == true)
        {
            CharacterSkillLootPool.AddTo(__instance);
        }
    }
}
