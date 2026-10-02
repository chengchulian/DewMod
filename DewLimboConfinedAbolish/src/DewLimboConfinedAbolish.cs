using UnityEngine;
using DewLimboConfinedAbolish.config;

namespace DewLimboConfinedAbolish;

public sealed class DewLimboConfinedAbolish : ModBehaviour
{
    public static DewLimboConfinedAbolish Instance;

    public PluginConfig config = new PluginConfig();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        instance.isAlteringGameplay = true;
        LocalizationSource.Init(this);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] 已加载：{mod.metadata.name} by {mod.metadata.author}");
    }

    private void OnDestroy()
    {
        harmony.UnpatchAll(harmony.Id);
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
