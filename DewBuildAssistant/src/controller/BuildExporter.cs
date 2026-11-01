using System;
using System.Collections.Generic;
using System.Linq;
using DewBuildAssistant.model;
using DewBuildAssistant.util;

namespace DewBuildAssistant.controller;

internal static class BuildExporter
{
    // 打开编辑器时只补齐空的 Q/R/D；使用当前页和转职目录，不覆盖玩家已有构筑。
    public static void FillCurrentMemorySkills(BuildRecommendations build)
    {
        if (build == null || !BuildApplicator.TryGetTarget(out string heroType, out _, out var page)) return;
        var skill = DewResources.GetByShortTypeName<Hero>(heroType)?.GetComponent<HeroSkill>();
        if (skill == null) return;
        Fill("Q", HeroSkillLocation.Q);
        Fill("R", HeroSkillLocation.R);
        Fill("D", HeroSkillLocation.Identity);

        void Fill(string slot, HeroSkillLocation location)
        {
            var target = build.Get(slot);
            if (target.Skill != null) return;
            var options = skill.GetLoadoutSkills(location);
            int index = page.GetSkill(location);
            if (options == null || index < 0 || index >= options.Length || options[index] == null) return;
            string key = options[index].GetType().Name;
            RecommendationEditing.PlaceSkill(target, new GuideItem { ResourceHint = key, Name = ResourceDisplayName(key) });
        }
    }

    // 导出点击时捕获的当前页，星座只分享资源名，等级沿用下载者自己的购买等级。
    public static GuideData Capture(string title, string description, string author)
    {
        if (!BuildApplicator.TryGetTarget(out string heroType, out _, out var page))
            throw new InvalidOperationException("Lobby loadout is not ready.");
        var hero = DewResources.GetByShortTypeName<Hero>(heroType);
        if (hero == null) throw new InvalidOperationException("Hero resource is missing.");
        var guide = new GuideData { Hero = heroType, Title = title.Trim(), Summary = description ?? "", Author = author,
            RequiresIdentityChange = IdentityChangeCompatibility.Enabled };
        var skill = hero.GetComponent<HeroSkill>();
        AddSkill("旅行者记忆1", HeroSkillLocation.Q);
        AddSkill("旅行者记忆2", HeroSkillLocation.R);
        AddSkill("旅行者身份记忆", HeroSkillLocation.Identity);
        AddStars("破坏星座", StarType.Destruction);
        AddStars("生命星座", StarType.Life);
        AddStars("幻想星座", StarType.Imagination);
        AddStars("灵活星座", StarType.Flexible);
        string dejavu = NetworkedManagerBase<GameSettingsManager>.softInstance.GetLocalPreferredGameSettings().dejavuItem;
        guide.PresentFields.Add("既视感");
        guide.Items["既视感"] = new List<GuideItem>();
        guide.ClearDejavu = string.IsNullOrEmpty(dejavu);
        if (!string.IsNullOrEmpty(dejavu))
            guide.Items["既视感"].Add(new GuideItem { Name = dejavu, ResourceHint = dejavu });
        LocalizeForDisplay(guide);
        WorkshopBuildCodec.Encode(guide);
        return guide;

        void AddSkill(string field, HeroSkillLocation location)
        {
            var options = skill.GetLoadoutSkills(location);
            int index = page.GetSkill(location);
            if (index < 0 || index >= options.Length) throw new InvalidOperationException("Invalid traveler memory.");
            string key = options[index].GetType().Name;
            guide.PresentFields.Add(field);
            guide.Items[field] = new List<GuideItem> { new() { Name = key, ResourceHint = key } };
        }

        void AddStars(string field, StarType type)
        {
            guide.PresentFields.Add(field);
            guide.Items[field] = page.GetStarList(type).Where(i => !string.IsNullOrEmpty(i.name)).Select(i => new GuideItem
            {
                ResourceHint = i.name,
                Name = i.name
            }).ToList();
        }
    }

    // 名称仅用于详情和工坊描述，下载者按自己的游戏语言生成，不参与资源匹配。
    public static void LocalizeForDisplay(GuideData guide)
    {
        foreach (var items in guide.Items.Values)
        foreach (var item in items) item.Name = ResourceDisplayName(item.ResourceHint);
        foreach (var entry in guide.Recommendations.Skills)
        {
            if (entry.Skill != null) entry.Skill.Name = ResourceDisplayName(entry.Skill.ResourceHint);
            foreach (var gem in entry.GemSlots.Where(g => g != null)) gem.Gem.Name = ResourceDisplayName(gem.Gem.ResourceHint);
        }
        guide.Details = WorkshopBuildCodec.Describe(guide);
    }

    public static string ResourceDisplayName(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
            foreach (var data in new[] { DewLocalization.data,
                DewLocalization.buildData.dataByLanguage.TryGetValue("zh-CN", out var chinese) ? chinese : null })
            {
                if (data == null) continue;
                if (data.stars.TryGetValue(key, out var star)) return star.name;
                if (key.StartsWith("Gem_", StringComparison.Ordinal) && data.gems.TryGetValue(key.Substring(4), out var gem)) return gem.name;
                if (key.StartsWith("St_", StringComparison.Ordinal) && data.skills.TryGetValue(DewLocalization.GetSkillKey(key), out var skill) && skill.configs.Count > 0)
                    return skill.configs[0].name;
            }
            return key;
    }

    // 使用游戏内容目录，不要求推荐物品已在当前存档解锁；这里只编辑攻略建议。
    public static List<GuideItem> RecommendationOptions(bool gems)
    {
        IEnumerable<string> keys = gems
            ? DewResources.FindAllByType<Gem>(ResourceLoadSettings.Light).Where(g => !g.excludeFromPool && Dew.IsGemIncludedInGame(g.GetType().Name)).Select(g => g.GetType().Name)
            : DewResources.FindAllByType<SkillTrigger>(ResourceLoadSettings.Light).Where(s => !s.excludeFromPool &&
                !s.GetType().Name.StartsWith("St_M_", StringComparison.Ordinal) && Dew.IsSkillIncludedInGame(s.GetType().Name)).Select(s => s.GetType().Name);
        return keys.Distinct().Select(k => new GuideItem { Name = ResourceDisplayName(k), ResourceHint = k }).OrderBy(i => i.Name).ToList();
    }
}
