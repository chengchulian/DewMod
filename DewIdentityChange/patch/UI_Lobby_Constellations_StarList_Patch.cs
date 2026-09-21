using System;
using System.Collections.Generic;
using System.Linq;
using DewIdentityChange.util;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(UI_Lobby_Constellations_StarList))]
public class UI_Lobby_Constellations_StarList_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("Refresh", new Type[] { })]
    public static bool Refresh_Prefix(UI_Lobby_Constellations_StarList __instance)
    {
        if (DewIdentityChange.Instance?.IsIdentityEnabled != true)
        {
            return true;
        }

        UI_Lobby_Constellations constellations = SingletonBehaviour<UI_Lobby_Constellations>.instance;
        if (DewPlayer.local == null ||
            constellations == null ||
            constellations.loadout == null ||
            string.IsNullOrEmpty(DewPlayer.local.selectedHeroType))
        {
            return false;
        }

        var hero = DewResources.GetByShortTypeName<Hero>(DewPlayer.local.selectedHeroType);
        if (hero == null)
        {
            return false;
        }

        List<UI_Lobby_Constellations_StarItem> items = __instance.items;
        UI_ToggleGroup categoryGroup = __instance.categoryGroup;
        TextMeshProUGUI categoryTitleText = __instance.categoryTitleText;
        TextMeshProUGUI categoryDescText = __instance.categoryDescText;
        GameObject[] fxPerCategoryEffects = __instance.fxPerCategoryEffects;
        TextMeshProUGUI characterSpecificTitle = __instance.characterSpecificTitle;
        TextMeshProUGUI globalTitle = __instance.globalTitle;
        GameObject globalDesc = __instance.globalDesc;
        GameObject seperatorObject = __instance.seperatorObject;
        RectTransform characterSpecificGroup = __instance.characterSpecificGroup;
        RectTransform globalGroup = __instance.globalGroup;
        UI_ToggleGroup listGroup = __instance.listGroup;

        __instance.hoveredIndex = -1;
        listGroup.currentIndex = -1;
        // Newer game versions pool both groups. Clear those references as well so
        // switching back to the original Refresh cannot reuse destroyed objects.
        var previousItems = new HashSet<UI_Lobby_Constellations_StarItem>(items);
        foreach (string fieldName in new[] { "_heroItems", "_globalItems" })
        {
            if (AccessTools.Field(typeof(UI_Lobby_Constellations_StarList), fieldName)?.GetValue(__instance)
                is List<UI_Lobby_Constellations_StarItem> cachedItems)
            {
                previousItems.UnionWith(cachedItems);
                cachedItems.Clear();
            }
        }

        foreach (var item in previousItems)
        {
            if (item == null) continue;
            item.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(item.gameObject);
        }

        items.Clear();

        var category = (StarType)categoryGroup.currentIndex;
        Color starCategoryColor = Dew.GetStarCategoryColor(category);
        categoryTitleText.text = DewLocalization.GetUIValue($"Constellations_Category_{category}");
        categoryTitleText.color = Color.Lerp(starCategoryColor, Color.white, 0.5f);
        categoryDescText.text = DewLocalization.GetUIValue($"Constellations_Category_{category}_Description");
        categoryDescText.color = ColorExtensions.WithA(Color.Lerp(starCategoryColor, Color.white, 0.5f), 0.66f);
        UiEffect.Play(fxPerCategoryEffects[categoryGroup.currentIndex]);

        var heroSkill = hero.GetComponent<HeroSkill>();
        SkillTrigger[] loadoutQ = heroSkill.GetLoadoutSkills(HeroSkillLocation.Q);
        SkillTrigger[] loadoutR = heroSkill.GetLoadoutSkills(HeroSkillLocation.R);
        SkillTrigger[] loadoutTrait = heroSkill.GetLoadoutSkills(HeroSkillLocation.Identity);

        characterSpecificTitle.text = DewLocalization.GetUIValue(DewPlayer.local.selectedHeroType + "_Name");

        var orderedStars = Dew.allStarTypes
            .Where(t => Dew.IsStarIncludedInGame(t.Name))
            .Select(t => DewResources.GetByType<StarEffect>(t))
            .Where(se => se != null)
            .OrderBy(se =>
            {
                if (se.skillType == null)
                {
                    return -1;
                }

                for (int i = 0; i < loadoutQ.Length; i++)
                {
                    if (loadoutQ[i].GetType() == se.skillType)
                    {
                        return i;
                    }
                }

                for (int i = 0; i < loadoutR.Length; i++)
                {
                    if (loadoutR[i].GetType() == se.skillType)
                    {
                        return 10 + i;
                    }
                }

                for (int i = 0; i < loadoutTrait.Length; i++)
                {
                    if (loadoutTrait[i].GetType() == se.skillType)
                    {
                        return 20 + i;
                    }
                }

                return -1;
            })
            .ThenBy(se => se.requiredLevel);

        bool hasCharacterSpecificStars = false;
        bool hasGlobalStars = false;
        var heroItems = AccessTools.Field(typeof(UI_Lobby_Constellations_StarList), "_heroItems")
            ?.GetValue(__instance) as List<UI_Lobby_Constellations_StarItem>;
        var globalItems = AccessTools.Field(typeof(UI_Lobby_Constellations_StarList), "_globalItems")
            ?.GetValue(__instance) as List<UI_Lobby_Constellations_StarItem>;
        foreach (var star in orderedStars)
        {
            if (star.type != category)
            {
                continue;
            }

            var parent = star.heroType != null ? characterSpecificGroup : globalGroup;
            var starItem = UnityEngine.Object.Instantiate(__instance.itemPrefab, parent);
            if (star.heroType != null)
            {
                hasCharacterSpecificStars = true;
            }
            else
            {
                hasGlobalStars = true;
            }

            starItem.Setup(star, items.Count);
            items.Add(starItem);
            // Let vanilla reuse these items if Identity Change is disabled while the UI is open.
            (star.heroType != null ? heroItems : globalItems)?.Add(starItem);
        }

        seperatorObject.SetActive(hasCharacterSpecificStars && hasGlobalStars);
        characterSpecificGroup.gameObject.SetActive(hasCharacterSpecificStars);
        globalGroup.gameObject.SetActive(hasGlobalStars);
        characterSpecificTitle.gameObject.SetActive(hasCharacterSpecificStars);
        globalTitle.gameObject.SetActive(hasGlobalStars);
        globalDesc.SetActive(hasGlobalStars);
        listGroup.currentIndex = -1;
        __instance.RefreshResetStatus();
        return false;
    }
}
