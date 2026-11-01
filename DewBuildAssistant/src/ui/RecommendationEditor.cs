using System;
using System.Collections.Generic;
using System.Linq;
using DewBuildAssistant.config;
using DewBuildAssistant.controller;
using DewBuildAssistant.model;
using DewBuildAssistant.util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static DewBuildAssistant.ui.BuildAssistantView;

namespace DewBuildAssistant.ui;

// 左侧保留图鉴候选网格，右侧使用游戏 QWERD 技能栏，每技能四个精粹位。
internal sealed class RecommendationEditor : MonoBehaviour
{
    private BuildAssistantView _view;
    private BuildRecommendations _build;
    private Action _closed;
    private RectTransform _content;
    private RectTransform _candidates;
    private readonly List<GameObject> _rows = new();
    private readonly List<GameObject> _candidateRows = new();
    private TMP_InputField _search;
    private TMP_Text _preview;
    private GuideItem _previewItem;
    private Button _memoryTab;
    private Button _gemTab;
    private ScrollRect _candidateScroll;
    private List<GuideItem> _memories;
    private List<GuideItem> _gems;
    private bool _showGems;
    private float _searchAt = -1;
    private bool _refreshWorkspace;
    private RectTransform _ghost;
    private GameObject _trash;
    public RecommendationDrag Dragging { get; private set; }
    public BuildRecommendations Recommendations => _build;
    private static string T(string key, params object[] args) => LocalizationSource.GetLocalizationText(key, args);

    public static void Open(BuildAssistantView view, Transform parent, BuildRecommendations build, Action closed = null)
    {
        BuildExporter.FillCurrentMemorySkills(build);
        var root = Panel(parent, "Recommendation editor", new Color(0, 0, 0, 0.82f));
        var editor = root.gameObject.AddComponent<RecommendationEditor>();
        editor._view = view;
        editor._build = build;
        editor._closed = closed;
        editor.Build();
    }

    private void Build()
    {
        var window = Panel(transform, "Editor", GameUiTheme.Background);
        Place(window, new Vector2(0.12f, 0.17f), new Vector2(0.88f, 0.83f));
        var heading = _view.Text(window, () => T("SkillBuild"), 22);
        Place(heading.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(16, -43), new Vector2(-110, -9));
        var done = _view.Button(window, () => T("Done"), Close, GameUiTheme.Accent);
        Place((RectTransform)done.transform, Vector2.one, Vector2.one, new Vector2(-82, -48), new Vector2(-14, -12));

        _memoryTab = _view.Button(window, () => T("CandidateMemories"), () => SelectTab(false), GameUiTheme.Accent);
        Place((RectTransform)_memoryTab.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -88), new Vector2(148, -52));
        _gemTab = _view.Button(window, () => T("CandidateGems"), () => SelectTab(true), GameUiTheme.Surface);
        Place((RectTransform)_gemTab.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(154, -88), new Vector2(288, -52));
        _search = _view.InputField(window, () => T("SearchResources"), 128, false);
        Place((RectTransform)_search.transform, new Vector2(0, 1), new Vector2(0.38f, 1), new Vector2(16, -140), new Vector2(-12, -104));
        _search.onValueChanged.AddListener(_ => _searchAt = Time.unscaledTime + 0.2f);
        _candidateScroll = _view.Scroll(window, "Catalogue candidates", out _candidates);
        Place((RectTransform)_candidateScroll.transform, Vector2.zero, new Vector2(0.38f, 1), new Vector2(16, 52), new Vector2(-12, -152));
        var catalogue = _candidates.gameObject.AddComponent<VerticalLayoutGroup>();
        catalogue.spacing = 10;
        catalogue.padding = new RectOffset(4, 4, 4, 4);
        catalogue.childControlWidth = catalogue.childControlHeight = true;
        catalogue.childForceExpandWidth = true;
        catalogue.childForceExpandHeight = false;
        _candidates.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _preview = _view.Text(window, () => _previewItem == null ? "" : BuildExporter.ResourceDisplayName(_previewItem.ResourceHint), 14);
        _preview.color = GameUiTheme.Muted;
        _preview.overflowMode = TextOverflowModes.Ellipsis;
        Place(_preview.rectTransform, Vector2.zero, new Vector2(0.38f, 0), new Vector2(16, 16), new Vector2(-12, 40));

