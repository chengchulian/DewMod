using System;
using System.Collections.Generic;
using System.Linq;
using DewBuildAssistant.config;
using DewBuildAssistant.model;

namespace DewBuildAssistant.util;

internal sealed class RecommendationGroup
{
    public string HeroKey;
    public Rarity? Rarity;
    public List<GuideItem> Items = new();
    public List<RecommendationGroup> Children = new();
    public string Title
    {
        get
        {
            if (HeroKey != null) return DewLocalization.TryGetUIValue(HeroKey + "_Name", out string hero) ? hero : HeroKey;
            if (Rarity.HasValue) return DewLocalization.TryGetUIValue("InGame_Tooltip_Rarity_" + Rarity.Value, out string rarity) ? rarity : Rarity.Value.ToString();
            return LocalizationSource.GetLocalizationText("CandidateGeneralMemories");
        }
    }
}

// 记忆和精粹均先按原生稀有度分类；英雄／身份分类内再沿用图鉴的英雄和配装顺序。
internal static class RecommendationCatalog
{
    public static List<RecommendationGroup> Groups(IEnumerable<GuideItem> items, bool gems)
    {
        if (gems)
            return items.GroupBy(i => DewResources.GetByShortTypeName<Gem>(i.ResourceHint, ResourceLoadSettings.Light)?.rarity ?? Rarity.Common)
                .OrderBy(g => (int)g.Key).Select(g => new RecommendationGroup
                {
                    Rarity = g.Key, Items = g.OrderBy(i => i.ResourceHint, StringComparer.Ordinal).ToList()
                }).ToList();

        var heroes = new List<string>();
        var skills = new Dictionary<string, Tuple<string, int>>(StringComparer.Ordinal);
        var locations = new[] { HeroSkillLocation.Q, HeroSkillLocation.R, HeroSkillLocation.Identity };
        foreach (var heroType in Dew.allHeroes)
        {
            if (!Dew.IsHeroIncludedInGame(heroType.Name)) continue;
            var hero = DewResources.GetByShortTypeName<Hero>(heroType.Name, ResourceLoadSettings.Light);
            var loadout = hero?.GetComponent<HeroSkill>();
            if (loadout == null) continue;
            heroes.Add(heroType.Name);
            for (int k = 0; k < locations.Length; k++)
            {
                var options = loadout.GetLoadoutSkills(locations[k]) ?? Array.Empty<SkillTrigger>();
                for (int index = 0; index < options.Length; index++)
                {
                    if (options[index] == null) continue;
                    string key = options[index].GetType().Name;
                    if (!skills.ContainsKey(key)) skills.Add(key, Tuple.Create(heroType.Name, k * 10 + index));
                }
            }
        }
        var groups = new List<RecommendationGroup>();
        foreach (var category in items.GroupBy(RarityOf).OrderBy(g => (int)g.Key))
        {
            var group = new RecommendationGroup { Rarity = category.Key };
            bool byHero = category.Key == Rarity.Character || category.Key == Rarity.Identity;
            group.Items = category.Where(i => !byHero || !skills.ContainsKey(i.ResourceHint))
                .OrderBy(i => i.ResourceHint, StringComparer.Ordinal).ToList();
            if (byHero)
                foreach (string hero in heroes)
                {
                    var selected = category.Where(i => skills.TryGetValue(i.ResourceHint, out var origin) && origin.Item1 == hero)
                        .OrderBy(i => skills[i.ResourceHint].Item2).ThenBy(i => i.ResourceHint, StringComparer.Ordinal).ToList();
                    if (selected.Count > 0) group.Children.Add(new RecommendationGroup { HeroKey = hero, Items = selected });
                }
            groups.Add(group);
        }
        return groups;
    }

    private static Rarity RarityOf(GuideItem item) => DewResources.GetByShortTypeName<SkillTrigger>(item.ResourceHint, ResourceLoadSettings.Light)?.rarity ?? Rarity.Common;
}
