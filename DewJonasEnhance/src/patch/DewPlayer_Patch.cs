using System;
using HarmonyLib;
using UnityEngine;

namespace DewJonasEnhance.patch;

[HarmonyPatch(typeof(DewPlayer))]
public class DewPlayer_Patch
{
    // patch get_shopAddedItems 自定义偏移
    [HarmonyPostfix]
    [HarmonyPatch("get_shopAddedItems")]
    public static void get_shopAddedItems_Postfix(DewPlayer __instance, ref int __result)
    {
        if (!__instance.isServer)
        {
            return;
        }

        
        __result += DewJonasEnhance.Instance.Config.Column - 3;
    }

    // patch set_shopAddedItems 自定义偏移
    [HarmonyPrefix]
    [HarmonyPatch("set_shopAddedItems")]
    public static void set_shopAddedItems_Prefix(DewPlayer __instance, ref int value)
    {
        if (!__instance.isServer)
        {
            return;
        }

        // 在设置时减去偏移值
        value -= DewJonasEnhance.Instance.Config.Column - 3;
    }

    
}