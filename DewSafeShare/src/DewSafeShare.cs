using DewSafeShare.config;
using UnityEngine;

namespace DewSafeShare;

public class DewSafeShare : ModBehaviour
{
    public static DewSafeShare Instance;
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
        Debug.Log($"[{mod.metadata.id}] Loaded {mod.metadata.name} by {mod.metadata.author}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        harmony.UnpatchAll(harmony.Id);
    }
}
