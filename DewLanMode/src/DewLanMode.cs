using DewLanMode.config;
using UnityEngine;

namespace DewLanMode;

public sealed class DewLanMode : ModBehaviour
{
    public static DewLanMode Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        LocalizationSource.Init(this);
        LanDiscoveryService.Install(gameObject);
        harmony.PatchAll();
        Debug.Log($"[{mod.metadata.id}] Loaded {mod.metadata.name} by {mod.metadata.author}");
    }

    private void OnDestroy()
    {
        DewLanModeUiCleanup.CleanupAll();
        LanDiscoveryService.Instance?.StopAdvertising();
        if (Instance == this)
        {
            Instance = null;
        }

        harmony.UnpatchAll(harmony.Id);
    }
}
