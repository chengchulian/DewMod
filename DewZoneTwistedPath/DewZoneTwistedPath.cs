using DewZoneReorder.config;
using DewZoneTwistedPath.config;
using UnityEngine;

namespace DewZoneTwistedPath;

public class DewZoneTwistedPath : ModBehaviour
{
    public static DewZoneTwistedPath Instance;
    
    public readonly PluginConfig Config = new PluginConfig();
    
    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        instance.isAlteringGameplay = true;
        LocalizationSource.Init(this);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
    }
    
    private void OnDestroy()
    {
        harmony.UnpatchAll(harmony.Id);
    }
}