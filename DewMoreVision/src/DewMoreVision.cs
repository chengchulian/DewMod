using DewMoreVision.config;
using UnityEngine;

namespace DewMoreVision;

public class DewMoreVision : ModBehaviour
{
    public static DewMoreVision Instance;
    public readonly PluginConfig Config = new PluginConfig();
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

    private void Test()
    {
        Dew.CreateSkillTrigger(DewResources.GetByType<St_U_WorldCracker>(),DewConsoleCommands.GetCursorWorldPos(),1,DewPlayer.local);
    }
    
}