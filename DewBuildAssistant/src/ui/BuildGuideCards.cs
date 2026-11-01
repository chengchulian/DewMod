using System;
using System.Collections.Generic;
using System.Linq;
using DewBuildAssistant.config;
using DewBuildAssistant.controller;
using DewBuildAssistant.model;
using UnityEngine;
using UnityEngine.UI;
using static DewBuildAssistant.ui.BuildAssistantView;

namespace DewBuildAssistant.ui;

// 卡片直接引用游戏资源的 Sprite，不下载、不复制图片，也不把图标写入工坊 JSON。
internal static class BuildGuideCards
{
    private static Color Surface => GameUiTheme.Surface;
    private static string T(string key, params object[] args) => LocalizationSource.GetLocalizationText(key, args);

    public static GameObject Render(BuildAssistantView view, Transform parent, GuideData guide)
    {
        var root = Vertical(parent, "Build cards");
        MemoryRow(view, root, guide);
        for (int pair = 4; pair < BuildFields.Length; pair += 2)
        {
            var row = Rect(root, "Star categories " + pair);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            for (int i = pair; i < pair + 2; i++)
            {
                var section = Vertical(row, BuildFields[i]);
                var width = section.gameObject.AddComponent<LayoutElement>();
                width.minWidth = width.preferredWidth = 0;
                width.flexibleWidth = 1;
                if (!guide.PresentFields.Contains(BuildFields[i])) continue;
                string labelKey = FieldLabels[i];
                var label = view.Text(section, () => T(labelKey), 15);
                label.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
                ItemGrid(view, section, guide.GetItems(BuildFields[i]));
            }
        }
        view.Text(root, () => T("SkillBuild"), 18);
        SkillBuildLayout.Create(view, root, guide.Recommendations, maxScale: 0.8f, alignLeft: true);
        return root.gameObject;
    }
    private static readonly string[] BuildFields = { "旅行者记忆1", "旅行者记忆2", "旅行者身份记忆", "既视感", "破坏星座", "生命星座", "幻想星座", "灵活星座" };
    private static readonly string[] FieldLabels = { "MemoryQ", "MemoryR", "IdentityMemory", "Dejavu", "DestructionStars", "LifeStars", "ImaginationStars", "FlexibleStars" };

    // Q/R/D 旅行者记忆与既视感固定为同一行，详情中不再为每个记忆单独占一整行。
    private static void MemoryRow(BuildAssistantView view, Transform parent, GuideData guide)
    {
        if (!BuildFields.Take(4).Any(guide.PresentFields.Contains)) return;
        var row = Rect(parent, "Q R D and deja vu");
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;
        for (int i = 0; i < 4; i++)
        {
            string field = BuildFields[i];
            if (!guide.PresentFields.Contains(field)) continue;
            var column = Vertical(row, field);
            var sizing = column.gameObject.AddComponent<LayoutElement>();
            sizing.minWidth = sizing.preferredWidth = 84;
            sizing.flexibleWidth = 0;
            var label = i < 3 ? view.Text(column, new[] { "Q", "R", "D" }[i], 16) : view.Text(column, () => T("Dejavu"), 16);
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            var icons = Rect(column, "Memory icon");
            var grid = icons.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(84, 84);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
            var items = guide.GetItems(field);
            icons.gameObject.AddComponent<LayoutElement>().preferredHeight = Math.Max(1, items.Count) * 84;
            if (items.Count == 0) MemoryTile(view, icons, null, i == 3);
            else foreach (var item in items) MemoryTile(view, icons, item, i == 3);
        }
    }

