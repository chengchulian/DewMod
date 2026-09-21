using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(HeroLoadoutData))]
public static class HeroLoadoutData_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("Validate_Imp", new[]
    {
        typeof(string),
        typeof(bool),
        typeof(bool),
        typeof(DewProfile.HeroStarSlotUnlockData)
    })]
    public static bool Validate_Imp_Prefix(
        HeroLoadoutData __instance,
        string heroType,
        bool isRepair,
        bool checkStarLevels,
        DewProfile.HeroStarSlotUnlockData unlockedSlots,
        ref bool __result)
    {
        if (DewIdentityChange.Instance?.IsIdentityEnabled != true)
        {
            return true;
        }

        bool isValid = true;
        Hero hero = DewResources.GetByShortTypeName<Hero>(heroType);
        if (hero == null)
        {
            __result = false;
            return false;
        }

        HeroSkill skill = hero.GetComponent<HeroSkill>();
        CheckSkill(ref __instance.skillQ, skill.loadoutQ.Values());
        CheckSkill(ref __instance.skillR, skill.loadoutR.Values());
        CheckSkill(ref __instance.skillTrait, skill.loadoutTrait.Values());
        CheckSkill(ref __instance.skillMovement, skill.loadoutMovement.Values());
        if (!isValid && !isRepair)
        {
            __result = false;
            return false;
        }

        ValidateConstellation(StarType.Destruction, ref __instance.cDestruction);
        ValidateConstellation(StarType.Life, ref __instance.cLife);
        ValidateConstellation(StarType.Imagination, ref __instance.cImagination);
        ValidateConstellation(StarType.Flexible, ref __instance.cFlexible);

        __result = isValid;
        return false;

        void CheckSkill(ref int current, SkillTrigger[] skills)
        {
            if (current >= 0 && current < skills.Length)
            {
                return;
            }

            if (isRepair)
            {
                current = skills.Length > 0 ? Mathf.Clamp(current, 0, skills.Length - 1) : 0;
            }

            isValid = false;
        }

        void ValidateConstellation(StarType type, ref List<LoadoutStarItem> stars)
        {
            if (stars == null)
            {
                isValid = false;
                if (!isRepair)
                {
                    return;
                }

                stars = new List<LoadoutStarItem>();
            }

            HeroConstellationSettings settings = hero.GetConstellationSettings(type);
            if (isRepair)
            {
                while (stars.Count < settings.maxCount)
                {
                    stars.Add(default);
                    isValid = false;
                }

                while (stars.Count > settings.maxCount)
                {
                    stars.RemoveAt(stars.Count - 1);
                    isValid = false;
                }
            }
            else if (stars.Count != settings.maxCount)
            {
                isValid = false;
                return;
            }

            var usedStars = new HashSet<string>();
            for (int i = 0; i < stars.Count; i++)
            {
                if (string.IsNullOrEmpty(stars[i].name))
                {
                    continue;
                }

                if (!usedStars.Add(stars[i].name))
                {
                    isValid = false;
                    if (!isRepair)
                    {
                        break;
                    }

                    stars[i] = default;
                    continue;
                }

                StarEffect starPrefab = DewResources.GetByShortTypeName<StarEffect>(stars[i].name);
                if (starPrefab == null)
                {
                    isValid = false;
                    if (!isRepair)
                    {
                        break;
                    }

                    stars[i] = default;
                    continue;
                }

                if (unlockedSlots != null)
                {
                    List<int> slots = unlockedSlots.Get(type);
                    if (i >= settings.defaultCount && !slots.Contains(i))
                    {
                        isValid = false;
                        if (!isRepair)
                        {
                            break;
                        }

                        stars[i] = default;
                        continue;
                    }
                }

                if (checkStarLevels &&
                    starPrefab.type != StarType.Flexible &&
                    (stars[i].level < 1 || stars[i].level > starPrefab.maxStarLevel))
                {
                    isValid = false;
                    if (!isRepair)
                    {
                        break;
                    }

                    LoadoutStarItem temp = stars[i];
                    temp.level = Mathf.Clamp(temp.level, 1, starPrefab.maxStarLevel);
                    stars[i] = temp;
                }
            }
        }
    }
}
