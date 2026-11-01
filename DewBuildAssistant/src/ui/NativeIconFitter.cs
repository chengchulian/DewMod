using UnityEngine;
using UnityEngine.UI;

namespace DewBuildAssistant.ui;

// 在原生图标 Setup 后测量当前可见层，保持其内部像素布局、遮罩和描边比例。
internal sealed class NativeIconFitter : MonoBehaviour
{
    private RectTransform _rect, _container;
    private Vector2 _lastSize;
    private Vector2 _min, _max;
    private bool _measured;

    public static void Attach(Component icon)
    {
        var fitter = icon.gameObject.AddComponent<NativeIconFitter>();
        fitter._rect = (RectTransform)icon.transform;
        fitter._container = icon.transform.parent as RectTransform;
        fitter.Measure();
        fitter.LateUpdate();
    }

    private void Measure()
    {
        _min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        _max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var corners = new Vector3[4];
        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (!graphic.enabled || graphic.color.a <= 0) continue;
            bool visible = true;
            for (var node = graphic.transform; node != _rect.parent; node = node.parent)
                if (!node.gameObject.activeSelf) { visible = false; break; }
            if (!visible) continue;
            graphic.rectTransform.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 local = _rect.InverseTransformPoint(corner);
                _min = Vector2.Min(_min, local);
                _max = Vector2.Max(_max, local);
            }
            _measured = true;
        }
    }

    private void LateUpdate()
    {
        if (!_measured || _container == null) return;
        var size = _container.rect.size;
        if (size == _lastSize) return;
        _lastSize = size;
        if (!NativeIconFit.Calculate(size.x, size.y, _min.x, _min.y, _max.x, _max.y,
            out float scale, out float x, out float y)) return;
        _rect.localScale = new Vector3(scale, scale, 1);
        _rect.anchoredPosition = new Vector2(x, y);
    }
}
