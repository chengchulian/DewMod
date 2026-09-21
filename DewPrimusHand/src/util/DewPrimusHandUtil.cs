using System;
using Mirror;

namespace DewPrimusHand.util;

public static class DewPrimusHandUtil
{
    public static Action ZoneManagerOnStarted(ZoneManager zoneManager)
    {
        zoneManager.ClientEvent_OnZoneLoaded += EnableWorldReveal;
        return () => zoneManager.ClientEvent_OnZoneLoaded -= EnableWorldReveal;
    }

    private static void EnableWorldReveal(EventInfoLoadZone e)
    {
        if (NetworkServer.active && DewPrimusHand.Instance.Config.WorldReveal)
        {
            NetworkedManagerBase<ZoneManager>.instance.RevealWorld(true);
        }
    }
}
