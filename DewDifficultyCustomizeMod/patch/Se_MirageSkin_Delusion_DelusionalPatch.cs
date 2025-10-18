using System;
using System.Reflection;
using DewDifficultyCustomizeMod.config;
using HarmonyLib;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(Se_MirageSkin_Delusion_Delusional))]
public class SeMirageSkinDelusionDelusional_Patch
{

    [HarmonyPrefix]
    [HarmonyPatch("VictimOntakenHealProcessor")]
    public static bool VictimOntakenHealProcessor_Prefix(Se_MirageSkin_Delusion_Delusional __instance, ref HealData data,
        Actor actor, Entity target)
    {
        float num;
        if (AttrCustomizeResources.Config.enableHealthReduceMultiplierAddByZone)
        {
            num = (float)(90.0 + 10.0 * (1.0 - 1.0 / Math.Pow(2.0,
                Math.Min(100, NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex))));
        }
        else
        {
            num = __instance.stack;
        }

        data.ApplyReduction(num / 100f);
        if (!__instance.victim.TryGetData<Ad_HealPreventedText>(out var prevented))
        {
            prevented = new Ad_HealPreventedText();
            __instance.victim.AddData(prevented);
        }

        if (!(Time.time - prevented.lastShowTime < 0.35f))
        {
            prevented.lastShowTime = Time.time;

            // 调用私有方法 RpcShowHealPrevented
            var rpcShowHealPreventedMethod = __instance.GetType()
                .GetMethod("RpcShowHealPrevented", BindingFlags.Instance | BindingFlags.NonPublic);
            if (rpcShowHealPreventedMethod != null)
            {
                rpcShowHealPreventedMethod.Invoke(__instance, null);
            }
        }

        return false;
    }

    private class Ad_HealPreventedText
    {
        public float lastShowTime;
    }
}