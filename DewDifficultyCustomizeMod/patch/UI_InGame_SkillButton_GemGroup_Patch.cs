using System;
using System.Collections;
using DewDifficultyCustomizeMod.util;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch;

[HarmonyPatch(typeof(UI_InGame_SkillButton_GemGroup))]
public static class UI_InGame_SkillButton_GemGroup_Patch
{
    
    private static readonly float DualLineUpY = 100f;
    private static readonly float DualLineDownY = -100f;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UI_InGame_SkillButton_GemGroup), "OnStateChanged")]
    public static bool OnStateChanged_Prefix(UI_InGame_SkillButton_GemGroup __instance, EditSkillManager.ModeType mode)
    {
        if (__instance == null)
        {
            return false;
        }
        
        
        AddGemCountUI(__instance);
        

        var transform = __instance.transform;
        var type = typeof(UI_InGame_SkillButton_GemGroup);

        // 反射私有字段
        var cg = AccessTools.Field(type, "_cg").GetValue(__instance) as CanvasGroup;
        var defaultScale = (float)AccessTools.Field(type, "_gemGroupDefaultScale").GetValue(__instance);

        float duration = __instance.gemGroupAnimDuration;

        if (mode == EditSkillManager.ModeType.None)
        {
            transform.DOScale(defaultScale * Vector3.one, duration).SetUpdate(true);

            if (__instance.enableFade)
                cg.DOFade(0f, duration).SetUpdate(true);

            cg.interactable = !__instance.interactableOnlyWhileEditing;
            cg.blocksRaycasts = !__instance.interactableOnlyWhileEditing;


            if (__instance.groups.Length > 4)
            {
                for (int i = 4; i < __instance.groups.Length; i++)
                {
                    SmoothIncreaseOfSpacing(__instance.groups[i], duration);
                }
            }
        }
        else
        {
            transform.DOScale(__instance.expandedGemGroupScale * Vector3.one, duration).SetUpdate(true);

            if (__instance.enableFade)
                cg.DOFade(1f, duration).SetUpdate(true);

            cg.interactable = true;
            cg.blocksRaycasts = true;


            if (__instance.groups.Length > 4)
            {
                for (int i = 4; i < __instance.groups.Length; i++)
                {
                    SmoothReductionOfSpacing(__instance.groups[i], duration);
                }
            }
        }

        return false; // 阻止原始方法执行
    }
    

    private static void AddGemCountUI(UI_InGame_SkillButton_GemGroup instance)
    {
        var maxGemCount = AttrCustomizeConstant.MaxGemCount;
        if (instance.groups.Length >= maxGemCount)
        {
            return;
        }
        Array.Resize(ref instance.groups, maxGemCount);
        for (int i = 4; i < maxGemCount; i++)
        {
            instance.groups[i] = UnityEngine.Object.Instantiate(instance.groups[3], instance.transform);
            instance.groups[i].name = GetEnglishByNum(i + 1);
            Transform group = instance.groups[i].transform;
            int num = i + 1;
            while (group.childCount < num)
            {
                GameObject obj = UnityEngine.Object.Instantiate(
                    instance.groups[0].transform.GetChild(0).gameObject, group, false);
                obj.GetComponent<UI_InGame_GemSlot>().slotIndex = group.childCount - 1;
            }

            SetupDualLineLayout(group, num);
        }

        var rectTransform = instance.transform as RectTransform;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
    }

    private static string GetEnglishByNum(int number)
    {
        return number switch
        {
            1 => "One",
            2 => "Two",
            3 => "Three",
            4 => "Four",
            5 => "Five",
            6 => "Six",
            7 => "Seven",
            8 => "Eight",
            9 => "Nine",
            10 => "Ten",
            11 => "Eleven",
            12 => "Twelve",
            _ => number.ToString(),
        };
    }

    private static void SetupDualLineLayout(Transform group, int totalSlots)
    {
        int num = Mathf.CeilToInt((float)totalSlots / 2f);
        int slotCount = totalSlots - num;
        ArrangeLine(group, num, slotCount, DualLineUpY, 50f * (1f - totalSlots * 0.02f));
        ArrangeLine(group, 0, num, DualLineDownY, 50f * (1f - totalSlots * 0.02f));
    }

    private static void ArrangeLine(Transform group, int startIndex, int slotCount, float yPos, float spacing)
    {
        if (slotCount <= 0) return;

        float startX = -(slotCount - 1) * spacing / 2f;
        for (int i = 0; i < slotCount; i++)
        {
            int index = startIndex + i;
            if (index < group.childCount)
            {
                group.GetChild(index).localPosition = new Vector3(startX + i * spacing, yPos, 0f);
            }
        }
    }

    private static void SmoothIncreaseOfSpacing(GameObject group, float duration)
    {
        int totalSlots = group.transform.childCount;
        int num = Mathf.CeilToInt((float)totalSlots / 2f);
        int slotCount = totalSlots - num;
        SmoothMoveY(group, 0, num, DualLineDownY, duration);
        SmoothMoveY(group, num, totalSlots, DualLineUpY, duration);
    }


    private static void SmoothReductionOfSpacing(GameObject group, float duration)
    {
        int totalSlots = group.transform.childCount;
        int num = Mathf.CeilToInt((float)totalSlots / 2f);
        int slotCount = totalSlots - num;
        SmoothMoveY(group, 0, num, DualLineDownY + 20, duration);
        SmoothMoveY(group, num, totalSlots, DualLineUpY - 20, duration);
    }

    private static void SmoothMoveY(GameObject group, int startIndex, int endIndex, float value, float duration)
    {
        for (int i = startIndex; i < endIndex; i++)
        {
            var child = group.transform.GetChild(i) as RectTransform;
            child.DOKill(complete: true);
            child.DOLocalMoveY(value, duration)
                .SetUpdate(isIndependentUpdate: true);
        }
    }


    private static UI_InGame_SkillButton GetButton(this UI_InGame_SkillButton_GemGroup instance)
    {
        return instance.transform.parent.GetComponentInChildren<UI_InGame_SkillButton>();
    }
}