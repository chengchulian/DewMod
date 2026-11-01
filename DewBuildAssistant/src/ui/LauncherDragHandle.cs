using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DewBuildAssistant.ui;

// 使用画布归一化坐标保存入口位置，缩放和分辨率变化后仍保持在可点击范围。
internal sealed class LauncherDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    private RectTransform _rect;
    private RectTransform _canvas;
    private Vector2 _offset;
    private Vector2 _before;
    private Vector2 _normalized = new(0.97f, 0.955f);
    private bool _moving;
    private bool _dragged;
    private int _endedFrame = -10;
    private Vector2 _lastSize;
    private static string PathName => Path.Combine(Application.persistentDataPath, "BD-Assistant", "ui-position.json");
    private sealed class Position { public float X; public float Y; }

    public bool IgnoreClick => _moving || _dragged || Time.frameCount <= _endedFrame + 1;

    private void Start()
    {
        _rect = (RectTransform)transform;
        _canvas = (RectTransform)GetComponentInParent<Canvas>().transform;
        _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
        try
        {
            if (File.Exists(PathName))
            {
                var p = JsonConvert.DeserializeObject<Position>(File.ReadAllText(PathName, Encoding.UTF8));
                if (p != null && !float.IsNaN(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.X) && !float.IsInfinity(p.Y))
                    _normalized = new Vector2(Mathf.Clamp01(p.X), Mathf.Clamp01(p.Y));
            }
        }
        catch (Exception ex) { Debug.LogWarning("[BD 助手] 入口位置读取失败：" + ex.Message); }
        Restore();
    }

    private void Update()
    {
        if (_canvas != null && !_moving && _canvas.rect.size != _lastSize) Restore();
    }

    private void Restore()
    {
        _lastSize = _canvas.rect.size;
        _rect.anchoredPosition = Clamp(new Vector2(Mathf.Lerp(_canvas.rect.xMin, _canvas.rect.xMax, _normalized.x),
            Mathf.Lerp(_canvas.rect.yMin, _canvas.rect.yMax, _normalized.y)));
    }

    public void OnPointerDown(PointerEventData eventData) { _dragged = false; }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _canvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, eventData.position, eventData.pressEventCamera, out var point);
        _before = _rect.anchoredPosition;
        _offset = _before - point;
        _moving = _dragged = true;
        eventData.eligibleForClick = false;
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!_moving) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, eventData.position, eventData.pressEventCamera, out var point);
        _rect.anchoredPosition = Clamp(point + _offset);
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_moving) return;
        _moving = false;
        _endedFrame = Time.frameCount;
        _normalized = new Vector2(Mathf.InverseLerp(_canvas.rect.xMin, _canvas.rect.xMax, _rect.anchoredPosition.x),
            Mathf.InverseLerp(_canvas.rect.yMin, _canvas.rect.yMax, _rect.anchoredPosition.y));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathName));
            File.WriteAllText(PathName, JsonConvert.SerializeObject(new Position { X = _normalized.x, Y = _normalized.y }), new UTF8Encoding(false));
        }
        catch (Exception ex) { Debug.LogWarning("[BD 助手] 入口位置保存失败：" + ex.Message); }
    }
    public bool CancelDrag()
    {
        if (!_moving) return false;
        _moving = false;
        _rect.anchoredPosition = _before;
        _endedFrame = Time.frameCount;
        return true;
    }
    private void OnDisable() { CancelDrag(); }
    private Vector2 Clamp(Vector2 point)
    {
        var half = _rect.rect.size * 0.5f + Vector2.one * 4;
        return new Vector2(Mathf.Clamp(point.x, _canvas.rect.xMin + half.x, _canvas.rect.xMax - half.x),
            Mathf.Clamp(point.y, _canvas.rect.yMin + half.y, _canvas.rect.yMax - half.y));
    }
}
