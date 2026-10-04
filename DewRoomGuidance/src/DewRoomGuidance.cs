using UnityEngine;
using DewRoomGuidance.config;

namespace DewRoomGuidance;

public sealed class DewRoomGuidance : ModBehaviour
{
    public static DewRoomGuidance Instance { get; private set; }
    public PluginConfig Config = new PluginConfig();
    private RoomGuidanceOverlay _overlay;

    private void Awake()
    {
        Instance = this;
        LocalizationSource.Init(this);
        _overlay = gameObject.AddComponent<RoomGuidanceOverlay>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (_overlay != null)
        {
            Destroy(_overlay);
            _overlay = null;
        }
    }
}
