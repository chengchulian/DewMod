using System;
using System.Collections.Generic;
using System.Linq;
using DewDifficultyCustomizeMod.config;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(Loot_Skill))]
public class Loot_Skill_Patch
{
    private static readonly Lazy<Dictionary<HeroSkillLocation, List<string>>> _skillsByTypeLazy =
        new(() =>
        {
            var dict = new Dictionary<HeroSkillLocation, List<string>>
            {
                { HeroSkillLocation.Q, new List<string>() },
                { HeroSkillLocation.R, new List<string>() },
                { HeroSkillLocation.Identity, new List<string>() },
                { HeroSkillLocation.Movement, new List<string>() }
            };

            foreach (var keyValuePair in DewLocalization.data.skills)
            {
                var key = keyValuePair.Key;
                var byShortTypeName = DewResources.GetByShortTypeName("St_" + key);
                if (byShortTypeName is not SkillTrigger skillTrigger) continue;

                if (skillTrigger.rarity is Rarity.Character or Rarity.Identity)
                {
                    dict[skillTrigger.skillType].Add(skillTrigger.name);
                }
            }

            return dict;
        });

    public static Dictionary<HeroSkillLocation, List<string>> SkillsByType => _skillsByTypeLazy.Value;

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Loot_Skill.SelectSkillAndLevel))]
    public static bool SelectSkillAndLevel_Prefix(Loot_Skill __instance, Rarity rarity, out SkillTrigger skill,
        out int level)
    {
        // 获取技能池并存储到局部变量中
        var pool = new HashSet<string>(NetworkedManagerBase<LootManager>.instance.poolSkillsByRarity[rarity]);

        // 使用静态字典来简化逻辑
        
        if (AttrCustomizeResources.Config.enableHeroSkillAddShop)
        {
            switch (rarity)
            {
                case Rarity.Common :
                    pool.UnionWith(SkillsByType[HeroSkillLocation.Q]);
                    break;
                case Rarity.Rare :
                    pool.UnionWith(SkillsByType[HeroSkillLocation.R]);
                    break;
                case Rarity.Epic :
                    pool.UnionWith(SkillsByType[HeroSkillLocation.Identity]);
                    break;
                case Rarity.Legendary :
                    pool.UnionWith(SkillsByType[HeroSkillLocation.Movement]);
                    break;
                default:
                    break;
            }
        }

        // 将需要移除的技能列表转换为 HashSet 以提高效率
        var removeSkillsSet = new HashSet<string>(AttrCustomizeResources.Config.removeSkills);
        pool.ExceptWith(removeSkillsSet);

        // 如果池为空，则添加默认技能
        if (pool.Count == 0)
        {
            pool.Add("St_C_Sneeze");
        }

        // 随机选择一个技能并获取其等级
        skill = DewResources.GetByShortTypeName<SkillTrigger>(pool.ElementAt(Random.Range(0, pool.Count)));

        // 计算技能等级
        var currentZoneIndex = NetworkedManagerBase<ZoneManager>.instance.currentZoneIndex;
        float minLevel = __instance.skillLevelMinByZoneIndex.Get(rarity).Evaluate(currentZoneIndex);
        float maxLevel = __instance.skillLevelMaxByZoneIndex.Get(rarity).Evaluate(currentZoneIndex);
        float floatLevel = Mathf.Lerp(minLevel, maxLevel, __instance.levelRandomCurve.Evaluate(Random.value));
        level = Mathf.Clamp(Mathf.RoundToInt(floatLevel), 1, 100);

        return false;
    }
}