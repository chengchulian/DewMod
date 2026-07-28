using System;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace DewVascularThief.util;

internal static class VascularThiefNetworkHelper
{
    public static uint GenerateAssetId(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            input = "DewVascularThief";
        }

        uint hash = 2166136261;
        foreach (char c in input)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return hash | 0x80000000;
    }

    public static NetworkIdentity EnsureNetworkIdentity(GameObject gameObject, uint assetId)
    {
        if (gameObject == null)
        {
            return null;
        }

        NetworkIdentity identity = gameObject.GetComponent<NetworkIdentity>() ?? gameObject.AddComponent<NetworkIdentity>();
        SetAssetId(identity, assetId);
        MarkAsRuntimePrefab(identity);
        return identity;
    }

    public static bool RegisterSpawnHandler(uint assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
    {
        if (spawnHandler == null)
        {
            return false;
        }

        NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler ?? DefaultUnspawnHandler);
        return true;
    }

    public static void UnregisterSpawnHandler(uint assetId)
    {
        try
        {
            NetworkClient.UnregisterSpawnHandler(assetId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[DewVascularThief] Failed to unregister spawn handler {assetId}: {exception.Message}");
        }
    }

    public static SpawnHandlerDelegate CreateSpawnHandler(GameObject prefab)
    {
        return message =>
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, message.position, message.rotation);
            instance.transform.localScale = message.scale;
            instance.name = prefab.name;
            instance.SetActive(true);

            NetworkIdentity identity = instance.GetComponent<NetworkIdentity>();
            if (identity != null)
            {
                MarkAsRuntimePrefab(identity);
                InitializeNetworkBehaviours(identity);
            }

            return instance;
        };
    }

    private static void DefaultUnspawnHandler(GameObject gameObject)
    {
        if (gameObject != null)
        {
            UnityEngine.Object.Destroy(gameObject);
        }
    }

    private static void SetAssetId(NetworkIdentity identity, uint assetId)
    {
        FieldInfo assetIdField = typeof(NetworkIdentity).GetField("_assetId", BindingFlags.NonPublic | BindingFlags.Instance);
        if (assetIdField != null)
        {
            assetIdField.SetValue(identity, assetId);
        }
    }

    private static void MarkAsRuntimePrefab(NetworkIdentity identity)
    {
        identity.sceneId = 0UL;
        FieldInfo isSceneObjectField = typeof(NetworkIdentity).GetField("_isSceneObject", BindingFlags.NonPublic | BindingFlags.Instance);
        isSceneObjectField?.SetValue(identity, false);
    }

    private static void InitializeNetworkBehaviours(NetworkIdentity identity)
    {
        MethodInfo initMethod = typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BindingFlags.NonPublic | BindingFlags.Instance);
        initMethod?.Invoke(identity, null);
    }
}
