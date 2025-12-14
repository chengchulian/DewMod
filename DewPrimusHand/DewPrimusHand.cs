using DewPrimusHand.config;
using DewPrimusHand.util;
using UnityEngine;

namespace DewPrimusHand;

public class DewPrimusHand : ModBehaviour
{
    public static DewPrimusHand Instance;
    public readonly PluginConfig Config = new PluginConfig();
    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        instance.isAlteringGameplay = true;
        
        Instance = this;
        LocalizationSource.Init(this);
        harmony.PatchAll();
        CallOnNetworkedManager<ZoneManager>(DewPrimusHandUtil.ZoneManagerOnStarted);
        Debug.Log($"[{mod.metadata.id}] 已加载: {mod.metadata.name} by {mod.metadata.author}");
    }
    
    private void OnDestroy()
    {
        harmony.UnpatchAll(harmony.Id);
    }
}