        _content = Rect(window, "Build workspace");
        Place(_content, new Vector2(0.38f, 0), Vector2.one, new Vector2(12, 68), new Vector2(-16, -64));
        var workspace = _content.gameObject.AddComponent<VerticalLayoutGroup>();
        workspace.childControlWidth = workspace.childControlHeight = true;
        workspace.childForceExpandHeight = false;
        workspace.childAlignment = TextAnchor.MiddleCenter;
        var trash = Panel(window, "Remove dropped gem", GameUiTheme.Surface);
        Place(trash, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-188, 16), new Vector2(-16, 52));
        GameUiTheme.Apply(trash.GetComponent<Image>());
        var trashText = _view.Text(trash, () => T("Remove"), 14);
        trashText.alignment = TextAlignmentOptions.Center;
        var target = trash.gameObject.AddComponent<RecommendationDrop>();
        target.Editor = this;
        target.Trash = true;
        target.Accept = d => RecommendationEditing.Remove(d.SourceSkill, d.SourceGemSlot, d.Item);
        _trash = trash.gameObject;
        _trash.SetActive(false);
        SelectTab(false);
        DrawWorkspace();
    }

    private void SelectTab(bool gems)
    {
        EndItemDrag();
        _showGems = gems;
        GameUiTheme.StyleButton(_memoryTab, gems ? GameUiTheme.Surface : GameUiTheme.Accent);
        GameUiTheme.StyleButton(_gemTab, gems ? GameUiTheme.Accent : GameUiTheme.Surface);
        _search.SetTextWithoutNotify("");
        _searchAt = -1;
        DrawCandidates();
        _candidateScroll.verticalNormalizedPosition = 1;
    }

    private void DrawCandidates()
    {
        Clear(_candidateRows);
        var options = _showGems ? (_gems ??= BuildExporter.RecommendationOptions(true)) : (_memories ??= BuildExporter.RecommendationOptions(false));
        string search = _search.text.Trim();
        var filtered = options.Where(i => i.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || i.ResourceHint.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        foreach (var group in RecommendationCatalog.Groups(filtered, _showGems))
            _candidateRows.Add(DrawCandidateGroup(group, _candidates));
        LayoutRebuilder.ForceRebuildLayoutImmediate(_candidates);
    }

    // 稀有度标题位于顶层；英雄子分类保留在对应稀有度下，避免稀有记忆混入通用区。
    private GameObject DrawCandidateGroup(RecommendationGroup group, Transform parent)
    {
        var section = BuildGuideCards.Vertical(parent, "Catalogue category");
        var heading = _view.Text(section, () => group.Title, group.HeroKey == null ? 14 : 13);
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
        if (group.Rarity.HasValue) heading.color = Dew.GetRarityColor(group.Rarity.Value);
        if (group.Items.Count > 0)
        {
            var icons = Rect(section, "Category candidates");
            var grid = icons.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(76, 84);
            grid.spacing = new Vector2(6, 6);
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            foreach (var item in group.Items)
            {
                var tile = BuildGuideCards.CompactTile(_view, icons, item, () => Preview(item));
                if (_showGems || RecommendationEditing.CanDragSkill(item))
                {
                    var drag = tile.gameObject.AddComponent<RecommendationDrag>();
                    drag.Editor = this;
                    drag.Item = item;
                    drag.IsGem = _showGems;
                }
            }
        }
        foreach (var child in group.Children) DrawCandidateGroup(child, section);
        return section.gameObject;
    }

    public void FocusCandidates(bool gems) => SelectTab(gems);

    private void DrawWorkspace()
    {
        Clear(_rows);
        _rows.Add(SkillBuildLayout.Create(_view, _content, _build, this, maxScale: 0.88f).gameObject);
    }
    public void Preview(GuideItem item)
    {
        _previewItem = item;
        _preview.GetComponent<LocalizedTextBinding>().OnLanguageChanged();
    }

    public void RefreshLanguage()
    {
        EndItemDrag();
        _memories = _gems = null;
        foreach (var entry in _build.Skills)
        {
            if (entry.Skill != null) entry.Skill.Name = BuildExporter.ResourceDisplayName(entry.Skill.ResourceHint);
            foreach (var gem in entry.GemSlots.Where(g => g != null)) gem.Gem.Name = BuildExporter.ResourceDisplayName(gem.Gem.ResourceHint);
        }
        DrawCandidates();
        DrawWorkspace();
    }

    public void BeginItemDrag(RecommendationDrag source, PointerEventData data)
    {
        EndItemDrag();
        Dragging = source;
        if (_trash != null) _trash.SetActive(source.SourceSkill != null);
        _ghost = Panel(transform, "Drag preview", GameUiTheme.Accent);
        _ghost.anchorMin = _ghost.anchorMax = _ghost.pivot = new Vector2(0.5f, 0.5f);
        _ghost.sizeDelta = new Vector2(48, 48);
        var image = _ghost.GetComponent<Image>();
        image.sprite = BuildGuideCards.ResourceSprite(source.Item.ResourceHint);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = BuildGuideCards.ResourceColor(source.Item.ResourceHint);
        image.raycastTarget = false;
        var group = _ghost.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        MoveItemDrag(data);
    }
    public void MoveItemDrag(PointerEventData data)
    {
        if (_ghost == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, data.position, data.pressEventCamera, out var point);
        _ghost.anchoredPosition = point;
    }
    public void EndItemDrag()
    {
        Dragging = null;
        if (_trash != null) _trash.SetActive(false);
        foreach (var drop in GetComponentsInChildren<RecommendationDrop>()) drop.ResetColor();
        if (_ghost != null) { _ghost.gameObject.SetActive(false); Destroy(_ghost.gameObject); }
        _ghost = null;
    }
    public void RefreshWorkspace() => _refreshWorkspace = true;
    private void Update()
    {
        if (Dragging != null) return;
        if (_refreshWorkspace) { _refreshWorkspace = false; DrawWorkspace(); }
        if (_searchAt >= 0 && Time.unscaledTime >= _searchAt) { _searchAt = -1; DrawCandidates(); }
    }
    public bool CloseFromBack()
    {
        if (Dragging != null) { EndItemDrag(); return true; }
        Close();
        return true;
    }
    private void Close() { EndItemDrag(); gameObject.SetActive(false); Destroy(gameObject); _closed?.Invoke(); }
    private void OnDisable() => EndItemDrag();
    private void Clear(List<GameObject> objects)
    {
        foreach (var obj in objects) { obj.SetActive(false); Destroy(obj); }
        objects.Clear();
    }
}
