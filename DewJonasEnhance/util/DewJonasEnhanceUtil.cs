using Mirror;

namespace DewJonasEnhance.util;

public static class DewJonasEnhanceUtil
{
    public static void ZoneManagerOnStart()
    {
        if (!NetworkServer.active)
        {
            return;
        }
        
        ZoneManager.instance.ClientEvent_OnRoomLoaded += MerchantReload;
        ZoneManager.instance.ClientEvent_OnZoneLoaded += ClientEventOnZoneLoaded;
        
    }

    
    private static void ClientEventOnZoneLoaded(EventInfoLoadZone obj)
    {

        Dew.CallDelayed(OnNewZoneReached);
    }

    private static void OnNewZoneReached()
    {
        NetworkedManagerBase<ZoneManager>.instance.CallOnReadyAfterTransition(AddPlatinumCoins);
    }


    private static void MerchantReload(EventInfoLoadRoom obj)
    {
        if (!DewJonasEnhance.Instance.Config.ReEnterRoomRefresh)
        {
            return;
        }

        if (!SingletonDewNetworkBehaviour<Room>.instance.isRevisit)
        {
            return;
        }

        foreach (Entity entity in NetworkedManagerBase<ActorManager>.instance.allEntities)
        {
            if (entity is not PropEnt_Merchant_Jonas jonas) continue;

            Dew.CallDelayed(delegate
            {
                foreach (DewPlayer current in DewPlayer.allHumanPlayers)
                {
                    jonas.PopulatePlayerMerchandises(current);
                }
            });
        }
    }

    private static void AddPlatinumCoins()
    {
        Dew.CallDelayed(() =>
        {
            foreach (var player in DewPlayer.allHumanPlayers)
            {
                player.platinumCoin += DewJonasEnhance.Instance.Config.AddPlatinumCoin;
            }
        }, 100);
    }
}