using DewDifficultyCustomizeMod.config;
using DewDifficultyCustomizeMod.util;
using HarmonyLib;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(GameManager))]
public class GameManager_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameManager.OnStartServer))]
    public static void OnStartServer_Postfix(GameManager __instance)
    {
        if (!__instance.isServer)
        {
            return;
        }
        GameManagerUtil.LoadThisModBehavior();
    }

    [HarmonyPostfix]
    [HarmonyPatch("get_maxAndSpawnedPopulationMultiplier")]
    public static void get_maxAndSpawnedPopulationMultiplier_Postfix(GameManager __instance, ref float __result)
    {
        if (!__instance.isServer)
        {
            return;
        }

        if (__result > 1)
        {
            __result = AttrCustomizeResources.Config.maxAndSpawnedPopulationMultiplier;
        }
    }
}