using System;
using DewDifficultyCustomizeMod.config;
using HarmonyLib;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(ZoneManager))]
public class ZoneManager_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("GenerateWorld_Imp")]
    public static void GenerateWorld_Imp_Prefix(ZoneManager __instance)
    {
        if (!NetworkedManagerBase<GameManager>.instance.isServer)
        {
            return;
        }

        if (AttrCustomizeResources.Config.numOfNodes > 0)
        {
            var value = Math.Max(AttrCustomizeResources.Config.numOfNodes,
                AttrCustomizeResources.Config.numOfMerchants + 2);
            value = Math.Max(value, 2);

            __instance.currentZone.numOfNodes = new Vector2Int(value, value);
        }

        if (AttrCustomizeResources.Config.numOfMerchants > 0)
        {
            var value = Math.Max(AttrCustomizeResources.Config.numOfMerchants, 0);

            __instance.currentZone.numOfMerchants = new Vector2Int(value, value);
        }

        DewBuildProfile.current.worldNodeCountOffset = 0;
    }
}