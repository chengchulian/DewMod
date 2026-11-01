using System.Linq;
using DewBuildAssistant.config;
using DewBuildAssistant.model;
using DewBuildAssistant.util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DewBuildAssistant.ui.BuildAssistantView;

namespace DewBuildAssistant.ui;

// 固定的 QWERD 展示模板，仅引用游戏圆框素材，不复制运行中的技能栏或战绩实例。
internal static class SkillBuildLayout
{
    private static readonly Color CoreColor = new(1f, 0.79f, 0.34f, 1);
    private static bool _missingSpritesLogged;

    public static RectTransform Create(BuildAssistantView view, Transform parent, BuildRecommendations build,
        RecommendationEditor editor = null, float maxScale = 1, bool alignLeft = false)
    {
        var bar = Rect(parent, "QWERD skill bar");
        var sizing = bar.gameObject.AddComponent<NativeBuildBar>();
        sizing.MaxScale = Mathf.Clamp(maxScale, 0.1f, 1);
        sizing.AlignLeft = alignLeft;
        var size = bar.gameObject.AddComponent<LayoutElement>();
        size.preferredHeight = 220 * sizing.MaxScale;
        size.flexibleWidth = 1;
        if (!NativeSkillSprites.TryResolve(Resources.FindObjectsOfTypeAll<Sprite>(), out var sprites))
        {
            // 缺少原生圆框时不创建无 Sprite 的 Image，以免再次渲染成实心方框。
            size.preferredHeight = 50;
            var message = view.Text(bar, () => LocalizationSource.GetLocalizationText("SkillBuildResourcesPending"), 14);
            message.color = GameUiTheme.Muted;
            if (!_missingSpritesLogged)
            {
                _missingSpritesLogged = true;
                Debug.LogWarning("[BD 助手] 大厅原生圆形技能素材尚未就绪：ActionButton_Background / ActionButton_Border / ActionBar_Level_Background");
            }
            return bar;
        }
        var row = Rect(bar, "Native skill row");
        row.anchorMin = row.anchorMax = new Vector2(alignLeft ? 0 : 0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(660, 220);
        sizing.Row = row;
        sizing.Size = size;
        // 固定层级在禁用父级下完成绑定，不依赖对局 UI 是否加载。
        row.gameObject.SetActive(false);
        for (int i = 0; i < BuildRecommendations.SlotKeys.Length; i++)
        {
            var skill = build.Get(BuildRecommendations.SlotKeys[i]);
            var column = Rect(row, skill.Slot + " skill");
            column.anchorMin = column.anchorMax = column.pivot = new Vector2(0.5f, 0.5f);
            column.anchoredPosition = new Vector2((i - 2) * 132, 0);
            column.sizeDelta = new Vector2(125, 151);
            CreateFixedTemplate(view, column, skill, editor, sprites);
            var key = view.Text(column, skill.Slot, 16);
            key.fontStyle = FontStyles.Bold;
            key.alignment = TextAlignmentOptions.Center;
            Place(key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-50, 62), new Vector2(50, 86));
            column.gameObject.SetActive(true);
        }
        row.gameObject.SetActive(true);
        sizing.Fit();
        return bar;
    }

    // 五个按键使用同一套固定尺寸和四槽坐标，保留原生 Sprite 与圆形遮罩。
    private static void CreateFixedTemplate(BuildAssistantView view, RectTransform parent, SkillBuild skill, RecommendationEditor editor, NativeSkillSprites sprites)
    {
        var slot = Circle(parent, "Skill", sprites, false, Vector2.zero, 100, out var icon);
        icon.sprite = skill.Skill == null ? null : BuildGuideCards.ResourceSprite(skill.Skill.ResourceHint);
        icon.gameObject.SetActive(icon.sprite != null);
        BindSlot(view, slot, icon, skill.Skill, skill.IsCore, false, skill, -1, editor);
        var positions = new[] { new Vector2(-50, -65), new Vector2(-18, -85), new Vector2(18, -85), new Vector2(50, -65) };
        for (int i = 0; i < 4; i++)
        {
            var gem = skill.GemSlots[i];
            var gemSlot = Circle(parent, "Gem " + i, sprites, true, positions[i], 30, out var gemIcon);
            gemIcon.sprite = gem == null ? null : BuildGuideCards.ResourceSprite(gem.Gem.ResourceHint);
            gemIcon.gameObject.SetActive(gemIcon.sprite != null);
            BindSlot(view, gemSlot, gemIcon, gem?.Gem, gem?.IsCore == true, true, skill, i, editor);
        }
    }

