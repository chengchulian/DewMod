using System;
using Mirror;

namespace DewPrimusHand.util;

public static class DewPrimusHandUtil
{
    public static void ZoneManagerOnStarted()
    {
        if (!NetworkServer.active)
        {
            return;
        }
        
        ZoneManager.instance.ClientEvent_OnZoneLoaded += EnableWorldReveal;
        
        

    }

    private static void EnableWorldReveal(EventInfoLoadZone e)
    {
        if (DewPrimusHand.Instance.Config.WorldReveal)
        {
            NetworkedManagerBase<ZoneManager>.instance.RevealWorld(true);
        }
    }
}