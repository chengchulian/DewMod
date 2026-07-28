using System.Linq;
using HarmonyLib;

namespace DewPrimusHand.patch;

[HarmonyPatch(typeof(Se_HeroKnockedOut), nameof(Se_HeroKnockedOut.CheckAndAddHeroSoul))]
public static class Se_HeroKnockedOut_Patch
{
    [HarmonyPrefix]
    public static bool CheckAndAddHeroSoul_Prefix(Se_HeroKnockedOut __instance)
    {
        if (!DewPrimusHand.Instance.Config.SpawnLostSoulInCurrentRoom)
        {
            return true;
        }

        var zoneManager = NetworkedManagerBase<ZoneManager>.instance;
        var owner = __instance.victim?.owner;
        if (__instance.disableQuest ||
            zoneManager == null ||
            owner == null ||
            zoneManager.currentNode.type == WorldNodeType.ExitBoss ||
            Dew.GetAliveHeroCount() == 0)
        {
            return false;
        }

        bool alreadyHasLostSoul = zoneManager.nodes.Any(node =>
            node.modifiers.Any(modifier =>
                modifier.type == nameof(RoomMod_HeroSoul) && modifier.clientData == owner.guid));
        if (!alreadyHasLostSoul)
        {
            zoneManager.AddModifier<RoomMod_HeroSoul>(zoneManager.currentNodeIndex, owner.guid);
        }

        return false;
    }
}
