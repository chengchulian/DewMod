using DewIdentityChange.config;
using UnityEngine;

namespace DewIdentityChange;

public class DewIdentityChange : ModBehaviour
{
    public static DewIdentityChange Instance;
    
    public readonly PluginConfig config = new PluginConfig();
    
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