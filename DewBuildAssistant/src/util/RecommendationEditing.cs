using System;
using System.Linq;
using DewBuildAssistant.model;

namespace DewBuildAssistant.util;

// 固定五个技能位和每技能四个精粹位；先校验，再移动，失败时保留全部源数据。
internal static class RecommendationEditing
{
    // 身份稀有度候选和固定 D 位都只展示，不参与技能拖动。
    public static bool CanDragSkill(GuideItem item, SkillBuild source = null) => Is(item, "St_") && source?.Slot != "D" &&
        DewResources.GetByShortTypeName<SkillTrigger>(item.ResourceHint, ResourceLoadSettings.Light)?.rarity != Rarity.Identity;

    public static bool PlaceSkill(SkillBuild target, GuideItem item, SkillBuild source = null)
    {
        if (target == null || !Is(item, "St_") || ReferenceEquals(source, target)) return false;
        if (source != null)
        {
            if (!CanDragSkill(item, source) || target.Slot == "D" || !ReferenceEquals(source.Skill, item) ||
                (target.Skill != null && !CanDragSkill(target.Skill, target))) return false;
            // 固定按键位不变，整组交换技能、四个精粹位及核心标记，避免覆盖目标内容。
            (source.Skill, target.Skill) = (target.Skill, source.Skill);
            (source.IsCore, target.IsCore) = (target.IsCore, source.IsCore);
            (source.GemSlots, target.GemSlots) = (target.GemSlots, source.GemSlots);
            return true;
        }
        target.Skill = Copy(item);
        return true;
    }

    public static bool PlaceGem(BuildRecommendations build, SkillBuild target, int index, GuideItem item, SkillBuild source = null, int sourceIndex = -1)
    {
        if (build == null || target == null || !build.Skills.Contains(target) || index < 0 || index >= 4 || !Is(item, "Gem_") ||
            (ReferenceEquals(source, target) && sourceIndex == index)) return false;
        BuildGem moved = null;
        if (source != null)
        {
            if (!build.Skills.Contains(source) || sourceIndex < 0 || sourceIndex >= 4 ||
                !ReferenceEquals(source.GemSlots[sourceIndex]?.Gem, item)) return false;
            moved = source.GemSlots[sourceIndex];
        }
        var displaced = source == null ? null : target.GemSlots[index];
        if (displaced != null && displaced.Gem?.ResourceHint == item.ResourceHint) return false;
        // 同一资源 key 在整个 QWERD 中只占一槽；移动时排除原槽，候选替换时排除目标槽。
        if (build.Skills.Any(s => s.GemSlots.Where((_, i) =>
                !(ReferenceEquals(s, target) && i == index) && !(ReferenceEquals(s, source) && i == sourceIndex))
            .Any(g => g?.Gem?.ResourceHint == item.ResourceHint ||
                (displaced != null && g?.Gem?.ResourceHint == displaced.Gem?.ResourceHint)))) return false;
        target.GemSlots[index] = moved ?? new BuildGem { Gem = Copy(item), IsCore = target.GemSlots[index]?.IsCore ?? false };
        // 已选精粹整项互换，核心标记跟随精粹；空目标等同于移动。
        if (source != null) source.GemSlots[sourceIndex] = displaced;
        return true;
    }

    // 精粹落到技能图标时只填第一个空位；同技能、重复、满槽或过期拖动均保留源数据。
    public static bool PlaceGemOnSkill(BuildRecommendations build, SkillBuild target, GuideItem item, SkillBuild source = null, int sourceIndex = -1)
    {
        if (target == null || ReferenceEquals(source, target)) return false;
        int index = target.GemSlots.FindIndex(g => g == null);
        return index >= 0 && PlaceGem(build, target, index, item, source, sourceIndex);
    }

    public static bool Remove(SkillBuild source, int gemIndex, GuideItem expected)
    {
        if (source == null || expected == null) return false;
        if (gemIndex < 0)
        {
            if (!ReferenceEquals(source.Skill, expected)) return false;
            ClearSkill(source);
        }
        else
        {
            if (gemIndex >= 4 || !ReferenceEquals(source.GemSlots[gemIndex]?.Gem, expected)) return false;
            source.GemSlots[gemIndex] = null;
        }
        return true;
    }

    private static void ClearSkill(SkillBuild entry)
    {
        entry.Skill = null;
        entry.IsCore = false;
        for (int i = 0; i < 4; i++) entry.GemSlots[i] = null;
    }

    private static bool Is(GuideItem item, string prefix) => item?.ResourceHint?.StartsWith(prefix, StringComparison.Ordinal) == true;
    private static GuideItem Copy(GuideItem item) => new() { Name = item.Name, ResourceHint = item.ResourceHint };
}
