using System;
using System.Collections.Generic;
using DewBuildAssistant.config;
using DewBuildAssistant.controller;
using DewBuildAssistant.model;
using DewBuildAssistant.util;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DewBuildAssistant.ui;

internal sealed class BuildAssistantView : MonoBehaviour
{
    private GameObject _modal;
    private GameObject _launcher;
    private RectTransform _list;
    private TMP_Text _target;
    private TMP_Text _detail;
    private TMP_Text _guideHeading;
    private TMP_Text _count;
    private int _guideCount;
    private bool _listLoading;
    private string _targetHero;
    private int _targetSlot;
    private Button _apply;
    private Button _refresh;
    private Button _upload;
    private Button _filterButton;
    private Button _needButton;
    private GameObject _filterMenu;
    private GameObject _uploadDialog;
    private TMP_InputField _uploadTitle;
    private bool _uploadTitleEdited;
    private TMP_InputField _uploadDescription;
    private RectTransform _uploadBuildContent;
    private GameObject _uploadBuildPreview;
    private BuildRecommendations _recommendations;
    private GuideData _renderedGuide;
    private RectTransform _detailContent;
    private readonly List<GameObject> _detailCards = new();
    private ScrollRect _detailScroll;
    private TMP_FontAsset _font;
    private BackHandler _backHandler;
    private LauncherDragHandle _launcherDrag;
    private Action<GuideSummary> _select;
    private readonly List<GameObject> _rows = new();
    public bool IsOpen => _modal != null && _modal.activeSelf;
    public Func<string> UploadDraftNotice = () => "";

    private static Color Background => GameUiTheme.Background;
    private static Color Surface => GameUiTheme.Surface;
    private static Color Accent => GameUiTheme.Accent;
    private static string T(string key, params object[] args) => LocalizationSource.GetLocalizationText(key, args);

