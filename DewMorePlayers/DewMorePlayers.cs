using System;
using DewMorePlayers.config;
using UnityEngine;

namespace DewMorePlayers;

public class DewMorePlayers : ModBehaviour
{
    public static DewMorePlayers Instance;

    public PluginConfig config = new PluginConfig();
    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        
        LocalizationSource.Init(this);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
    }

    private void OnDestroy()
    {
        harmony.UnpatchAll(harmony.Id);
    }
}