    private static RectTransform Circle(Transform parent, string name, NativeSkillSprites sprites, bool gem, Vector2 position, float diameter, out Image icon)
    {
        var root = Rect(parent, name);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = position;
        root.sizeDelta = Vector2.one * diameter;
        var background = root.gameObject.AddComponent<Image>();
        background.sprite = sprites.Background;
        background.type = Image.Type.Simple;
        background.color = new Color(0.14117648f, 0.15294118f, 0.2f, 1);
        var inside = Rect(root, "Round mask");
        inside.localScale = Vector3.one * 0.92f;
        var fill = inside.gameObject.AddComponent<Image>();
        fill.sprite = sprites.Mask;
        fill.type = Image.Type.Simple;
        fill.raycastTarget = false;
        inside.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        icon = Rect(inside, "Game icon").gameObject.AddComponent<Image>();
        icon.raycastTarget = false;
        var border = Rect(root, "Border").gameObject.AddComponent<Image>();
        border.sprite = gem ? sprites.GemBorder : sprites.SkillBorder;
        border.type = Image.Type.Simple;
        border.color = gem ? new Color(0.38039216f, 0.43529412f, 0.5294118f, 1) : Color.white;
        border.raycastTarget = false;
        return root;
    }

    // Build 详情的旅行者记忆与技能构筑共用同一套原生圆框和遮罩。
    public static bool MemoryIcon(Transform parent, string key)
    {
        if (!NativeSkillSprites.TryResolve(Resources.FindObjectsOfTypeAll<Sprite>(), out var sprites)) return false;
        var root = Circle(parent, "Native traveler memory", sprites, false, Vector2.zero, 68, out var icon);
        Place(root, Vector2.zero, Vector2.one);
        icon.sprite = string.IsNullOrEmpty(key) ? null : BuildGuideCards.ResourceSprite(key);
        icon.gameObject.SetActive(icon.sprite != null);
        return true;
    }

    private static void BindSlot(BuildAssistantView view, RectTransform target, Image icon, GuideItem item, bool core, bool gem,
        SkillBuild skill, int index, RecommendationEditor editor)
    {
        var frames = target.GetComponentsInChildren<Image>(true).Where(i => i != icon && i.sprite != null).ToArray();
        var frame = frames.FirstOrDefault(i => i.sprite.name == "ActionButton_Border" || i.sprite.name == "ActionBar_Level_Border") ?? frames.FirstOrDefault();
        if (frame != null && core) frame.color = CoreColor;
        if (editor == null) return;
        var hit = target.GetComponent<Image>() ?? target.gameObject.AddComponent<Image>();
        if (hit.sprite == null)
        {
            if (frame != null)
            {
                hit.sprite = frame.sprite;
                hit.type = frame.type;
                hit.pixelsPerUnitMultiplier = frame.pixelsPerUnitMultiplier;
            }
            hit.color = Color.clear;
        }
        hit.raycastTarget = true;
        var button = target.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() =>
        {
            if (item == null) { editor.FocusCandidates(gem); return; }
            if (gem) skill.GemSlots[index].IsCore = !skill.GemSlots[index].IsCore;
            else skill.IsCore = !skill.IsCore;
            editor.RefreshWorkspace();
        });
        var drop = target.gameObject.AddComponent<RecommendationDrop>();
        drop.Editor = editor;
        drop.AcceptsGems = gem || skill.Slot == "D";
        drop.AcceptsSkillsAndGems = !gem && skill.Slot != "D";
        drop.Accept = d => gem ? RecommendationEditing.PlaceGem(editor.Recommendations, skill, index, d.Item, d.SourceSkill, d.SourceGemSlot) :
            d.IsGem ? RecommendationEditing.PlaceGemOnSkill(editor.Recommendations, skill, d.Item, d.SourceSkill, d.SourceGemSlot) :
                skill.Slot != "D" && RecommendationEditing.CanDragSkill(d.Item, d.SourceSkill) && RecommendationEditing.PlaceSkill(skill, d.Item, d.SourceSkill);
        if (item == null) return;
        var drag = target.gameObject.AddComponent<RecommendationDrag>();
        drag.Editor = editor;
        drag.Item = item;
        drag.IsGem = gem;
        drag.SourceSkill = skill;
        drag.SourceGemSlot = index;
    }
}

// 整体等比缩放原生技能栏，圆形图标及精粹的相对位置保持不变。
internal sealed class NativeBuildBar : MonoBehaviour
{
    public RectTransform Row;
    public LayoutElement Size;
    public float MaxScale = 1;
    public bool AlignLeft;
    private void OnRectTransformDimensionsChange() => Fit();
    public void Fit()
    {
        if (Row == null || Size == null) return;
        float scale = Mathf.Min(MaxScale, Mathf.Max(1, ((RectTransform)transform).rect.width - 16) / 660);
        Row.localScale = Vector3.one * scale;
        Row.anchoredPosition = new Vector2(AlignLeft ? 8 + 330 * scale : 0, 0);
        Size.preferredHeight = 220 * scale;
    }
}
