using System;
using System.Collections.Generic;
using System.Linq;
using DewLucidDreamExpansion.content;
using DewLucidDreamExpansion.ui;
using HarmonyLib;
using Mirror;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DewLucidDreamExpansion.controller;

internal static class DreamRegistry
{
    internal static readonly Type[] Dreams =
    {
        typeof(LucidDream_AstronomyMaster), typeof(LucidDream_Telescope), typeof(LucidDream_ResourceInversion),
        typeof(LucidDream_PrimordialCurse), typeof(LucidDream_FallenStarGod), typeof(LucidDream_HeavyFootsteps)
    };

    private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
    private static readonly Dictionary<Type, string> Guids = new Dictionary<Type, string>();
    private static readonly Dictionary<Type, uint> AssetIds = new Dictionary<Type, uint>();

    // 固定标识保证存档和联机资源一致；使用原版数据库生成网络预制体。
    internal static void Register()
    {
        var database = DewResources.database;
        var list = (List<Type>)Dew.allLucidDreams;
        foreach (Type type in Dreams.Concat(new[] { typeof(Shrine_Astronomy) }))
        {
            string guid = "lde-" + type.Name;
            uint id = 2166136261;
            foreach (char c in guid) id = unchecked((id ^ c) * 16777619);
            id |= 0x80000000;
            Guids[type] = guid;
            AssetIds[type] = id;
            database.typeAssemblyQualifiedNameToGuid[type.AssemblyQualifiedName] = guid;
            database.netObjectAssetIdToGuid[id] = guid;
            if (!database.allGuids.Contains(guid)) database.allGuids.Add(guid);
            if (typeof(LucidDream).IsAssignableFrom(type) && !list.Contains(type)) list.Add(type);
        }
        database.InitForRuntime();
        RegisterProfile(DewSave.profileMain);
        foreach (Type type in Guids.Keys)
        {
            uint id = AssetIds[type];
            string guid = Guids[type];
            NetworkClient.RegisterSpawnHandler(id, message =>
            {
                GameObject instance = Object.Instantiate(GetPrefab(guid), message.position, message.rotation);
                instance.transform.localScale = message.scale;
                instance.SetActive(true);
                InitializeIdentity(instance.GetComponent<NetworkIdentity>(), id);
                return instance;
            }, Object.Destroy);
        }
    }

    internal static void RegisterProfile(DewProfile profile)
    {
        if (profile == null) return;
        foreach (Type type in Dreams)
        {
            if (!profile.lucidDreams.TryGetValue(type.Name, out var data))
                profile.lucidDreams[type.Name] = data = new DewProfile.UnlockData();
            data.status = UnlockStatus.Complete;
        }
    }

    internal static bool IsCustom(Type type) => Guids.ContainsKey(type);
    internal static bool IsGuid(string guid) => guid != null && Guids.ContainsValue(guid);

    // 预制体按需生成，祭坛只复用原版模型和特效，网络行为使用独立组件。
    internal static GameObject GetPrefab(string guid)
    {
        if (Prefabs.TryGetValue(guid, out var existing) && existing != null) return existing;
        Type type = Guids.First(p => p.Value == guid).Key;
        var root = new GameObject(type.Name);
        root.SetActive(false);
        root.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(root);
        root.AddComponent<NetworkIdentity>();
        Actor actor = (Actor)root.AddComponent(type);
        if (actor is LucidDream dream)
        {
            dream.type = GetDreamType(type);
            dream.icon = DreamIcons.Get(type);
        }
        else if (actor is Shrine_Astronomy shrine)
        {
            Shrine_Enlightenment source = DewResources.GetByType<Shrine_Enlightenment>();
            ShrinePresentation.Copy(source, shrine);
            shrine.maxUseCountPerPlayer = 1;
            shrine.maxTotalUseCount = -1;
            shrine.baseGoldCost = 0;
        }
        InitializeIdentity(root.GetComponent<NetworkIdentity>(), AssetIds[type]);
        DewResources.database.objectToGuidFallback[root] = guid;
        Prefabs[guid] = root;
        return root;
    }

    internal static LucidDreamType GetDreamType(Type type)
    {
        int index = Array.IndexOf(Dreams, type);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(type));
        return index < 2 ? LucidDreamType.Good : index == 2 ? LucidDreamType.Chaotic : LucidDreamType.Evil;
    }

    private static void InitializeIdentity(NetworkIdentity identity, uint id)
    {
        AccessTools.Field(typeof(NetworkIdentity), "_assetId").SetValue(identity, id);
        identity.sceneId = 0;
        AccessTools.Field(typeof(NetworkIdentity), "_isSceneObject")?.SetValue(identity, false);
        AccessTools.Method(typeof(NetworkIdentity), "InitializeNetworkBehaviours").Invoke(identity, null);
    }

    // 只移除本 Mod 注册的数据和对象。
    internal static void Unregister()
    {
        var database = DewResources.database;
        var list = (List<Type>)Dew.allLucidDreams;
        foreach (Type type in Guids.Keys)
        {
            NetworkClient.UnregisterSpawnHandler(AssetIds[type]);
            database.typeAssemblyQualifiedNameToGuid.Remove(type.AssemblyQualifiedName);
            database.netObjectAssetIdToGuid.Remove(AssetIds[type]);
            database.allGuids.Remove(Guids[type]);
            list.Remove(type);
        }
        database.InitForRuntime();
        foreach (GameObject prefab in Prefabs.Values)
        {
            database.objectToGuidFallback.Remove(prefab);
            if (prefab != null) Object.Destroy(prefab);
        }
        Prefabs.Clear();
        Guids.Clear();
        AssetIds.Clear();
        DreamIcons.Clear();
    }
}
