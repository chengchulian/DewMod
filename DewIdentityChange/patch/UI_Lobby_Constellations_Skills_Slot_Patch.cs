using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DewIdentityChange.patch;

[HarmonyPatch(typeof(UI_Lobby_Constellations_Skills_Slot))]
public static class UI_Lobby_Constellations_Skills_Slot_Patch
{
    private static readonly FieldInfo HeroField =
        AccessTools.Field(typeof(UI_Lobby_Constellations_Skills_Slot), "_hero");

    private static readonly FieldInfo SkillsField =
        AccessTools.Field(typeof(UI_Lobby_Constellations_Skills_Slot), "_skills");

    private static readonly FieldInfo SkillField =
        AccessTools.Field(typeof(UI_Lobby_Constellations_Skills_Slot), "_skill");

    [HarmonyPrefix]
    [HarmonyPatch("Refresh")]
    public static bool Refresh_Prefix(UI_Lobby_Constellations_Skills_Slot __instance)
    {
        var constellations = SingletonBehaviour<UI_Lobby_Constellations>.softInstance;
        if (constellations?.loadout == null ||
            DewPlayer.local == null ||
            string.IsNullOrEmpty(DewPlayer.local.selectedHeroType))
        {
            return false;
        }

        var hero = DewResources.GetByShortTypeName<Hero>(DewPlayer.local.selectedHeroType);
        if (hero == null)
        {
            return false;
        }

        AssetRef<Hero> heroRef = hero;
        var skills = hero.GetComponent<HeroSkill>().GetLoadoutSkills(__instance.type).ToAssetRefs();
        HeroField.SetValue(__instance, heroRef);
        SkillsField.SetValue(__instance, skills);

        if (skills == null || skills.Length == 0)
        {
            SkillField.SetValue(__instance, default(AssetRef<SkillTrigger>));
            __instance.skillIcon.enabled = false;
            __instance.selectionCountText.text = "0/0";
            __instance.selectionGroupObject.SetActive(false);
            __instance.newObject.SetActive(false);
            return false;
        }

        int index = constellations.loadout.GetSkill(__instance.type);
        index = Mathf.Clamp(index, 0, skills.Length - 1);
        AssetRef<SkillTrigger> skill = skills[index];
        SkillField.SetValue(__instance, skill);

        __instance.skillIcon.enabled = true;
        __instance.skillIcon.sprite = skill.asset.configs[0].triggerIcon;
        __instance.selectionCountText.text = $"{index + 1}/{skills.Length}";
        __instance.selectionGroupObject.SetActive(skills.Length > 1);
        UpdateHasNewStatus(__instance, hero, skill);
        return false;
    }

    private static void UpdateHasNewStatus(
        UI_Lobby_Constellations_Skills_Slot slot,
        Hero hero,
        AssetRef<SkillTrigger> selectedSkill)
    {
        if (slot.type == HeroSkillLocation.Movement || hero == null || selectedSkill.asset == null)
        {
            slot.newObject.SetActive(false);
            return;
        }

        bool hasNew = false;
        foreach (var skill in hero.GetComponent<HeroSkill>().GetLoadoutSkills(slot.type))
        {
            if (skill == null || selectedSkill.asset == skill)
            {
                continue;
            }

            if (DewSave.profileMain.skills.TryGetValue(skill.GetType().Name, out var data) &&
                data.isNewHeroOrHeroSkill)
            {
                hasNew = true;
                break;
            }
        }

        slot.newObject.SetActive(hasNew);
    }
}
