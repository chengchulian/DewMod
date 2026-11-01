using System;
using System.Runtime.CompilerServices;
using DG.Tweening;
using UnityEngine;

namespace DewGemSlotCount.ui;

public static class MovementEditLayout
{
    private sealed class LayoutState
    {
        public CanvasGroup canvas;
        public RectTransform content;
        public Vector3 position;
        public Tween moveTween;
        public bool expanded;
    }

    private static readonly ConditionalWeakTable<UI_InGame_SkillButtons, LayoutState> States = new();

    // 统一界面与手柄导航的显隐条件，避免选择到隐藏的位移技能。
    public static bool ShouldShowMovement(EditSkillManager.ModeType mode)
    {
        var mod = DewGemSlotCount.Instance;
        Hero hero = DewPlayer.local == null ? null : DewPlayer.local.hero;
        var edit = ManagerBase<EditSkillManager>.softInstance;
        bool canAddMovementSlot = mod != null && mod.GameplayConfig.AllowMovementCorruptedChaos &&
                                  mode == EditSkillManager.ModeType.EditSkillShrine && edit != null &&
                                  edit.currentProvider is Shrine_CorruptedChaos;
        return mod != null && mod.Config.OptimizeUI && hero != null &&
                    mode != EditSkillManager.ModeType.None &&
                    (mod.GameplayConfig.EditMovementSkill || canAddMovementSlot ||
                     hero.Skill.GetMaxGemCount(HeroSkillLocation.Movement) > 0);
    }

    // 沿用历史版本：位移保留原本横向位置和缩放，仅将原容器上移 350 个 UI 单位。
    public static void Apply(UI_InGame_SkillButtons ui, EditSkillManager.ModeType mode)
    {
        if (!ShouldShowMovement(mode))
        {
            Restore(ui);
            return;
        }

        if (!States.TryGetValue(ui, out LayoutState state))
        {
            UI_InGame_SkillButton button = Array.Find(ui.skillButtons, b => b.skillType == HeroSkillLocation.Movement);
            if (button == null) return;
            CanvasGroup canvas = Array.Find(ui.hiddenWhenExpanded, c => button.transform.IsChildOf(c.transform));
            if (canvas == null) return;
            var content = (RectTransform)canvas.transform;
            state = new LayoutState
            {
                canvas = canvas, content = content, position = content.anchoredPosition3D
            };
            States.Add(ui, state);
        }

        if (!state.expanded)
        {
            state.expanded = true;
            state.moveTween?.Kill();
            state.moveTween = state.content.DOAnchorPos3D(state.position + Vector3.up * 350f,
                ui.hiddenWhenExpandedDuration).SetUpdate(isIndependentUpdate: true);
        }
        state.canvas.DOKill();
        state.canvas.alpha = 1f;
        state.canvas.interactable = true;
        state.canvas.blocksRaycasts = true;
    }

    // 退回普通 HUD 或关闭优化时恢复精确原位，避免多次开关造成累计偏移。
    public static void Restore(UI_InGame_SkillButtons ui)
    {
        if (!States.TryGetValue(ui, out LayoutState state) || !state.expanded) return;
        state.expanded = false;
        state.moveTween?.Kill();
        state.moveTween = null;
        state.content.anchoredPosition3D = state.position;
        state.canvas.DOKill();
        var edit = ManagerBase<EditSkillManager>.softInstance;
        bool editing = edit != null && edit.mode != EditSkillManager.ModeType.None;
        state.canvas.alpha = editing ? 0f : 1f;
        state.canvas.interactable = !editing;
        state.canvas.blocksRaycasts = !editing;
    }
}
