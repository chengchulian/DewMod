using System;
using DewJonasEnhance.config;
using DewJonasEnhance.util;
using UnityEngine;

namespace DewJonasEnhance;

public class DewJonasEnhance : ModBehaviour
{
    public static DewJonasEnhance Instance;
    public PluginConfig Config = new PluginConfig();
    
    public void Awake()
    {
        Instance = this;
    }
    public void Start()
    {
        instance.isAlteringGameplay = true;
        LocalizationSource.Init(this);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
        
        CallOnNetworkedManager<ZoneManager>(DewJonasEnhanceUtil.ZoneManagerOnStart);
    }
    public void OnDestroy()
    {
        
        harmony.UnpatchAll(harmony.Id);
    }
}