using System;
using DewVascularThief.config;
using Mirror;
using UnityEngine;

namespace DewVascularThief.util;

internal sealed class VascularThiefResourceRegistry
{
    public uint AssetId { get; } = VascularThiefNetworkHelper.GenerateAssetId(VascularThiefText.ResourceGuid);

    private GameObject _prefab;
    private St_U_VascularThief _prefabSkill;
    private bool _spawnHandlerRegistered;

    public void Register()
    {
        RegisterWithDewResources();
        EnsurePrefabExists();
        RegisterSpawnHandler();
    }

    public void Unregister()
    {
        if (_spawnHandlerRegistered)
        {
            VascularThiefNetworkHelper.UnregisterSpawnHandler(AssetId);
            _spawnHandlerRegistered = false;
        }

        if (_prefab != null)
        {
            UnityEngine.Object.DestroyImmediate(_prefab);
            _prefab = null;
            _prefabSkill = null;
        }
    }

    public St_U_VascularThief GetPrefab()
    {
        EnsurePrefabExists();
        return _prefabSkill;
    }

    private void RegisterWithDewResources()
    {
        DewInternal.DewResourceDatabase database = DewResources.database;
        Type skillType = typeof(St_U_VascularThief);
        string assemblyQualifiedName = skillType.AssemblyQualifiedName;

        database.typeAssemblyQualifiedNameToGuid[assemblyQualifiedName] = VascularThiefText.ResourceGuid;
        database.netObjectAssetIdToGuid[AssetId] = VascularThiefText.ResourceGuid;

        if (!database.allGuids.Contains(VascularThiefText.ResourceGuid))
        {
            database.allGuids.Add(VascularThiefText.ResourceGuid);
        }

        database.InitForRuntime();
    }

    private void EnsurePrefabExists()
    {
        if (_prefabSkill != null && _prefabSkill)
        {
            return;
        }

        if (_prefab != null)
        {
            UnityEngine.Object.DestroyImmediate(_prefab);
        }

        _prefab = new GameObject(VascularThiefText.SkillTypeName);
        _prefab.SetActive(false);
        _prefab.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(_prefab);

        VascularThiefNetworkHelper.EnsureNetworkIdentity(_prefab, AssetId);
        _prefabSkill = _prefab.AddComponent<St_U_VascularThief>();
        VascularThiefSkillConfigurer.Configure(_prefabSkill);
    }

    private void RegisterSpawnHandler()
    {
        if (_spawnHandlerRegistered)
        {
            return;
        }

        EnsurePrefabExists();
        _spawnHandlerRegistered = VascularThiefNetworkHelper.RegisterSpawnHandler(
            AssetId,
            VascularThiefNetworkHelper.CreateSpawnHandler(_prefab),
            UnspawnHandler);
    }

    private static void UnspawnHandler(GameObject gameObject)
    {
        if (gameObject != null)
        {
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
