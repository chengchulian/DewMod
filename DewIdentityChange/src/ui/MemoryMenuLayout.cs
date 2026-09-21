using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DewIdentityChange.ui;

// The original menus place all items in one row/column on the menu itself.
// Keep their background and input handlers, and move only the items into a viewport.
public sealed class MemoryMenuLayout : MonoBehaviour
{
    private const float Padding = 24f;
    private const float Spacing = 10f;
    private readonly Vector3[] _corners = new Vector3[4];
    private readonly Dictionary<RectTransform, Vector3> _itemScales = new();
    private RectTransform _root;
    private RectTransform _originalParent;
    private RectTransform _viewport;
    private RectTransform _content;
    private GridLayoutGroup _grid;
    private ScrollRect _scroll;
    private LayoutGroup _originalLayout;
    private ContentSizeFitter _originalFitter;
    private bool _layoutEnabled;
    private bool _fitterEnabled;
    private Vector2 _originalSize;
    private Canvas _canvas;
    private int _count;
    private int _selected;
    private bool _revealSelection;
    private bool _restoreQueued;
    private Vector2 _lastAvailableSize;

    public bool HasViewport => _content != null;

    public static void Apply<T>(ContextMenu menu, Transform parent, List<T> items, int selected)
        where T : Component
    {
        if (menu == null || parent == null || items == null)
        {
            return;
        }

        var layout = menu.GetComponent<MemoryMenuLayout>();
        if (layout == null)
        {
            layout = menu.gameObject.AddComponent<MemoryMenuLayout>();
        }

        if (layout._content == null || layout._viewport == null)
        {
            layout.Initialize((RectTransform)parent);
        }
        else
        {
            layout._viewport.gameObject.SetActive(true);
            if (layout._originalLayout != null) layout._originalLayout.enabled = false;
            if (layout._originalFitter != null) layout._originalFitter.enabled = false;
        }

        foreach (var item in items)
        {
            var rect = (RectTransform)item.transform;
            if (!layout._itemScales.ContainsKey(rect))
            {
                layout._itemScales.Add(rect, rect.localScale);
            }

            rect.SetParent(layout._content, false);
            rect.localScale = Vector3.one;
        }

        layout._count = items.Count;
        layout._selected = Mathf.Clamp(selected, 0, Mathf.Max(0, items.Count - 1));
        layout._revealSelection = true;
        layout._lastAvailableSize = Vector2.zero;
        layout._scroll.StopMovement();
        layout.Resize();
    }

    private void Initialize(RectTransform parent)
    {
        _root = (RectTransform)transform;
        _originalParent = parent;
        _originalSize = _root.sizeDelta;
        _canvas = GetComponentInParent<Canvas>().rootCanvas;
        _originalLayout = parent.GetComponent<LayoutGroup>();
        _originalFitter = parent.GetComponent<ContentSizeFitter>();
        _layoutEnabled = _originalLayout != null && _originalLayout.enabled;
        _fitterEnabled = _originalFitter != null && _originalFitter.enabled;
        if (_originalLayout != null) _originalLayout.enabled = false;
        if (_originalFitter != null) _originalFitter.enabled = false;

        _viewport = CreateRect("MemoryViewport", _root);
        _viewport.anchorMin = Vector2.zero;
        _viewport.anchorMax = Vector2.one;
        _viewport.offsetMin = new Vector2(Padding, Padding);
        _viewport.offsetMax = new Vector2(-Padding, -Padding);
        _viewport.gameObject.AddComponent<RectMask2D>();
        var image = _viewport.gameObject.AddComponent<Image>();
        image.color = Color.clear;

        _content = CreateRect("MemoryContent", _viewport);
        _content.anchorMin = _content.anchorMax = new Vector2(0f, 1f);
        _content.pivot = new Vector2(0f, 1f);
        _grid = _content.gameObject.AddComponent<GridLayoutGroup>();
        _grid.cellSize = new Vector2(136f, 136f);
        _grid.spacing = new Vector2(Spacing, Spacing);
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.childAlignment = TextAnchor.UpperLeft;

        _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
        _scroll.viewport = _viewport;
        _scroll.content = _content;
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.inertia = false;
        _scroll.scrollSensitivity = 60f;
        _viewport.gameObject.AddComponent<UI_GamepadScrollRect>().useRightJoystick = true;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        return rect;
    }

    private Camera UiCamera => _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

    private void Resize()
    {
        // Convert from pixels through this menu's transform, including CanvasScaler and menu scale.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, Vector2.zero, UiCamera, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root,
            new Vector2(Screen.width, Screen.height), UiCamera, out var max);
        Vector2 available = max - min;
        if (available.x <= 0f || available.y <= 0f || available == _lastAvailableSize) return;
        _lastAvailableSize = available;

