using UnityEngine;
using DewRoomGuidance.config;

namespace DewRoomGuidance;

public sealed class DewRoomGuidance : ModBehaviour
{
    private RoomGuidanceOverlay _overlay;

    private void Awake()
    {
        LocalizationSource.Init(this);
        _overlay = gameObject.AddComponent<RoomGuidanceOverlay>();
    }

    private void OnDestroy()
    {
        if (_overlay != null)
        {
            Destroy(_overlay);
            _overlay = null;
        }
    }
}
