using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(UI_InGame_Scoreboard_PlayerItem_Skill))]
public static class UI_InGame_Scoreboard_PlayerItem_Skill_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("UpdateInfo")]
    public static bool UpdateInfo_Prefix(UI_InGame_Scoreboard_PlayerItem_Skill __instance)
    {
        var gemObjectsField = AccessTools.Field(typeof(UI_InGame_Scoreboard_PlayerItem_Skill), "gemObjects234");
        var itemField = AccessTools.Field(typeof(UI_InGame_Scoreboard_PlayerItem_Skill), "_item");
        var skillIconField = AccessTools.Field(typeof(UI_InGame_Scoreboard_PlayerItem_Skill), "skillIcon");
        var hasSkillObjectField = AccessTools.Field(typeof(UI_InGame_Scoreboard_PlayerItem_Skill), "hasSkillObject");
        var noSkillObjectField = AccessTools.Field(typeof(UI_InGame_Scoreboard_PlayerItem_Skill), "noSkillObject");
        var multipleChargesObjectField = AccessTools.Field(typeof(UI_InGame_Scoreboard_PlayerItem_Skill), "multipleChargesObject");
        var chargeCountTextField = AccessTools.Field(typeof(UI_InGame_Scoreboard_PlayerItem_Skill), "chargeCountText");
        var activationKeyTextField = AccessTools.Field(typeof(UI_InGame_Scoreboard_PlayerItem_Skill), "activationKeyText");

        var gemObjects = (GameObject[])gemObjectsField.GetValue(__instance);
        var item = (UI_InGame_Scoreboard_PlayerItem)itemField.GetValue(__instance);
        if (item == null || item.hero == null || !item.hero.isActive) return false;

        Hero h = item.hero;
        var maxGemCount = h.Skill.GetMaxGemCount(__instance.type);

        if (gemObjects.Length < maxGemCount - 1)
        {
            Array.Resize(ref gemObjects, maxGemCount - 1);
            gemObjectsField.SetValue(__instance, gemObjects);

            for (int i = 3; i < maxGemCount - 1; i++)
            {
                int quantity = i + 2;
                gemObjects[i] = UnityEngine.Object.Instantiate(gemObjects[2], __instance.transform);
                gemObjects[i].name = $"{quantity} Gems";
                var group = gemObjects[i].transform;

                while (group.childCount < quantity)
                {
                    GameObject obj = UnityEngine.Object.Instantiate(
                        gemObjects[2].transform.GetChild(0).gameObject,
                        group, false);
                    obj.GetComponent<UI_InGame_Scoreboard_PlayerItem_Skill_Gem>().index = group.childCount - 1;
                }

                SetupDualLineLayout(group, quantity);
            }
        }

        for (int i = 0; i < gemObjects.Length; i++)
        {
            gemObjects[i].SetActive(maxGemCount == i + 2);
        }

        var skill = h.Skill.GetSkill(__instance.type);
        ((GameObject)hasSkillObjectField.GetValue(__instance)).SetActive(skill != null);
        ((GameObject)noSkillObjectField.GetValue(__instance)).SetActive(skill == null);

        if (skill != null)
        {
            ((Image)skillIconField.GetValue(__instance)).sprite = skill.configs[0].triggerIcon;
            ((GameObject)multipleChargesObjectField.GetValue(__instance)).SetActive(skill.configs[0].maxCharges > 1);
            ((TextMeshProUGUI)chargeCountTextField.GetValue(__instance)).text = skill.configs[0].maxCharges.ToString();
        }

        ((TextMeshProUGUI)activationKeyTextField.GetValue(__instance)).text =
            DewInput.GetReadableTextForCurrentMode(
                ManagerBase<ControlManager>.instance.GetSkillBinding(__instance.type));

        return false; // 阻止原始 UpdateInfo 执行
    }

    private static void SetupDualLineLayout(Transform group, int totalSlots)
    {
        int num = Mathf.CeilToInt(totalSlots / 2f);
        int slotCount = totalSlots - num;
        ArrangeLine(group, num, slotCount, 100f, 30f * (1f - totalSlots * 0.02f));
        ArrangeLine(group, 0, num, -20f, 30f * (1f - totalSlots * 0.02f));
    }

    private static void ArrangeLine(Transform group, int startIndex, int slotCount, float yPos, float spacing)
    {
        if (slotCount <= 0) return;
        float offset = -((slotCount - 1) * spacing / 2f);
        for (int i = 0; i < slotCount; i++)
        {
            int index = startIndex + i;
            if (index < group.childCount)
            {
                group.GetChild(index).localPosition = new Vector3(offset + i * spacing, yPos, 0f);
            }
        }
    }
}