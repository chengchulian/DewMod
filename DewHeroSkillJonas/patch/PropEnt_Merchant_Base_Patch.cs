using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using DewHeroSkillJonas.util;

namespace DewHeroSkillJonas.patch;

[HarmonyPatch(typeof(PropEnt_Merchant_Base))]
public static class PropEnt_Merchant_Base_Patch
{
    
    [HarmonyPrefix]
    [HarmonyPatch("PopulatePlayerMerchandises")]
    private static bool PopulatePlayerMerchandises_Prefix(PropEnt_Merchant_Base __instance, DewPlayer player)
    {
        
        
        if (!DewHeroSkillJonas.Instance.Config.Enable)
        {
            return true;
        }

        
        if (__instance is not PropEnt_Merchant_Jonas jonas)
        {
            return true;
        }
        
        var heroSkillMarker = __instance.gameObject.GetComponent<HeroSkillMarker>();
        if (heroSkillMarker == null)
        {
            Debug.Log( "No HeroSkillMarker" );
            return true;
        }
        try
        {
            Debug.Log( "HeroSkillMarker" );
            PropEnt_Merchant_HeroSkill.OnPopulateMerchandises(player, jonas);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        __instance.onMerchandisePopulated?.Invoke(player);
        return false;
    }
    
    public class HeroSkillMarker : MonoBehaviour { }
}

