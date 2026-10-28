using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Barabashka.Media
{
    /// <summary>
    /// Ползунок для перемотки и громкости: нажатие и перетаскивание задают значение от 0 до 1.
    /// Само не двигается во время перетаскивания, пока пользователь держит палец.
    /// </summary>
    internal sealed class ScrubBar : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform _track;
        private RectTransform _fill;
        private RectTransform _knob;
        private float _value;
        private bool _dragging;

        public event Action Began;
        public event Action<float> Changed;
        public event Action<float> Ended;

        public bool Dragging { get { return _dragging; } }

        public float Value
        {
            get { return _value; }
            set
            {
                if (_dragging) return;
                SetVisual(value);
            }
        }

        public static ScrubBar Create(Transform parent, string name, Color fillColor, Color trackColor)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrubBar));
            root.transform.SetParent(parent, false);
            Image hit = root.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            ScrubBar bar = root.GetComponent<ScrubBar>();

            GameObject track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(root.transform, false);
            Image trackImage = track.GetComponent<Image>();
            trackImage.sprite = ViewerArt.Rounded();
            trackImage.type = Image.Type.Sliced;
            trackImage.color = trackColor;
            trackImage.raycastTarget = false;
            bar._track = track.GetComponent<RectTransform>();
            bar._track.anchorMin = new Vector2(0f, 0.5f);
            bar._track.anchorMax = new Vector2(1f, 0.5f);
            bar._track.offsetMin = new Vector2(26f, -8f);
            bar._track.offsetMax = new Vector2(-26f, 8f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            Image fillImage = fill.GetComponent<Image>();
            fillImage.sprite = ViewerArt.Rounded();
            fillImage.type = Image.Type.Sliced;
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;
            bar._fill = fill.GetComponent<RectTransform>();
            bar._fill.anchorMin = Vector2.zero;
            bar._fill.anchorMax = new Vector2(0f, 1f);
            bar._fill.offsetMin = Vector2.zero;
            bar._fill.offsetMax = Vector2.zero;

            GameObject knob = new GameObject("Knob", typeof(RectTransform), typeof(Image));
            knob.transform.SetParent(track.transform, false);
            Image knobImage = knob.GetComponent<Image>();
            knobImage.sprite = ViewerArt.Rounded();
            knobImage.type = Image.Type.Sliced;
            knobImage.color = Color.white;
            knobImage.raycastTarget = false;
            bar._knob = knob.GetComponent<RectTransform>();
            bar._knob.sizeDelta = new Vector2(44f, 44f);
            bar._knob.anchorMin = bar._knob.anchorMax = new Vector2(0f, 0.5f);
            bar._knob.anchoredPosition = Vector2.zero;
            return bar;
        }

        private void SetVisual(float value)
        {
            _value = Mathf.Clamp01(value);
            if (_fill == null) return;
            _fill.anchorMax = new Vector2(_value, 1f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
            _knob.anchorMin = _knob.anchorMax = new Vector2(_value, 0.5f);
            _knob.anchoredPosition = Vector2.zero;
        }

        private float FromPointer(PointerEventData e)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, e.position, null, out local)) return _value;
            Rect rect = _track.rect;
            if (rect.width < 1f) return _value;
            return Mathf.Clamp01((local.x - rect.xMin) / rect.width);
        }

        public void OnPointerDown(PointerEventData e)
        {
            _dragging = true;
            Action began = Began;
            if (began != null) began();
            SetVisual(FromPointer(e));
            Action<float> changed = Changed;
            if (changed != null) changed(_value);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            SetVisual(FromPointer(e));
            Action<float> changed = Changed;
            if (changed != null) changed(_value);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging) return;
            SetVisual(FromPointer(e));
            _dragging = false;
            Action<float> ended = Ended;
            if (ended != null) ended(_value);
        }
    }
}
