

using DewHeroSkillJonas.patch;
using UnityEngine;

namespace DewHeroSkillJonas.util;

public class Test
{
    public static void SpawnTest()
    {
        Dew.SpawnEntity(DewResources.GetByType<PropEnt_Merchant_Jonas>(),
            DewConsoleCommands.GetCursorWorldPos(),
            Quaternion.identity, NetworkedManagerBase<ActorManager>.instance.serverActor,
            DewPlayer.creep,
            NetworkedManagerBase<GameManager>.instance.ambientLevel,
            beforeSpawn: (newActor) =>
            {
                
                newActor.gameObject.AddComponent<global::DewHeroSkillJonas.patch.PropEnt_Merchant_Base_Patch.HeroSkillMarker>();
            }
            );
    }
}