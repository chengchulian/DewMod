using DewVascularThief.config;
using DewVascularThief.util;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace DewVascularThief.patch;

[HarmonyPatch(typeof(DewResources))]
internal static class VascularThiefResourcePatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(DewResources.Load))]
    private static bool Load_Prefix(string guid, ref UnityEngine.Object __result)
    {
        if (guid != VascularThiefText.ResourceGuid)
        {
            return true;
        }

        St_U_VascularThief prefab = VascularThiefSkillFactory.Resources.GetPrefab();
        if (prefab == null)
        {
            return true;
        }

        NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
        if (identity != null)
        {
            VascularThiefNetworkHelper.EnsureNetworkIdentity(prefab.gameObject, VascularThiefSkillFactory.Resources.AssetId);
        }

        __result = prefab.gameObject;
        return false;
    }
}
