using System.Collections.Generic;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;

namespace DewGemSlotCount.patch
{
    [HarmonyPatch(typeof(UI_InGame_SkillButtons))]
    public class UI_InGame_SkillButtons_Patch
    {
        // 反射获取私有字段 _selfDefaultScale
        private static readonly System.Reflection.FieldInfo selfDefaultScaleField =
            AccessTools.Field(typeof(UI_InGame_SkillButtons), "_selfDefaultScale");

        // 保存 hiddenWhenExpanded 原始位置，防止累加偏移
        private static readonly Dictionary<RectTransform, Vector3> originalPositions = new();

        [HarmonyPatch("OnStateChanged")]
        [HarmonyPrefix]
        public static bool OnStateChanged_Prefix(UI_InGame_SkillButtons __instance, EditSkillManager.ModeType mode)
        {
            if (!DewGemSlotCount.Instance.Config.OptimizeUI)
            {
                return true;
            }
            
            float layoutExpandedScale = 1.9f;

            // 使用反射读取私有字段 _selfDefaultScale 的值
            float selfDefaultScale = (float)selfDefaultScaleField.GetValue(__instance);
            float moveOffsetY = 350f; // 上移固定距离，可根据需要调整

            if (mode != EditSkillManager.ModeType.None)
            {
                // 主按钮缩放
                __instance.transform.DOKill();
                __instance.transform.DOScale(Vector3.one * __instance.selfExpandedScale, __instance.animDuration)
                    .SetUpdate(isIndependentUpdate: true);

                // 布局缩放
                for (int i = 0; i < __instance.expandedLayouts.Length; i++)
                {
                    __instance.expandedLayouts[i].localScale = Vector3.one * layoutExpandedScale;
                }

                // hiddenWhenExpanded 位移动画
                CanvasGroup[] array = __instance.hiddenWhenExpanded;
                foreach (CanvasGroup obj in array)
                {
                    RectTransform rt = obj.GetComponent<RectTransform>();

                    // 记录原始位置
                    if (!originalPositions.ContainsKey(rt))
                        originalPositions[rt] = rt.anchoredPosition;

                    Vector3 targetPos = originalPositions[rt] + Vector3.up * moveOffsetY;

                    rt.DOKill();
                    rt.DOAnchorPos(targetPos, __instance.hiddenWhenExpandedDuration).SetUpdate(isIndependentUpdate: true);
                }
            }
            else
            {
                // 主按钮恢复缩放
                __instance.transform.DOKill();
                __instance.transform.DOScale(Vector3.one * selfDefaultScale, __instance.animDuration)
                    .SetUpdate(isIndependentUpdate: true);

                // 布局恢复缩放
                for (int k = 0; k < __instance.expandedLayouts.Length; k++)
                {
                    __instance.expandedLayouts[k].localScale = Vector3.one;
                }

                // hiddenWhenExpanded 位移动画回原位
                CanvasGroup[] array = __instance.hiddenWhenExpanded;
                foreach (CanvasGroup obj in array)
                {
                    RectTransform rt = obj.GetComponent<RectTransform>();
                    if (originalPositions.ContainsKey(rt))
                    {
                        Vector3 targetPos = originalPositions[rt];
                        rt.DOKill();
                        rt.DOAnchorPos(targetPos, __instance.hiddenWhenExpandedDuration).SetUpdate(isIndependentUpdate: true);
                    }
                }
            }

            // Gamepad 焦点处理
            if (DewInput.currentMode == InputMode.Gamepad)
            {
                if (mode == EditSkillManager.ModeType.None && ManagerBase<GlobalUIManager>.instance.focused == __instance)
                {
                    ManagerBase<GlobalUIManager>.instance.SetFocus(null);
                }
                else if (mode != EditSkillManager.ModeType.None && ManagerBase<GlobalUIManager>.instance.focused == null)
                {
                    ManagerBase<GlobalUIManager>.instance.SetFocus(__instance);
                }
            }

            // 跳过原方法
            return false;
        }
    }
}
