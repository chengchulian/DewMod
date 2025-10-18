using System.Collections.Generic;
using DewDifficultyCustomizeMod.config;
using HarmonyLib;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(Shrine_Ascension))]
public class Shrine_Ascension_Patch
{
    // 反射获取私有方法 GetNextRarity
    private static readonly System.Reflection.MethodInfo GetNextRarityMethod =
        AccessTools.Method(typeof(Shrine_Ascension), "GetNextRarity");

    // 反射获取私有方法 RpcShowNotice
    private static readonly System.Reflection.MethodInfo RpcShowNoticeMethod =
        AccessTools.Method(typeof(Shrine_Ascension), "RpcShowNotice");

    // 封装对私有方法 GetNextRarity 的调用
    private static Rarity GetNextRarity(Shrine_Ascension instance, Rarity rarity)
    {
        return (Rarity)GetNextRarityMethod.Invoke(instance, new object[] { rarity });
    }

    // 封装对私有方法 RpcShowNotice 的调用
    private static void RpcShowNotice(Shrine_Ascension instance, DewPlayer whom, string fromType, string toType, int level)
    {
        RpcShowNoticeMethod.Invoke(instance, new object[] { whom, fromType, toType, level });
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnActivateEditSkill", typeof(SkillTrigger))]
    public static bool OnActivateEditSkill_Prefix(Shrine_Ascension __instance, ref bool __result, SkillTrigger target)
    {
        string fromType = target.GetType().Name;
        DewPlayer owner = target.owner.owner;
        Hero owner2 = target.owner;
        Rarity nextRarity = GetNextRarity(__instance, target.rarity);
        
        // 获取技能池并过滤掉被移除的技能
        List<string> pool = new List<string>(NetworkedManagerBase<LootManager>.instance.poolSkillsByRarity[nextRarity]);
        var removeSkillsSet = new HashSet<string>(AttrCustomizeResources.Config.removeSkills);
        pool.RemoveAll(item => removeSkillsSet.Contains(item));

        // 如果池为空，则添加默认技能
        if (pool.Count == 0)
        {
            pool.Add("St_C_Sneeze");
        }
        
        string toType = pool[Random.Range(0, pool.Count)];
        SkillTrigger skillTrigger = Dew.CreateSkillTrigger(DewResources.GetByShortTypeName<SkillTrigger>(toType),
            owner2.position, target.level);
        HeroSkillLocation skillType = target.skillType;
        target.Destroy();
        owner2.Skill.EquipSkill(skillType, skillTrigger);

        RpcShowNotice(__instance, owner, fromType, toType, skillTrigger.level);

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnActivateEditSkill", typeof(Gem))]
    public static bool OnActivateEditSkill_Prefix(Shrine_Ascension __instance, ref bool __result, Gem target)
    {
        string fromType = target.GetType().Name;
        DewPlayer owner = target.owner.owner;
        Hero hero = target.owner;
        Rarity nextRarity = GetNextRarity(__instance, target.rarity);
        
        // 获取精华池并过滤掉被移除的精华
        List<string> pool = new List<string>(NetworkedManagerBase<LootManager>.instance.poolGemsByRarity[nextRarity]);
        var removeGemsSet = new HashSet<string>(AttrCustomizeResources.Config.removeGems);
        pool.RemoveAll(item => removeGemsSet.Contains(item));

        // 如果池为空，则添加默认精华
        if (pool.Count == 0)
        {
            pool.Add("Gem_C_Charcoal");
        }
        
        string toType = Dew.SelectRandomWeightedInList(
            pool,
            (string type) => (!hero.Skill.HasGemOfType(type)) ? 1f : 0f);
        Gem gem = Dew.CreateGem(DewResources.GetByShortTypeName<Gem>(toType), hero.position, target.quality);
        GemLocation location = target.location;
        target.Destroy();
        hero.Skill.EquipGem(location, gem);
        RpcShowNotice(__instance, owner, fromType, toType, gem.quality);
        __result = true;

        return false;
    }
}
