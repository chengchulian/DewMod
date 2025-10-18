using System;
using DewDifficultyCustomizeMod.config;
using DewDifficultyCustomizeMod.util;
using HarmonyLib;

namespace DewDifficultyCustomizeMod.patch;

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

        __result += Math.Clamp(AttrCustomizeResources.Config.shopItems - 3, 0, 27);
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

        // 在设置时减去偏移值（比如5）
        value -= Math.Clamp(AttrCustomizeResources.Config.shopItems - 3, 0, 27);
    }


    [HarmonyPostfix]
    [HarmonyPatch("UserCode_CmdNotifyMidJoinReady__String")]
    public static void UserCode_CmdNotifyMidJoinReady__String_Postfix(DewPlayer __instance)
    {
        if (!__instance.isServer)
        {
            return;
        }

        GameManagerUtil.SendSynchronizeClient();
    }
}