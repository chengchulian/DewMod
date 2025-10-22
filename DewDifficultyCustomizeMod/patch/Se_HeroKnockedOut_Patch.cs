using System.Linq;
using DewDifficultyCustomizeMod.config;
using HarmonyLib;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(Se_HeroKnockedOut))]
public class Se_HeroKnockedOut_Patch
{
    
    
    // 创建访问 _didAddQuest 的字段引用
    private static readonly AccessTools.FieldRef<Se_HeroKnockedOut, bool> _didAddQuestRef =
        AccessTools.FieldRefAccess<Se_HeroKnockedOut, bool>("_didAddQuest");
    
    [HarmonyPostfix]
    [HarmonyPatch("ActiveLogicUpdate")]
    public static void ActiveLogicUpdate_Postfix(Se_HeroKnockedOut __instance, float dt)
    {
        if (!__instance.isServer)
        {
            return;
        }
        if (!_didAddQuestRef(__instance)
            && Time.time - __instance.creationTime > 1f
            && Dew.SelectRandomAliveHero(fallbackToDead: false) != null
            && (AttrCustomizeResources.Config.enableBossRoomGenerateLostSoul 
                || NetworkedManagerBase<ZoneManager>.instance.currentNode.type != WorldNodeType.ExitBoss))
        {
            _didAddQuestRef(__instance) = true;
    
            NetworkedManagerBase<QuestManager>.instance.StartQuest<Quest_LostSoul>(s => 
            {
                s.NetworktargetHero = (Hero)__instance.victim;
            });
        }
        __instance.victim.Status.SetHealth(0.01f);
        
    }

    [HarmonyPrefix]
    [HarmonyPatch("CheckAndAddHeroSoul")]
    public static bool CheckAndAddHeroSoul_Prefix(Se_HeroKnockedOut __instance)
    {
        if (!AttrCustomizeResources.Config.enableCurrentNodeGenerateLostSoul)
        {
            return true;
        }
        if (!NetworkedManagerBase<ZoneManager>.instance.nodes.Any((WorldNodeData n) =>
                n.modifiers.Any(m =>
                    m.type == "RoomMod_HeroSoul" && m.clientData == __instance.victim.owner.guid)))
        {
            NetworkedManagerBase<ZoneManager>.instance.AddModifier<RoomMod_HeroSoul>(NetworkedManagerBase<ZoneManager>.instance.currentNodeIndex, __instance.victim.owner.guid);
        }

        return false;
    }
    
    
}