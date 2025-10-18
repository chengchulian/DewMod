using DewDifficultyCustomizeMod.config;
using DewDifficultyCustomizeMod.util;
using HarmonyLib;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;
[HarmonyPatch(typeof(LobbyServiceProvider))]
public class LobbyServiceProvider_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(LobbyServiceProvider.GetInitialAttr_maxPlayers))]
    public static bool GetInitialAttr_maxPlayers_Prefix(ref int __result)
    {
        __result = Mathf.Clamp(AttrCustomizeResources.Config.maxPlayer,AttrCustomizeConstant.MinPlayerClamp,AttrCustomizeConstant.MaxPlayerClamp);
        return false;
    }
}