    private static void MemoryTile(BuildAssistantView view, Transform parent, GuideItem item, bool dejavu)
    {
        var root = Rect(parent, dejavu ? "Deja vu" : "Traveler memory");
        var icon = Rect(root, "Native icon");
        Place(icon, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-30, -60), new Vector2(30, 0));
        bool shown = dejavu ? GameUiTheme.DejavuIcon(icon, item?.ResourceHint) : SkillBuildLayout.MemoryIcon(icon, item?.ResourceHint);
        if (!shown && item != null)
        {
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = ResourceSprite(item.ResourceHint);
            image.color = image.sprite == null ? Color.clear : ResourceColor(item.ResourceHint);
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        var label = view.Text(root, () => item == null ? T("EmptySlot") : BuildExporter.ResourceDisplayName(item.ResourceHint), 12);
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        Place(label.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 22));
    }

    public static RectTransform Vertical(Transform parent, string name)
    {
        var root = Rect(parent, name);
        var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        return root;
    }

    public static void ItemGrid(BuildAssistantView view, Transform parent, List<GuideItem> items)
    {
        if (items.Count == 0)
        {
            var empty = view.Text(parent, () => T("EmptySlot"), 13);
            empty.color = GameUiTheme.Muted;
            return;
        }
        var root = Rect(parent, "Icon grid");
        var grid = root.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(82, 78);
        grid.spacing = new Vector2(6, 4);
        grid.constraint = GridLayoutGroup.Constraint.Flexible;
        var size = root.gameObject.AddComponent<LayoutElement>();
        size.flexibleWidth = 1;
        foreach (var item in items)
            CompactTile(view, root, item, null);
    }

    // 图鉴式紧凑卡片：图标和名称都透传射线，由整张卡片接收点击及拖动。
    public static Button CompactTile(BuildAssistantView view, Transform parent, GuideItem item, Action clicked, float iconSize = 44)
    {
        var star = DewResources.GetByShortTypeName(item.ResourceHint, ResourceLoadSettings.Light) as StarEffect;
        var button = view.Button(parent, () => BuildExporter.ResourceDisplayName(item.ResourceHint), clicked ?? (() => { }), Surface);
        GameUiTheme.Apply(button.GetComponent<Image>(), cell: true);
        if (star != null) button.GetComponent<Image>().color = Color.clear;
        var icon = Rect(button.transform, "Game icon");
        Place(icon, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(-iconSize / 2, -iconSize - 3), new Vector2(iconSize / 2, -3));
        if (star == null || !GameUiTheme.StarIcon(icon, star))
        {
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = ResourceSprite(item.ResourceHint);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = image.sprite == null ? Color.clear : ResourceColor(item.ResourceHint);
        }
        var name = button.GetComponentInChildren<TMPro.TMP_Text>();
        name.fontSize = iconSize < 40 ? 11 : 12;
        name.enableAutoSizing = false;
        name.alignment = TMPro.TextAlignmentOptions.Center;
        name.overflowMode = TMPro.TextOverflowModes.Ellipsis;
        Place(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -iconSize - 5));
        return button;
    }

    public static Button ItemButton(BuildAssistantView view, Transform parent, GuideItem item, Action clicked)
    {
        var button = view.Button(parent, () => item == null ? T("ChooseMemory") : BuildExporter.ResourceDisplayName(item.ResourceHint), clicked ?? (() => { }), Surface);
        if (item != null) Icon(button.transform, item.ResourceHint, new Vector2(4, -36), new Vector2(36, -4));
        var text = button.GetComponentInChildren<TMPro.TMP_Text>();
        text.rectTransform.offsetMin = new Vector2(item == null ? 8 : 42, 3);
        return button;
    }

    private static void Icon(Transform parent, string key, Vector2 min, Vector2 max)
    {
        var rect = Rect(parent, "Game icon");
        Place(rect, new Vector2(0, 1), new Vector2(0, 1), min, max);
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.sprite = ResourceSprite(key);
        image.color = image.sprite == null ? Color.clear : ResourceColor(key);
    }

    // 与 UI_StarIcon 一样按星座资源自身的类型取色，转职的跨类型栏位不改变其颜色。
    public static Color ResourceColor(string key)
    {
        if (string.IsNullOrEmpty(key)) return Color.white;
        return DewResources.GetByShortTypeName(key, ResourceLoadSettings.Light) is StarEffect star
            ? Dew.GetStarCategoryColor(star.type) : Color.white;
    }

    public static Sprite ResourceSprite(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        var asset = DewResources.GetByShortTypeName(key, ResourceLoadSettings.Light);
        return asset switch
        {
            Gem gem => gem.icon,
            SkillTrigger skill when skill.configs != null && skill.configs.Length > 0 => skill.configs[0].triggerIcon,
            StarEffect star => star.starIcon,
            _ => null
        };
    }
}
