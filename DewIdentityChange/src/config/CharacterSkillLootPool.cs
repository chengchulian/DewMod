using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DewIdentityChange.config;

public static class CharacterSkillLootPool
{
    private static readonly Dictionary<HeroSkillLocation, Rarity> LootRarityByLocation = new()
    {
        { HeroSkillLocation.Q, Rarity.Common },
        { HeroSkillLocation.R, Rarity.Rare },
        { HeroSkillLocation.Identity, Rarity.Epic },
        { HeroSkillLocation.Movement, Rarity.Legendary }
    };

    public static void AddTo(LootManager lootManager)
    {
        if (lootManager == null)
        {
            return;
        }

        int addedCount = 0;
        foreach (var pair in LootRarityByLocation)
        {
            foreach (string skillName in HeroSkillSource.SkillNamesByType[pair.Key].Distinct())
            {
                SkillTrigger skill = DewResources.GetByShortTypeName<SkillTrigger>(
                    skillName,
                    ResourceLoadSettings.Light);
                if (skill == null ||
                    skill.rarity is not (Rarity.Character or Rarity.Identity) ||
                    skill.excludeFromPool ||
                    !Dew.IsSkillIncludedInGame(skill.GetType().Name))
                {
                    continue;
                }

                string typeName = skill.GetType().Name;
                bool wasAdded = AddUnique(lootManager.poolSkills, typeName);
                if (lootManager.poolSkillsByRarity.TryGetValue(pair.Value, out var rarityPool))
                {
                    wasAdded |= AddUnique(rarityPool, typeName);
                }

                if (lootManager.poolSkillsByRarity.TryGetValue(skill.rarity, out var nativeRarityPool))
                {
                    AddUnique(nativeRarityPool, typeName);
                }

                AddToTagPools(lootManager, skill, typeName);
                if (wasAdded)
                {
                    addedCount++;
                }
            }
        }

        Debug.Log("[DewIdentityChange] Added " + addedCount + " hero skills to loot pools");
    }

    private static void AddToTagPools(
        LootManager lootManager,
        SkillTrigger skill,
        string typeName)
    {
        if (skill.tags == DescriptionTags.None)
        {
            if (lootManager.poolSkillsByTag.TryGetValue(DescriptionTags.None, out var nonePool))
            {
                AddUnique(nonePool, typeName);
            }

            return;
        }

        foreach (DescriptionTags tag in Enum.GetValues(typeof(DescriptionTags)))
        {
            if (tag == DescriptionTags.None || !skill.tags.HasFlag(tag))
            {
                continue;
            }

            if (lootManager.poolSkillsByTag.TryGetValue(tag, out var tagPool))
            {
                AddUnique(tagPool, typeName);
            }
        }
    }

    private static bool AddUnique(List<string> pool, string typeName)
    {
        if (pool == null || pool.Contains(typeName))
        {
            return false;
        }

        pool.Add(typeName);
        return true;
    }
}
