using System.Linq;
using DewDifficultyCustomizeMod.config;
using DewDifficultyCustomizeMod.util;
using HarmonyLib;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(PlayGameManager))]
public class PlayGameManager_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayGameManager.DoDejavuSpawn))]
    public static bool DoDejavuSpawn_Prefix(PlayGameManager __instance, DewPlayer h)
    {
        if (AttrCustomizeResources.Config.disableDejaVu)
        {
            // 跳过原方法
            return false;
        }

        if (AttrCustomizeResources.Config.removeGems.Contains(h.selectedDejavuItem))
        {
            // 跳过原方法
            return false;
        }

        if (AttrCustomizeResources.Config.removeSkills.Contains(h.selectedDejavuItem))
        {
            // 跳过原方法
            return false;
        }


        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(PlayGameManager.DoDejavuSpawn))]
    public static void DoDejavuSpawn_Postfix(PlayGameManager __instance, DewPlayer h)
    {
        DewPlayer player = h;
        Vector3 pivot = Dew.GetGoodRewardPosition(player.hero.agentPosition);

        string[] startSkills = AttrCustomizeResources.Config.startSkills;
        int[] startSkillsLevel = AttrCustomizeResources.Config.startSkillsLevel;

        for (var i = 0; i < startSkills.Length; i++)
        {
            SkillTrigger skillTrigger = DewResources.GetByShortTypeName<SkillTrigger>(startSkills[i]);

            Vector3 revivePos = pivot + UnityEngine.Random.insideUnitSphere.Flattened() * 5f;
            revivePos = Dew.GetPositionOnGround(revivePos);
            revivePos = Dew.GetValidAgentDestination_LinearSweep(pivot, revivePos);
            Dew.CreateSkillTrigger(skillTrigger, revivePos, startSkillsLevel[i], player);
        }

        string[] startGems = AttrCustomizeResources.Config.startGems;
        int[] startGemsQuality = AttrCustomizeResources.Config.startGemsQuality;

        for (var i = 0; i < startGems.Length; i++)
        {
            Gem gem = DewResources.GetByShortTypeName<Gem>(startGems[i]);
            Vector3 revivePos = pivot + UnityEngine.Random.insideUnitSphere.Flattened() * 5f;
            revivePos = Dew.GetPositionOnGround(revivePos);
            revivePos = Dew.GetValidAgentDestination_LinearSweep(pivot, revivePos);
            Dew.CreateGem(gem, revivePos, startGemsQuality[i], player);
        }
    }
}