        float cell = Mathf.Min(136f, Mathf.Max(1f, Mathf.Min(available.x, available.y) * 0.75f - Padding * 2f));
        _grid.cellSize = new Vector2(cell, cell);
        int columns = Mathf.Clamp(Mathf.FloorToInt((available.x * 0.65f - Padding * 2f + Spacing) /
                                                  (cell + Spacing)), 1, Mathf.Min(6, Mathf.Max(1, _count)));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)_count / columns));
        float width = columns * (cell + Spacing) - Spacing;
        float height = rows * (cell + Spacing) - Spacing;
        _grid.constraintCount = columns;
        _content.sizeDelta = new Vector2(width, height);
        _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + Padding * 2f);
        _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Min(height + Padding * 2f, available.y * 0.75f));
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
    }

    private void LateUpdate()
    {
        if (_root == null || _content == null) return;
        Resize();

        // Callers position the menu after OnEnable, so clamp after that assignment and layout.
        _root.GetWorldCorners(_corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(UiCamera, _corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(UiCamera, _corners[2]);
        const float margin = 16f;
        var delta = new Vector2(
            Mathf.Max(0f, margin - min.x) + Mathf.Min(0f, Screen.width - margin - max.x),
            Mathf.Max(0f, margin - min.y) + Mathf.Min(0f, Screen.height - margin - max.y));
        var parent = _root.parent as RectTransform;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(UiCamera, _root.position);
        if (parent != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                parent, position + delta, UiCamera, out var worldPosition))
        {
            _root.position = worldPosition;
        }

        if (_revealSelection)
        {
            _revealSelection = false;
            float offset = (_selected / _grid.constraintCount) * (_grid.cellSize.y + Spacing);
            float overflow = Mathf.Max(0f, _content.rect.height - _viewport.rect.height);
            _content.anchoredPosition = new Vector2(0f, Mathf.Clamp(offset, 0f, overflow));
        }
    }

    private void OnDisable()
    {
        // 禁用回调发生在父物体停用过程中，下一帧再改父节点才能避免 Unity 层级异常。
        QueueRestore();
    }

    private void QueueRestore()
    {
        if (_content == null || _originalParent == null || _restoreQueued)
        {
            return;
        }

        _restoreQueued = true;
        Dew.GetCoroutiner().StartCoroutine(RestoreNextFrame());
    }

    // 延迟恢复条目父节点，避开 Unity 正在派发启用/停用回调的时间窗口。
    private IEnumerator RestoreNextFrame()
    {
        yield return null;
        _restoreQueued = false;
        if (this == null)
        {
            yield break;
        }

        // 菜单已经重新打开时，放弃旧的恢复请求，避免覆盖新一轮布局。
        if (isActiveAndEnabled)
        {
            yield break;
        }

        Restore();
    }

    private void Restore()
    {
        if (_content == null || _originalParent == null) return;
        foreach (var pair in _itemScales)
        {
            if (pair.Key == null) continue;
            pair.Key.SetParent(_originalParent, false);
            pair.Key.localScale = pair.Value;
        }

        _itemScales.Clear();
        // Keep the auxiliary hierarchy alive between menu toggles. The vanilla
        // menu intentionally caches its item components in _items and may reuse
        // them on the very next OnEnable, before deferred Destroy has run.
        if (_viewport != null)
        {
            _viewport.gameObject.SetActive(false);
        }
        if (_originalLayout != null) _originalLayout.enabled = _layoutEnabled;
        if (_originalFitter != null) _originalFitter.enabled = _fitterEnabled;
        _root.sizeDelta = _originalSize;
    }

    public static void RestoreAll()
    {
        foreach (var layout in Resources.FindObjectsOfTypeAll<MemoryMenuLayout>())
        {
            // Mod 卸载时也可能正处于菜单停用回调，恢复和销毁统一延后一帧。
            Dew.GetCoroutiner().StartCoroutine(RestoreAndDestroyNextFrame(layout));
        }
    }

    // 卸载布局时先恢复原始层级，再销毁滚动容器，避免 SetParent 发生在停用回调中。
    private static IEnumerator RestoreAndDestroyNextFrame(MemoryMenuLayout layout)
    {
        yield return null;
        if (layout == null)
        {
            yield break;
        }

        layout._restoreQueued = false;
        layout.Restore();
        if (layout._viewport != null)
        {
            Destroy(layout._viewport.gameObject);
        }

        Destroy(layout);
    }
}
