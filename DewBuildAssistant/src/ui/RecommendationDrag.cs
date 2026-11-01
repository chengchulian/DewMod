using System;
using DewBuildAssistant.model;
using DewBuildAssistant.util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DewBuildAssistant.ui;

// 拖动源独立于图鉴行为；不会标记图鉴已读或触发游戏配装命令。
internal sealed class RecommendationDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler
{
    public RecommendationEditor Editor;
    public GuideItem Item;
    public bool IsGem;
    public SkillBuild SourceSkill;
    public int SourceGemSlot = -1;

    public void OnBeginDrag(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left) return;
        // 身份记忆固定在 D；其下方精粹仍可拖动。
        if (!IsGem && !RecommendationEditing.CanDragSkill(Item, SourceSkill)) return;
        data.eligibleForClick = false;
        Editor.BeginItemDrag(this, data);
    }
    public void OnDrag(PointerEventData data) => Editor.MoveItemDrag(data);
    public void OnEndDrag(PointerEventData data) => Editor.EndItemDrag();
    public void OnPointerEnter(PointerEventData data) => Editor.Preview(Item);
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Right && SourceSkill != null)
        { if (RecommendationEditing.Remove(SourceSkill, SourceGemSlot, Item)) Editor.RefreshWorkspace(); }
        else Editor.Preview(Item);
    }
}

internal sealed class RecommendationDrop : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public RecommendationEditor Editor;
    public bool AcceptsGems;
    public bool AcceptsSkillsAndGems;
    public bool Trash;
    public Func<RecommendationDrag, bool> Accept;
    private Image _image;
    private Color _color;

    private void Awake() { _image = GetComponent<Image>(); if (_image != null) _color = _image.color; }
    public void OnDrop(PointerEventData data)
    {
        var drag = Editor.Dragging;
        if (drag == null || (!Trash && !AcceptsSkillsAndGems && drag.IsGem != AcceptsGems)) return;
        bool accepted = Accept(drag);
        if (accepted) Editor.RefreshWorkspace();
        ResetColor();
    }
    public void OnPointerEnter(PointerEventData data)
    {
        if (Editor.Dragging != null && (Trash || AcceptsSkillsAndGems || Editor.Dragging.IsGem == AcceptsGems) && _image != null)
            _image.color = GameUiTheme.Accent;
    }
    public void OnPointerExit(PointerEventData data) => ResetColor();
    private void OnDisable() => ResetColor();
    public void ResetColor() { if (_image != null) _image.color = _color; }
}
