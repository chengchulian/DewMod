using System.Collections.Generic;
using DewMoreVision.config;
using HarmonyLib;
using UnityEngine;

namespace DewMoreVision.patch;

[HarmonyPatch(typeof(CameraManager))]
public class CameraManager_Patch
{
    // [HarmonyPrefix]
    // [HarmonyPatch("SetZoomLevel")]
    // public static bool SetZoomLevel_Prefix(CameraManager __instance, int level)
    // { 
    //     // 使用反射访问私有属性
    //     var propertyInfo = AccessTools.Property(typeof(CameraManager), "currentZoomIndex");
    //     if (propertyInfo != null)
    //     {
    //         propertyInfo.SetValue(__instance, Mathf.Clamp(level, -(__instance.zoomSteps / 3), __instance.zoomSteps - 1));
    //     }
    //     
    //     // 返回 false 表示跳过原方法执行
    //     return false;
    // }

    [HarmonyPostfix]
    [HarmonyPatch("Awake")]
    public static void Awake_Postfix(CameraManager __instance)
    {
        __instance.zoomSteps = DewMoreVision.Instance.Config.ZoomSteps;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetFollowOffsetTarget")]
    public static bool GetFollowOffsetTarget_Prefix(CameraManager __instance, ref Vector3 __result)
    {
        var cfg = DewMoreVision.Instance.Config;

        float zoomIndex = __instance.currentZoomIndex;

        // 使用 AccessTools 获取私有字段 _cameraModifiers 的值
        var cameraModifiersField = AccessTools.Field(typeof(CameraManager), "_cameraModifiers");
        var cameraModifiers = cameraModifiersField.GetValue(__instance) as List<CameraModifierBase>;

        // 寻找动态修改器(保持原有行为)
        if (!__instance.isSpectating && cameraModifiers != null)
        {
            foreach (var cameraModifier in cameraModifiers)
            {
                if (cameraModifier is CameraModifierZoom zoom)
                {
                    zoomIndex = zoom.zoomIndex;
                }
            }
        }

        var steps = __instance.zoomSteps - 1;

        zoomIndex = Mathf.Lerp(
            -(cfg.ZoomMultiple - 1) * steps,
            steps,
            zoomIndex / steps
        );

        // 原插值逻辑
        float val = zoomIndex / steps;

        Vector3 target = val < 0.5f
            ? Vector3.LerpUnclamped(__instance.farZoomBody, __instance.midZoombody, val * 2f)
            : Vector3.LerpUnclamped(__instance.midZoombody, __instance.closeZoomBody, val * 2f - 1f);
        __result = Quaternion.Euler(0f, __instance.entityCamAngle, 0f) * target;

        return false;
    }
}