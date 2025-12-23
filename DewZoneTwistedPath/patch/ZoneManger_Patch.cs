using System;
using DewZoneTwistedPath.enums;
using HarmonyLib;
using Mirror;

namespace DewZoneTwistedPath.patch;

[HarmonyPatch(typeof(ZoneManager))]
public class ZoneManger_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(ZoneManager.LoadNextZoneByContentSettings))]
    public static bool LoadNextZoneByContentSettings_Prefix(ZoneManager __instance)
    {
        if (!NetworkServer.active || __instance.nextZoneOverride != null)
        {
            return true;
        }

        // 获取当前区域索引对应的区域配置
        var zoneName = GetZoneByIndex(__instance.currentZoneIndex % 4);
        if (zoneName == ZoneName.Zone_None)
        {
            return true;
        }

        var zoneNameStr = Enum.GetName(zoneName.GetType(), zoneName);
        __instance.nextZoneOverride = DewResources.GetByName<Zone>(zoneNameStr);

        return true;
        

        
    }

    private static ZoneName GetZoneByIndex(int index)
    {
        var config = DewZoneTwistedPath.Instance.Config;
        return index switch
        {
            0 => config.zone1,
            1 => config.zone2,
            2 => config.zone3,
            3 => config.zone4,
            _ => ZoneName.Zone_None
        };
    }
}