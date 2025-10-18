using HarmonyLib;
using UnityEngine;

namespace DewMoreVision.patch;

[HarmonyPatch(typeof(CameraManager))]
public class CameraManager_Patch
{
    
    
    [HarmonyPrefix]
    [HarmonyPatch("SetZoomLevel")]
    public static bool SetZoomLevel_Prefix(CameraManager __instance, int level)
    { 
        // 使用反射访问私有属性
        var propertyInfo = AccessTools.Property(typeof(CameraManager), "currentZoomIndex");
        if (propertyInfo != null)
        {
            propertyInfo.SetValue(__instance, Mathf.Clamp(level, -(__instance.zoomSteps / 3), __instance.zoomSteps - 1));
        }
        
        // 返回 false 表示跳过原方法执行
        return false;
    }
    
}