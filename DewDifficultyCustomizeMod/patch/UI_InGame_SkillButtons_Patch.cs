using DG.Tweening;
using HarmonyLib;
using UnityEngine;

namespace DewDifficultyCustomizeMod.patch
{
    [HarmonyPatch(typeof(UI_InGame_SkillButtons))]
    public static class UI_InGame_SkillButtons_Patch
    {
        private static bool _isAnimating = false;
        private static float _layoutExpandedScale = 1.9f;
        [HarmonyPatch(typeof(UI_InGame_SkillButtons), "OnStateChanged")]
        [HarmonyPrefix]
        static bool OnStateChanged_Prefix(UI_InGame_SkillButtons __instance, EditSkillManager.ModeType mode)
        {
            float selfDefaultScale = (float)AccessTools.Field(typeof(UI_InGame_SkillButtons), "_selfDefaultScale")
                .GetValue(__instance);

            if (mode != 0)
            {
                __instance.transform.DOScale(Vector3.one * __instance.selfExpandedScale, __instance.animDuration)
                    .SetUpdate(isIndependentUpdate: true);
                for (int i = 0; i < __instance.expandedLayouts.Length; i++)
                {
                    __instance.expandedLayouts[i].localScale = Vector3.one * _layoutExpandedScale;
                }

                CanvasGroup[] array = __instance.hiddenWhenExpanded;
                if (_isAnimating)
                {
                    return false;
                }

                for (int j = 0; j < array.Length; j++)
                {
                    RectTransform component = array[j].GetComponent<RectTransform>();
                    component.DOScale(Vector3.one * __instance.selfExpandedScale, __instance.animDuration)
                        .SetUpdate(isIndependentUpdate: true);
                    if (component != null)
                    {
                        component.DOKill(complete: true);
                        component.DOAnchorPosY(
                            component.anchoredPosition.y +
                            component.parent.GetComponent<RectTransform>().rect.height * 3f,
                            __instance.hiddenWhenExpandedDuration).SetUpdate(isIndependentUpdate: true);
                    }
                }

                _isAnimating = true;
            }
            else
            {
                __instance.transform.DOScale(Vector3.one * selfDefaultScale, __instance.animDuration)
                    .SetUpdate(isIndependentUpdate: true);
                for (int k = 0; k < __instance.expandedLayouts.Length; k++)
                {
                    __instance.expandedLayouts[k].localScale = Vector3.one;
                }

                CanvasGroup[] array2 = __instance.hiddenWhenExpanded;
                for (int l = 0; l < array2.Length; l++)
                {
                    RectTransform component2 = array2[l].GetComponent<RectTransform>();
                    component2.DOScale(Vector3.one * selfDefaultScale, __instance.animDuration)
                        .SetUpdate(isIndependentUpdate: true);
                    if (component2 != null && _isAnimating)
                    {
                        component2.DOKill(complete: true);
                        component2.DOAnchorPosY(
                            component2.anchoredPosition.y -
                            component2.parent.GetComponent<RectTransform>().rect.height * 3f,
                            __instance.hiddenWhenExpandedDuration).SetUpdate(isIndependentUpdate: true);
                    }
                }

                _isAnimating = false;
            }

            if (DewInput.currentMode == InputMode.Gamepad)
            {
                if (mode == EditSkillManager.ModeType.None &&
                    (UI_InGame_SkillButtons)ManagerBase<GlobalUIManager>.instance.focused == __instance)
                {
                    ManagerBase<GlobalUIManager>.instance.SetFocus(null);
                }
                else if (mode != 0 && ManagerBase<GlobalUIManager>.instance.focused == null)
                {
                    ManagerBase<GlobalUIManager>.instance.SetFocus(__instance);
                }
            }


            // 跳过原方法
            return false;
        }
    }
}