    // 使用 uGUI 的射线与滚动系统，窗口打开时遮罩接收点击，避免穿透到大厅控件。
    public static BuildAssistantView Create(Transform owner, Action toggle, Action refresh, Action apply,
        Action openWorkshop, Action<GuideSummary> select, Action<string, string, BuildRecommendations> upload,
        WorkshopFilter filter, Action<WorkshopFilter> filterChanged, Func<BuildRecommendations> getRecommendations, Action applyNeeds)
    {
        var root = new GameObject("BD Assistant", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(owner, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var view = root.AddComponent<BuildAssistantView>();
        view._select = select;
        GameUiTheme.Capture();
        view.FindFont();
        view.Build(toggle, refresh, apply, openWorkshop, upload, filter, filterChanged, getRecommendations, applyNeeds);
        view._backHandler = ManagerBase<GlobalUIManager>.softInstance?.AddBackHandler(view, 9000, view.CloseTopPopup);
        return view;
    }

    private void FindFont()
    {
        // 游戏正文包含 SC/仿宋回退链；星座窗口的首个文本可能是纯数字字体。
        _font = ManagerBase<GlobalUIManager>.softInstance?.fontBody;
        if (_font != null) return;

        // 管理器尚未就绪时，仅选实际支持中文的已加载字体。
        foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (font != null && font.HasCharacter('助', true, true) && font.HasCharacter('忆', true, true))
            {
                _font = font;
                return;
            }
        }
        _font = TMP_Settings.defaultFontAsset;
    }

    private void Build(Action toggle, Action refresh, Action apply, Action openWorkshop, Action<string, string, BuildRecommendations> upload,
        WorkshopFilter filter, Action<WorkshopFilter> filterChanged, Func<BuildRecommendations> getRecommendations, Action applyNeeds)
    {
        _launcher = Button(transform, "", () => { if (!_launcherDrag.IgnoreClick) toggle(); }, Accent).gameObject;
        _launcher.name = "BD Assistant launcher";
        GameUiTheme.LauncherIcon(_launcher.transform);
        _launcherDrag = _launcher.AddComponent<LauncherDragHandle>();
        var launcherRect = (RectTransform)_launcher.transform;
        launcherRect.anchorMin = launcherRect.anchorMax = new Vector2(1, 1);
        launcherRect.pivot = new Vector2(1, 1);
        launcherRect.anchoredPosition = new Vector2(-28, -24);
        launcherRect.sizeDelta = new Vector2(44, 44);

        _modal = Panel(transform, "Modal", new Color(0, 0, 0, 0.58f)).gameObject;
        var window = Panel(_modal.transform, "Window", Background);
        Place(window, new Vector2(0.075f, 0.10f), new Vector2(0.925f, 0.90f));
        var header = Text(window, () => T("Title"), 24);
        if (ManagerBase<GlobalUIManager>.softInstance?.fontHeading != null) header.font = ManagerBase<GlobalUIManager>.softInstance.fontHeading;
        Place(header.rectTransform, new Vector2(0, 1), new Vector2(0.6f, 1), new Vector2(16, -47), new Vector2(-20, -10));
        var close = Button(window, () => T("Close"), toggle, Surface);
        Place((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-78, -48), new Vector2(-14, -12));

        _target = Text(window, () => _targetHero == null ? T("NotInLobby") :
            T("Target", DewLocalization.GetUIValue(_targetHero + "_Name"), _targetSlot + 1), 15);
        Place(_target.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -78), new Vector2(-14, -54));
        _count = Text(window, () => _listLoading ? T("LoadingList") : T("GuideCount", _guideCount), 16);
        _count.color = new Color(0.6f, 0.73f, 0.80f);
        Place(_count.rectTransform, new Vector2(0, 1), new Vector2(0.26f, 1), new Vector2(16, -154), new Vector2(-6, -132));

        // 工具栏独立于两个滚动区，滚动详情时头部与操作按钮始终固定。
        var listHeader = ActionRow(window, "List header", TextAnchor.MiddleLeft);
        Place(listHeader, new Vector2(0, 1), new Vector2(0.26f, 1), new Vector2(14, -124), new Vector2(-6, -88));
        BuildFilter(window, listHeader, filter, filterChanged);
        _refresh = Button(listHeader, () => T("Refresh"), refresh, Surface);
        CompactButton(_refresh);
        var detailHeader = ActionRow(window, "Detail header", TextAnchor.MiddleRight);
        Place(detailHeader, new Vector2(0.26f, 1), Vector2.one, new Vector2(6, -124), new Vector2(-14, -88));
        _guideHeading = Text(detailHeader, "", 16);
        _guideHeading.alignment = TextAlignmentOptions.MidlineLeft;
        _guideHeading.textWrappingMode = TextWrappingModes.NoWrap;
        _guideHeading.overflowMode = TextOverflowModes.Ellipsis;
        var headingSize = _guideHeading.gameObject.AddComponent<LayoutElement>();
        headingSize.minWidth = headingSize.preferredWidth = 0;
        headingSize.flexibleWidth = 1;
        headingSize.preferredHeight = 36;

        var listScroll = Scroll(window, "Guide list", out _list);
        Place((RectTransform)listScroll.transform, new Vector2(0, 0), new Vector2(0.26f, 1), new Vector2(14, 64), new Vector2(-6, -158));
        var layout = _list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 5, 5);
        layout.spacing = 4;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        _list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _detailScroll = Scroll(window, "Details", out var content);
        _detailContent = content;
        Place((RectTransform)_detailScroll.transform, new Vector2(0.26f, 0), new Vector2(1, 1), new Vector2(6, 64), new Vector2(-14, -132));
        var detailLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        detailLayout.padding = new RectOffset(10, 10, 8, 8);
        detailLayout.spacing = 6;
        detailLayout.childControlWidth = detailLayout.childControlHeight = true;
        detailLayout.childForceExpandHeight = false;
        _detail = Text(content, "", 16);
        _detail.text = T("ChooseGuide");
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _apply = Button(window, () => T("Apply"), apply, Accent);
        Place((RectTransform)_apply.transform, Vector2.zero, Vector2.zero, new Vector2(14, 16), new Vector2(202, 52));
        var workshop = Button(detailHeader, () => T("OpenWorkshop"), openWorkshop, Surface);
        CompactButton(workshop);
        _upload = Button(detailHeader, () => T("UploadBuild"), () =>
        {
            _filterMenu.SetActive(false);
            CloseRecommendationEditors();
            _recommendations = getRecommendations();
            if (_recommendations == null) return;
            BuildExporter.FillCurrentMemorySkills(_recommendations);
            _uploadTitle.SetTextWithoutNotify(T("DefaultBuildTitle"));
            _uploadTitleEdited = false;
            _uploadDescription.text = "";
            _uploadDialog.SetActive(true);
            foreach (var binding in _uploadDialog.GetComponentsInChildren<LocalizedTextBinding>(true)) binding.OnLanguageChanged();
            RefreshUploadBuildPreview();
        }, Accent);
        CompactButton(_upload);
        _needButton = Button(window, () => T("ApplyBuildNeeds"), applyNeeds, Surface);
        Place((RectTransform)_needButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-238, 16), new Vector2(-14, 52));
        _needButton.gameObject.SetActive(false);

        // 上传表单只在玩家确认后创建/更新作品，默认遵循游戏的私密草稿流程。
        _uploadDialog = Panel(_modal.transform, "Upload dialog", new Color(0, 0, 0, 0.8f)).gameObject;
        var form = Panel(_uploadDialog.transform, "Form", Background);
        Place(form, new Vector2(0.20f, 0.20f), new Vector2(0.80f, 0.80f));
        var uploadHeading = Text(form, () => T("UploadWindowTitle") +
            (IdentityChangeCompatibility.Enabled ? "  [" + T("IdentityBuildTag") + "]" : ""), 22);
        Place(uploadHeading.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -48), new Vector2(-24, -16));
        var uploadTarget = Text(form, () => _targetHero == null ? "" :
            T("UploadTarget", DewLocalization.GetUIValue(_targetHero + "_Name"), _targetSlot + 1), 13);
        uploadTarget.color = GameUiTheme.Muted;
        Place(uploadTarget.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -76), new Vector2(-24, -54));
        var titleLabel = Text(form, () => T("BuildTitle"), 14);
        Place(titleLabel.rectTransform, new Vector2(0, 1), new Vector2(0.32f, 1), new Vector2(24, -108), new Vector2(-12, -88));
        _uploadTitle = InputField(form, () => T("BuildTitle"), 120, false);
        _uploadTitle.onValueChanged.AddListener(_ => _uploadTitleEdited = true);
        Place((RectTransform)_uploadTitle.transform, new Vector2(0, 1), new Vector2(0.32f, 1), new Vector2(24, -154), new Vector2(-12, -116));
        var descriptionLabel = Text(form, () => T("BuildDescription"), 14);
        Place(descriptionLabel.rectTransform, new Vector2(0, 1), new Vector2(0.32f, 1), new Vector2(24, -190), new Vector2(-12, -170));
        _uploadDescription = InputField(form, () => T("BuildDescription"), 8000, true);
        Place((RectTransform)_uploadDescription.transform, Vector2.zero, new Vector2(0.32f, 1), new Vector2(24, 108), new Vector2(-12, -198));
        var buildLabel = Text(form, () => T("SkillBuild"), 18);
        Place(buildLabel.rectTransform, new Vector2(0.32f, 1), Vector2.one, new Vector2(12, -124), new Vector2(-216, -88));
        buildLabel.alignment = TextAlignmentOptions.MidlineLeft;
        var skills = Button(form, () => T("EditSkillBuild"), () =>
            RecommendationEditor.Open(this, _uploadDialog.transform, _recommendations, RefreshUploadBuildPreview), Accent);
        Place((RectTransform)skills.transform, Vector2.one, Vector2.one, new Vector2(-204, -124), new Vector2(-24, -88));
        _uploadBuildContent = Panel(form, "Upload build preview", Color.clear);
        Place(_uploadBuildContent, new Vector2(0.32f, 0), Vector2.one, new Vector2(12, 108), new Vector2(-24, -136));
        var previewLayout = _uploadBuildContent.gameObject.AddComponent<VerticalLayoutGroup>();
        previewLayout.childControlWidth = previewLayout.childControlHeight = true;
        previewLayout.childForceExpandHeight = false;
        previewLayout.childAlignment = TextAnchor.MiddleCenter;
        var visibility = Text(form, () => string.IsNullOrEmpty(UploadDraftNotice()) ? T("UploadVisibilityNote") : UploadDraftNotice(), 13);
        visibility.color = GameUiTheme.Muted;
        Place(visibility.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(24, 64), new Vector2(-24, 98));
        var send = Button(form, () => T("ConfirmUpload"), () =>
        {
            if (string.IsNullOrWhiteSpace(_uploadTitle.text)) return;
            _uploadDialog.SetActive(false);
            upload(_uploadTitle.text, _uploadDescription.text, _recommendations);
        }, Accent);
        Place((RectTransform)send.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-208, 18), new Vector2(-24, 54));
        var cancel = Button(form, () => T("Close"), () => _uploadDialog.SetActive(false), Surface);
        Place((RectTransform)cancel.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-292, 18), new Vector2(-220, 54));
        _uploadDialog.SetActive(false);
    }

    // 独立的三选一下拉菜单复用当前字体与按钮样式，不依赖游戏内的预制体模板。
    private void BuildFilter(Transform window, Transform header, WorkshopFilter selected, Action<WorkshopFilter> changed)
    {
        _filterButton = Button(header, () => T("FilterLabel", T("Filter" + selected)) + " v",
            () => { _filterMenu.transform.SetAsLastSibling(); _filterMenu.SetActive(!_filterMenu.activeSelf); }, Surface);
        CompactButton(_filterButton);
        var menu = Panel(window, "Build filter dropdown", Surface);
        Place(menu, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -250), new Vector2(172, -128));
        _filterMenu = menu.gameObject;
        var caption = _filterButton.GetComponentInChildren<TMP_Text>();
        var options = new[] { WorkshopFilter.Mine, WorkshopFilter.Public, WorkshopFilter.All };
        for (int i = 0; i < options.Length; i++)
        {
            var option = options[i];
            var button = Button(menu, () => T("Filter" + option), () =>
            {
                selected = option;
                _filterMenu.SetActive(false);
                caption.text = T("FilterLabel", T("Filter" + option)) + " v";
                CompactButton(_filterButton);
                changed(option);
            }, Surface);
            Place((RectTransform)button.transform, new Vector2(0, 1), Vector2.one,
                new Vector2(4, -40 * (i + 1) + 2), new Vector2(-4, -40 * i - 2));
        }
        _filterMenu.SetActive(false);
    }

    internal TMP_InputField InputField(Transform parent, Func<string> placeholder, int limit, bool multiline)
    {
        var root = Panel(parent, placeholder(), Surface);
        GameUiTheme.ApplyInput(root.GetComponent<Image>());
        var viewport = Rect(root, "Text viewport");
        Place(viewport, Vector2.zero, Vector2.one, new Vector2(10, 8), new Vector2(-10, -8));
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = Text(viewport, "", 18);
        var hint = Text(viewport, placeholder, 18);
        hint.color = new Color(0.5f, 0.6f, 0.65f);
        var input = root.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = (TextMeshProUGUI)text;
        input.placeholder = hint;
        input.characterLimit = limit;
        input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        input.richText = false;
        return input;
    }

    public void SetOpen(bool open)
    {
        if (_filterMenu != null) _filterMenu.SetActive(false);
        _modal.SetActive(open);
        _launcher.SetActive(!open);
    }

    public void SetTarget(string hero, int slot)
    {
        _targetHero = hero;
        _targetSlot = slot;
        _target.GetComponent<LocalizedTextBinding>().OnLanguageChanged();
        CloseRecommendationEditors();
        if (_uploadDialog != null) _uploadDialog.SetActive(false);
    }

    private void CloseRecommendationEditors()
    {
        if (_uploadDialog == null) return;
        foreach (var editor in _uploadDialog.GetComponentsInChildren<RecommendationEditor>(true))
        {
            editor.gameObject.SetActive(false);
            Destroy(editor.gameObject);
        }
    }

    public void SetUploadEnabled(bool enabled) => _upload.interactable = enabled;

    public void SetGuides(List<GuideSummary> guides, GuideSummary selected, bool loading)
    {
        foreach (var row in _rows) { row.SetActive(false); Destroy(row); }
        _rows.Clear();
        _guideCount = guides.Count;
        _listLoading = loading;
        _count.GetComponent<LocalizedTextBinding>().OnLanguageChanged();
        if (guides.Count == 0)
        {
            var empty = Text(_list, () => loading ? T("LoadingList") : T("NoGuides"), 17);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 110;
            _rows.Add(empty.gameObject);
        }
        foreach (var guide in guides)
        {
            var current = guide;
            string title = string.IsNullOrWhiteSpace(guide.Title) ? guide.Page : guide.Title;
            var button = Button(_list, () => (selected == guide ? "- " : "+ ") +
                (guide.RequiresIdentityChange ? "[" + T("IdentityBuildTag") + "] " : "") + title + "\n" + guide.Author +
                (guide.IsOwned ? " · " + (guide.LocalUploadSlot >= 0 ? T("BuildPage", guide.LocalUploadSlot + 1) : T("BuildPageUnlinked")) : ""),
                () => _select(current), selected == guide ? Accent : Surface);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            _rows.Add(button.gameObject);
        }
    }

    public void SetDetail(string detail, string message, bool canApply, bool canRefresh)
    {
        _detail.text = string.IsNullOrWhiteSpace(message) ? detail :
            string.IsNullOrWhiteSpace(detail) ? message : message + "\n" + detail;
        _detail.gameObject.SetActive(!string.IsNullOrWhiteSpace(_detail.text));
        _apply.interactable = canApply;
        _refresh.interactable = canRefresh;
        _filterButton.interactable = canRefresh;
        if (!canRefresh) _filterMenu.SetActive(false);
    }

    public void SetGuideCards(GuideData guide, string message, bool canApply, bool canRefresh)
    {
        string requirement = guide?.RequiresIdentityChange == true ?
            (IdentityChangeCompatibility.Enabled ? "[" + T("IdentityBuildTag") + "]" : T("IdentityChangeRequired")) + "\n" : "";
        _guideHeading.text = guide?.Title ?? T("Title");
        SetDetail(guide == null ? (string.IsNullOrWhiteSpace(message) ? T("ChooseGuide") : "") :
            requirement + guide.Author + (string.IsNullOrWhiteSpace(guide.Summary) ? "" : "\n" + guide.Summary), message, canApply, canRefresh);
        if (_renderedGuide == guide) return;
        _renderedGuide = guide;
        foreach (var card in _detailCards) { card.SetActive(false); Destroy(card); }
        _detailCards.Clear();
        if (guide != null) _detailCards.Add(BuildGuideCards.Render(this, _detailContent, guide));
        // 新作品完成布局后再回到顶端；状态消息刷新保留原滚动位置。
        LayoutRebuilder.ForceRebuildLayoutImmediate(_detailContent);
        _detailScroll.verticalNormalizedPosition = 1;
    }

    internal ScrollRect Scroll(Transform parent, string name, out RectTransform content)
    {
        var root = Panel(parent, name, Surface);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        var viewport = Panel(root, "Viewport", Color.clear);
        viewport.gameObject.AddComponent<RectMask2D>();
        Place(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16, 0));
        content = Rect(viewport, "Content");
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32;
        var track = Panel(root, "Scrollbar", new Color(0, 0, 0, 0.22f));
        Place(track, new Vector2(1, 0), Vector2.one, new Vector2(-13, 3), new Vector2(-3, -3));
        var handle = Panel(track, "Handle", new Color(0.34f, 0.46f, 0.55f));
        GameUiTheme.ApplyScrollbar(track.GetComponent<Image>(), false);
        GameUiTheme.ApplyScrollbar(handle.GetComponent<Image>(), true);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle;
        bar.targetGraphic = handle.GetComponent<Image>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    internal TMP_Text Text(Transform parent, string value, float size)
    {
        var rect = Rect(parent, "Text");
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(text, size);
        text.text = value;
        return text;
    }

    private void ConfigureText(TMP_Text text, float size)
    {
        if (_font != null) text.font = _font;
        text.fontSize = size;
        text.color = GameUiTheme.Text;
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;
    }

    internal TMP_Text Text(Transform parent, Func<string> value, float size)
    {
        var text = Text(parent, value(), size);
        LocalizedTextBinding.Bind(text, value);
        return text;
    }

    internal Button Button(Transform parent, Func<string> caption, Action action, Color color)
    {
        var button = Button(parent, caption(), action, color);
        LocalizedTextBinding.Bind(button.GetComponentInChildren<TMP_Text>(), caption);
        return button;
    }

    // 刷新全部弹层，保留表单输入、搜索内容和推荐草稿。
    public void RefreshLanguage()
    {
        if (!_uploadTitleEdited) _uploadTitle.SetTextWithoutNotify(T("DefaultBuildTitle"));
        foreach (var binding in GetComponentsInChildren<LocalizedTextBinding>(true)) binding.OnLanguageChanged();
        foreach (var row in GetComponentsInChildren<HorizontalLayoutGroup>(true))
            if (row.name == "List header" || row.name == "Detail header")
                foreach (var button in row.GetComponentsInChildren<Button>(true)) CompactButton(button);
        foreach (var editor in GetComponentsInChildren<RecommendationEditor>(true)) editor.RefreshLanguage();
        if (_recommendations != null) RefreshUploadBuildPreview();
        _renderedGuide = null;
    }

    internal Button Button(Transform parent, string caption, Action action, Color color)
    {
        var rect = Rect(parent, caption);
        var image = rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        GameUiTheme.StyleButton(button, color);
        button.onClick.AddListener(new UnityAction(action));
        var label = Text(rect, caption, 14);
        Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 3), new Vector2(-8, -3));
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 11;
        label.fontSizeMax = 14;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return button;
    }

    // 按本地化文字的实际宽度收拢按钮，工具栏不把按钮拉长到整列。
    private static void CompactButton(Button button)
    {
        var sizing = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
        var label = button.GetComponentInChildren<TMP_Text>();
        sizing.minWidth = 60;
        sizing.preferredWidth = Mathf.Clamp(label.GetPreferredValues(label.text).x + 22, 60, 208);
        sizing.preferredHeight = 36;
        sizing.flexibleWidth = 0;
    }

    private static RectTransform ActionRow(Transform parent, string name, TextAnchor alignment)
    {
        var row = Rect(parent, name);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = alignment;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.spacing = 6;
        return row;
    }

    public void SetBuildNeedsEnabled(bool visible, bool enabled)
    {
        _needButton.gameObject.SetActive(visible);
        _needButton.interactable = enabled;
    }

    // 上传预览与编辑器共享同一份草稿，关闭编辑弹窗或切换语言后重绘原生技能栏。
    private void RefreshUploadBuildPreview()
    {
        if (_uploadBuildContent == null || _recommendations == null) return;
        if (_uploadBuildPreview != null)
        {
            _uploadBuildPreview.SetActive(false);
            Destroy(_uploadBuildPreview);
        }
        _uploadBuildPreview = SkillBuildLayout.Create(this, _uploadBuildContent, _recommendations, maxScale: 0.85f).gameObject;
    }

    internal static RectTransform Panel(Transform parent, string name, Color color)
    {
        var rect = Rect(parent, name);
        var image = rect.gameObject.AddComponent<Image>();
        bool window = name == "Window" || name == "Form" || name == "Editor";
        if (color == Color.clear || name == "Modal" || name == "Upload dialog" || name == "Recommendation editor")
            image.color = color; // 透明裁剪区和全屏点击遮罩不绘制面板资源。
        else if (window)
        {
            GameUiTheme.Apply(image, window: true);
            GameUiTheme.WindowDecoration(rect);
        }
        else GameUiTheme.ApplySurface(image);
        return rect;
    }

    // 由游戏的返回键栈消费 Esc；只处理最上层，避免同一按键继续关闭大厅。
    private bool CloseTopPopup()
    {
        if (!isActiveAndEnabled) return false;
        if (_launcherDrag != null && _launcherDrag.CancelDrag()) return true;
        if (!IsOpen) return false;
        foreach (var editor in _modal.GetComponentsInChildren<RecommendationEditor>())
            if (editor.isActiveAndEnabled) return editor.CloseFromBack();
        if (_uploadDialog.activeSelf) { _uploadDialog.SetActive(false); return true; }
        if (_filterMenu.activeSelf) { _filterMenu.SetActive(false); return true; }
        SetOpen(false);
        return true;
    }

    private void OnDestroy() { _backHandler?.Remove(); }

    internal static RectTransform Rect(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, Vector2.zero, Vector2.one);
        return rect;
    }

    internal static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
