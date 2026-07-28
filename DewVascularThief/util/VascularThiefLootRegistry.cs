using System;
using DewVascularThief.config;

namespace DewVascularThief.util;

internal static class VascularThiefLootRegistry
{
    public static void Register(LootManager lootManager)
    {
        if (lootManager == null)
        {
            return;
        }

        AddUnique(lootManager.poolSkills, VascularThiefText.SkillTypeName);
        if (lootManager.poolSkillsByRarity.TryGetValue(Rarity.Unique, out var uniqueSkills))
        {
            AddUnique(uniqueSkills, VascularThiefText.SkillTypeName);
        }

        if (lootManager.poolSkillsByTag.TryGetValue(DescriptionTags.None, out var untaggedSkills))
        {
            AddUnique(untaggedSkills, VascularThiefText.SkillTypeName);
        }
    }

    private static void AddUnique(System.Collections.Generic.List<string> list, string value)
    {
        if (list != null && !list.Contains(value))
        {
            list.Add(value);
        }
    }
}
