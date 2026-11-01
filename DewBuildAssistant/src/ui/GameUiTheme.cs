using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DewBuildAssistant.ui;

// 只引用大厅原生面板、控件和图标资源，不创建纹理、材质或替代面板美术。
internal static class GameUiTheme
{
    public static Color Background => _window != null ? _window.color : Color.white;
    public static Color Surface => _button != null ? _button.color : Color.white;
    public static Color Accent => Dew.StardustColor;
    public static readonly Color Text = new(0.88f, 0.94f, 0.96f);
    public static readonly Color Muted = new(0.57f, 0.69f, 0.75f);
    private static Image _window, _cell, _button, _input, _scrollHandle, _scrollTrack;
    private static Image _primaryButton;
    private static RectTransform _windowDeco;
    private static Button _nativeButton;
    private static UI_StarIcon _starIcon;
    private static UI_Achievement_Icon _dejavuIcon;
    private static Sprite _launcher;
    private static Image[] _images;

    public static void Capture()
    {
        // 包括禁用的大厅窗口；排除助手自己的副本，避免重复打开后递归克隆。
        _images = Resources.FindObjectsOfTypeAll<Image>().Where(i => i.gameObject.scene.IsValid() &&
            i.GetComponentInParent<BuildAssistantView>() == null).ToArray();
        _window = FindImage("Window_Background") ?? FindImage("texWindowGradientBG");
        // 星座窗口的保存/放弃按钮使用稳定的蓝色样式，避免拾取调试按钮或动态选中底色。
        var constellations = Resources.FindObjectsOfTypeAll<UI_Lobby_Constellations>().FirstOrDefault();
        _nativeButton = constellations?.resetButton;
        _button = _nativeButton?.targetGraphic as Image;
        _primaryButton = constellations?.saveButton?.targetGraphic as Image;
        _button ??= FindImage("BR_Simple_Background");
        var lobbyName = Resources.FindObjectsOfTypeAll<UI_Lobby_LobbyNameGroup>().FirstOrDefault();
        _input = lobbyName?.inputField?.targetGraphic as Image;
        if (_input == null || _input.color.a < 0.1f) _input = _button;
        _scrollHandle = FindImage("ScrollBar_Handler") ?? FindImage("ScrollBar_Handler_0");
        _scrollTrack = _scrollHandle != null ? _scrollHandle.transform.parent?.GetComponent<Image>() : null;
        _windowDeco = Resources.FindObjectsOfTypeAll<RectTransform>().FirstOrDefault(r =>
            r.name == "Window Deco - Big Generic" && r.gameObject.scene.IsValid() &&
            r.GetComponentInParent<BuildAssistantView>() == null && r.parent != null &&
            r.parent.GetComponentsInChildren<Image>(true).Any(i =>
                i.name == "Background" && i.transform.parent == r.parent && i.color.a > 0.5f));
        // 背景可能使用材质而非 Window_Background Sprite，优先取装饰同一面板内的背景。
        if (_windowDeco != null && _windowDeco.parent != null)
        {
            var nativeBackground = _windowDeco.parent.GetComponentsInChildren<Image>(true).FirstOrDefault(i =>
                i.name == "Background" && i.transform.parent == _windowDeco.parent && i.color.a > 0.5f);
            if (nativeBackground != null) _window = nativeBackground;
        }
        _starIcon = Resources.FindObjectsOfTypeAll<UI_StarIcon>().FirstOrDefault(i =>
            i.GetComponentInParent<BuildAssistantView>() == null);
        var dejavu = Resources.FindObjectsOfTypeAll<UI_Lobby_DejavuButton>().FirstOrDefault();
        var dejavuWindow = Resources.FindObjectsOfTypeAll<UI_Lobby_DejavuWindow>().FirstOrDefault();
        _dejavuIcon = dejavu?.icon ?? dejavuWindow?.detailItemIcon ?? dejavuWindow?.itemPrefab?.icon;

        _cell = null;
        _launcher = null;
        var catalogue = Resources.FindObjectsOfTypeAll<UI_Collectables_MainView>().FirstOrDefault();
        if (catalogue?.skillPrefab != null) _cell = catalogue.skillPrefab.transform.Find("Base")?.GetComponent<Image>();
        if (_cell == null)
        {
            var item = Resources.FindObjectsOfTypeAll<UI_Collectables_ObjectItem>().FirstOrDefault();
            if (item != null) _cell = item.transform.Find("Base")?.GetComponent<Image>();
        }
        var opener = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b =>
            b.gameObject.scene.IsValid() && b.GetComponentInParent<BuildAssistantView>() == null &&
            b.name.IndexOf("Collectable", StringComparison.OrdinalIgnoreCase) >= 0);
        if (opener != null)
            _launcher = opener.GetComponentsInChildren<Image>(true).FirstOrDefault(i =>
                i.sprite != null && i.type == Image.Type.Simple)?.sprite;
    }

    private static Image FindImage(string sprite) => _images.FirstOrDefault(i => i.sprite != null && i.sprite.name == sprite);

    // 连同原生材质、颜色与切片参数一起引用，不再把原生面板乘上自定义底色。
    private static void CopyImage(Image target, Image source)
    {
        // 原生窗口背景本身就是无 Sprite 的深蓝 Image，仍须复制其颜色和材质。
        if (source == null)
        {
            target.sprite = null;
            target.color = Color.clear;
            return;
        }
        target.sprite = source.sprite;
        target.material = source.material;
        target.type = source.type;
        target.color = source.color;
        target.preserveAspect = source.preserveAspect;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.fillCenter = source.fillCenter;
        target.useSpriteMesh = source.useSpriteMesh;
    }

    public static void Apply(Image image, bool window = false, bool cell = false)
    {
        CopyImage(image, window ? _window : cell ? _cell ?? _input : _button);
        if (window && _window != null)
        {
            // 弹窗保持原生底色并遮住下层窗口，避免多层文字透出干扰阅读。
            var color = image.color;
            color.a = 1;
            image.color = color;
        }
    }

    public static void ApplySurface(Image image) => CopyImage(image, _window);
    public static void ApplyInput(Image image) => CopyImage(image, _input);
    public static void ApplyScrollbar(Image image, bool handle) => CopyImage(image, handle ? _scrollHandle : _scrollTrack ?? _input);

    public static void StyleButton(Button button, Color requested)
    {
        CopyImage((Image)button.targetGraphic, requested == Accent ? _primaryButton ?? _button : _button);
        if (_nativeButton != null) button.colors = _nativeButton.colors;
        if (requested == Color.clear) button.targetGraphic.color = Color.clear;
    }

    public static void WindowDecoration(Transform parent)
    {
        if (_windowDeco == null) return;
        var deco = CloneDisplay(_windowDeco, parent);
        deco.name = "Native window decoration";
        // 源装饰自身的伸缩边距和层级不变，只换为助手窗口的尺寸。
        var rect = (RectTransform)deco.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = _windowDeco.offsetMin;
        rect.offsetMax = _windowDeco.offsetMax;
        deco.transform.SetAsFirstSibling();
    }

    // 副本在禁用的临时父级下完成解绑，保留 Graphic/遮罩/描边等视觉组件。
    private static T CloneDisplay<T>(T source, Transform parent) where T : Component
    {
        var staging = new GameObject("Native UI staging", typeof(RectTransform));
        staging.SetActive(false);
        staging.transform.SetParent(parent, false);
        var clone = Object.Instantiate(source, staging.transform, false);
        foreach (var behaviour in clone.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour is not Graphic && behaviour is not Mask && behaviour is not RectMask2D &&
                behaviour is not BaseMeshEffect && behaviour is not LayoutGroup &&
                behaviour is not LayoutElement && behaviour is not ContentSizeFitter && behaviour is not AspectRatioFitter && behaviour is not Canvas)
                behaviour.enabled = false;
            if (behaviour is ILangaugeChangedCallback) Object.DestroyImmediate(behaviour);
        }
        foreach (var graphic in clone.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        foreach (var group in clone.GetComponentsInChildren<CanvasGroup>(true))
        {
            group.alpha = 1;
            group.interactable = group.blocksRaycasts = false;
        }
        clone.gameObject.SetActive(true);
        var rect = (RectTransform)clone.transform;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        // 保留原生设计尺寸；图标的固定像素子节点随后作为整体缩放。
        var sourceRect = (RectTransform)source.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sourceRect.rect.size;
        rect.anchoredPosition = Vector2.zero;
        clone.transform.SetParent(parent, false);
        Object.Destroy(staging);
        return clone;
    }

    // 满填充仅用于构筑展示，使用原生星座背景、轮廓和类型配色，不读取购买等级。
    public static bool StarIcon(Transform parent, StarEffect star)
    {
        if (_starIcon == null || star == null) return false;
        var icon = CloneDisplay(_starIcon, parent);
        icon.Setup(star, 1, false, false, false);
        NativeIconFitter.Attach(icon);
        icon.name = "Native star icon";
        return true;
    }

    // 复用既视感使用的成就图标，包括其稀有度背景、技能/精粹框。
    public static bool DejavuIcon(Transform parent, string key)
    {
        if (_dejavuIcon == null) return false;
        var asset = string.IsNullOrEmpty(key) ? null : DewResources.GetByShortTypeName(key, ResourceLoadSettings.Light);
        var icon = CloneDisplay(_dejavuIcon, parent);
        icon.SetupByItem(asset);
        if (asset == null && icon.rarityImage != null) icon.rarityImage.color = Color.clear;
        NativeIconFitter.Attach(icon);
        icon.name = "Native deja vu icon";
        return true;
    }

    public static void LauncherIcon(Transform parent)
    {
        // 没有专用入口图标时只引用一枚游戏精粹图标，不再组合自绘标记。
        var sprite = _launcher ?? DewResources.FindAllByType<Gem>(ResourceLoadSettings.Light).FirstOrDefault(g => g.icon != null)?.icon;
        if (sprite == null) return;
        var image = BuildAssistantView.Rect(parent, "Native launcher icon").gameObject.AddComponent<Image>();
        BuildAssistantView.Place(image.rectTransform, Vector2.zero, Vector2.one, new Vector2(7, 7), new Vector2(-7, -7));
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }
}
