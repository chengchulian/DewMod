using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DewSuperSmart;

internal static class SafeZoneRpcPatch
{
    // 按原有可选类型名定位 RPC，缺失时继续使用反射回退路径。
    public static void Install(Harmony harmony)
    {
        Type type = AccessTools.TypeByName("Ai_Mon_Ink_BossWhiteNight_Cataclysm_SafeZone");
        MethodInfo method = type != null
            ? AccessTools.Method(type, "UserCode_RpcPlayShieldEffect__Vector3__Single")
            : null;
        MethodInfo postfix = typeof(SafeZoneRpcPatch).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null || postfix == null)
        {
            Debug.LogWarning("[DewSuperSmart] 未找到安全区 RPC，将使用反射回退获取远端安全区中心。");
            return;
        }

        try
        {
            harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[DewSuperSmart] 安全区 RPC 补丁安装失败，将使用反射回退。{exception.Message}");
        }
    }

    // 补丁只转发坐标，中心点缓存和去重由控制层负责。
    private static void Postfix(object __instance, Vector3 pos)
    {
        SafeZoneTracker.RecordCenter(__instance, pos);
    }
}
