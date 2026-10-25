using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Смахивание сообщения вправо. Вертикальное движение отдаётся прокрутке чата, поэтому лента листается как обычно.
/// </summary>
public sealed class SwipeToDismiss : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float DismissFraction = 0.35f;

    private ScrollRect _scroll;
    private Action _onDismiss;
    private RectTransform _rect;
    private CanvasGroup _group;
    private Canvas _canvas;
    private bool _horizontal;
    private float _startX;

    public void Init(ScrollRect scroll, Action onDismiss)
    {
        _scroll = scroll;
        _onDismiss = onDismiss;
        _rect = GetComponent<RectTransform>();
        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
    }

    private float Scale()
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        return _canvas != null && _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;
    }

    private float Shift(PointerEventData e)
    {
        return Mathf.Max(0f, (e.position.x - e.pressPosition.x) / Scale());
    }

    public void OnBeginDrag(PointerEventData e)
    {
        Vector2 total = e.position - e.pressPosition;
        _horizontal = Mathf.Abs(total.x) > Mathf.Abs(total.y);
        if (!_horizontal)
        {
            if (_scroll != null) _scroll.OnBeginDrag(e);
            return;
        }
        _startX = _rect.anchoredPosition.x;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_horizontal)
        {
            if (_scroll != null) _scroll.OnDrag(e);
            return;
        }
        float dx = Shift(e);
        _rect.anchoredPosition = new Vector2(_startX + dx, _rect.anchoredPosition.y);
        _group.alpha = 1f - Mathf.Clamp01(dx / Mathf.Max(1f, _rect.rect.width * 0.9f));
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_horizontal)
        {
            if (_scroll != null) _scroll.OnEndDrag(e);
            return;
        }
        float dx = Shift(e);
        if (dx > _rect.rect.width * DismissFraction)
        {
            if (_onDismiss != null) _onDismiss();
            return;
        }
        _rect.anchoredPosition = new Vector2(_startX, _rect.anchoredPosition.y);
        _group.alpha = 1f;
    }
}
