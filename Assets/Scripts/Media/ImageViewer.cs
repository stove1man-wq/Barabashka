using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Barabashka.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Barabashka.Media
{
    /// <summary>
    /// Полноэкранный просмотр картинок поверх приложения (этап 1 из docs/MEDIA.md).
    /// Листание свайпом влево/вправо, увеличение щипком и двойным касанием, сдвиг увеличенной картинки пальцем,
    /// закрытие свайпом вниз, кнопкой «назад» и голосом (через IMediaViewer). Интерфейс строится кодом при первом показе.
    /// </summary>
    public sealed class ImageViewer : MonoBehaviour, IMediaViewer
    {
        private const float MinScale = 1f;
        private const float MaxScale = 5f;
        private const float DoubleTapScale = 2.5f;
        private const float DoubleTapSeconds = 0.3f;
        private const float TapMaxMove = 15f;          // пиксели экрана
        private const float TapMaxSeconds = 0.35f;
        private const float SwipeChangeDistance = 140f;  // единицы холста (1080 x 1920)
        private const float SwipeCloseDistance = 240f;
        private const float TopBarHeight = 150f;
        private const float BottomBarHeight = 200f;

        private readonly List<MediaItem> _items = new List<MediaItem>();
        private readonly Dictionary<int, Vector2> _pointers = new Dictionary<int, Vector2>();

        private int _index;
        private Font _font;
        private GameObject _root;
        private Canvas _canvas;
        private RectTransform _area;
        private RectTransform _topBar;
        private RectTransform _bottomBar;
        private RawImage _picture;
        private Text _title;
        private Text _counter;
        private Text _message;
        private Button _prevButton;
        private Button _nextButton;
        private Texture2D _texture;
        private int _loadVersion;

        // Положение картинки: масштаб, сдвиг при увеличении, смещение при свайпе (единицы холста).
        private float _scale = 1f;
        private Vector2 _offset;
        private Vector2 _swipe;
        private Vector2 _swipeRaw;
        private int _axis = -1;

        // Жесты.
        private Vector2 _downPosition;
        private float _downTime;
        private bool _pinching;
        private float _pinchDistance;
        private float _pinchScale;
        private Vector2 _pinchMid;
        private Vector2 _pinchOffset;
        private float _lastTapTime = -10f;
        private Vector2 _lastTapPosition;

        private Vector2Int _lastScreen;
        private Rect _lastSafeArea;

        public event Action Closed;

        public bool IsOpen { get { return _root != null && _root.activeSelf; } }

        private float CanvasScale { get { return _canvas != null ? Mathf.Max(0.01f, _canvas.scaleFactor) : 1f; } }

        // ---- IMediaViewer ----

        public void Show(IReadOnlyList<MediaItem> items, int startIndex)
        {
            _items.Clear();
            int start = 0;
            MediaItem wanted = (items != null && startIndex >= 0 && startIndex < items.Count) ? items[startIndex] : null;
            if (items != null)
            {
                foreach (MediaItem item in items)
                {
                    if (item == null || item.Kind != MediaKind.Image || string.IsNullOrEmpty(item.Uri)) continue;
                    if (item == wanted) start = _items.Count;
                    _items.Add(item);
                }
            }
            if (_items.Count == 0) return;

            EnsureUi();
            _root.SetActive(true);
            ApplyLayout();
            GoTo(start);
        }

        public void Close()
        {
            if (!IsOpen) return;
            _loadVersion++;
            _pointers.Clear();
            _pinching = false;
            _root.SetActive(false);
            ClearPicture();
            Action handler = Closed;
            if (handler != null) handler();
        }

        public bool Next()
        {
            if (!IsOpen || _index + 1 >= _items.Count) return false;
            GoTo(_index + 1);
            return true;
        }

        public bool Previous()
        {
            if (!IsOpen || _index <= 0) return false;
            GoTo(_index - 1);
            return true;
        }

        // ---- построение интерфейса ----

        private void EnsureUi()
        {
            if (_root != null) return;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _root = new GameObject("BarabashkaMediaViewer", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            _root.transform.SetParent(transform, false);
            _canvas = _root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            _root.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.98f);

            // Область картинки: ловит жесты и обрезает увеличенную картинку по своим границам.
            GameObject areaObject = new GameObject("PictureArea", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            areaObject.transform.SetParent(_root.transform, false);
            _area = areaObject.GetComponent<RectTransform>();
            Image areaImage = areaObject.GetComponent<Image>();
            areaImage.color = new Color(0f, 0f, 0f, 0f);
            areaImage.raycastTarget = true;
            TouchSurface surface = areaObject.AddComponent<TouchSurface>();
            surface.Owner = this;

            GameObject pictureObject = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            pictureObject.transform.SetParent(_area, false);
            _picture = pictureObject.GetComponent<RawImage>();
            _picture.raycastTarget = false;
            RectTransform pictureRect = _picture.rectTransform;
            pictureRect.anchorMin = pictureRect.anchorMax = pictureRect.pivot = new Vector2(0.5f, 0.5f);

            _message = MakeText("Message", _area, 40, new Color(0.78f, 0.84f, 0.93f, 1f), TextAnchor.MiddleCenter);
            Stretch(_message.rectTransform, new Vector2(60f, 60f), new Vector2(-60f, -60f));

            // Верхняя панель: назад, название, счётчик.
            _topBar = MakeBar("TopBar", true);
            RectTransform topContent = MakeBarContent(_topBar, true, TopBarHeight);
            Button back = MakeButton("Back", topContent, "back", new Vector2(120f, 120f));
            SetAnchoredLeft(back.GetComponent<RectTransform>(), 30f);
            back.onClick.AddListener(Close);
            _title = MakeText("Title", topContent, 40, new Color(0.93f, 0.95f, 0.98f, 1f), TextAnchor.MiddleLeft);
            _title.horizontalOverflow = HorizontalWrapMode.Wrap;
            _title.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(_title.rectTransform, new Vector2(170f, 10f), new Vector2(-230f, -10f));
            _counter = MakeText("Counter", topContent, 36, new Color(0.62f, 0.7f, 0.82f, 1f), TextAnchor.MiddleRight);
            Stretch(_counter.rectTransform, new Vector2(-230f, 10f), new Vector2(-30f, -10f));
            _counter.rectTransform.anchorMin = new Vector2(1f, 0f);
            _counter.rectTransform.anchorMax = new Vector2(1f, 1f);
            _counter.rectTransform.pivot = new Vector2(1f, 0.5f);
            _counter.rectTransform.offsetMin = new Vector2(-230f, 10f);
            _counter.rectTransform.offsetMax = new Vector2(-30f, -10f);

            // Нижняя панель: предыдущая, подсказка, следующая.
            _bottomBar = MakeBar("BottomBar", false);
            RectTransform bottomContent = MakeBarContent(_bottomBar, false, BottomBarHeight);
            _prevButton = MakeButton("Prev", bottomContent, "back", new Vector2(140f, 140f));
            SetAnchoredLeft(_prevButton.GetComponent<RectTransform>(), 30f);
            _prevButton.onClick.AddListener(() => Previous());
            _nextButton = MakeButton("Next", bottomContent, "next", new Vector2(140f, 140f));
            RectTransform nextRect = _nextButton.GetComponent<RectTransform>();
            nextRect.anchorMin = nextRect.anchorMax = new Vector2(1f, 0.5f);
            nextRect.pivot = new Vector2(1f, 0.5f);
            nextRect.anchoredPosition = new Vector2(-30f, 0f);
            _nextButton.onClick.AddListener(() => Next());
            Text hint = MakeText("Hint", bottomContent, 30, new Color(0.55f, 0.63f, 0.76f, 1f), TextAnchor.MiddleCenter);
            hint.text = "Скажите: «дальше», «назад», «закрой»";
            Stretch(hint.rectTransform, new Vector2(190f, 10f), new Vector2(-190f, -10f));

            _root.SetActive(false);
        }

        private RectTransform MakeBar(string name, bool top)
        {
            GameObject bar = new GameObject(name, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(_root.transform, false);
            bar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            return rect;
        }

        // Содержимое панели занимает её «рабочую» часть; отступ под системные панели остаётся пустым.
        private static RectTransform MakeBarContent(RectTransform bar, bool top, float height)
        {
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(bar, false);
            RectTransform rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, top ? 0f : 1f);
            rect.anchorMax = new Vector2(1f, top ? 0f : 1f);
            rect.pivot = new Vector2(0.5f, top ? 0f : 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private Button MakeButton(string name, Transform parent, string icon, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = ViewerArt.Rounded();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.16f, 0.21f, 0.30f, 1f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
            button.colors = colors;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(go.transform, false);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = ViewerArt.Icon(icon);
            iconImage.raycastTarget = false;
            Stretch(iconImage.rectTransform, new Vector2(16f, 16f), new Vector2(-16f, -16f));
            return button;
        }

        private Text MakeText(string name, Transform parent, int size, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetAnchoredLeft(RectTransform rect, float x)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        // Размеры панелей и области картинки с учётом вырезов и системных панелей.
        private void ApplyLayout()
        {
            if (_root == null) return;
            float scale = CanvasScale;
            Rect safe = Screen.safeArea;
            float topInset = Mathf.Max(0f, (Screen.height - safe.yMax) / scale);
            float bottomInset = Mathf.Max(0f, safe.yMin / scale);

            _topBar.sizeDelta = new Vector2(0f, TopBarHeight + topInset);
            _topBar.anchoredPosition = Vector2.zero;
            _bottomBar.sizeDelta = new Vector2(0f, BottomBarHeight + bottomInset);
            _bottomBar.anchoredPosition = Vector2.zero;

            _area.anchorMin = Vector2.zero;
            _area.anchorMax = Vector2.one;
            _area.offsetMin = new Vector2(0f, BottomBarHeight + bottomInset);
            _area.offsetMax = new Vector2(0f, -(TopBarHeight + topInset));

            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            _lastSafeArea = safe;
        }

        // ---- показ ----

        private void GoTo(int index)
        {
            _index = Mathf.Clamp(index, 0, _items.Count - 1);
            ResetTransform();
            UpdateChrome();
            StartCoroutine(Load(_items[_index], ++_loadVersion));
        }

        private void UpdateChrome()
        {
            MediaItem item = _items[_index];
            _title.text = string.IsNullOrEmpty(item.Title) ? string.Empty : item.Title;
            _counter.text = _items.Count > 1 ? (_index + 1) + " / " + _items.Count : string.Empty;
            _prevButton.interactable = _index > 0;
            _nextButton.interactable = _index < _items.Count - 1;
        }

        private IEnumerator Load(MediaItem item, int version)
        {
            ClearPicture();
            ShowMessage("Загрузка…");

            string url = ToUrl(item.Uri);
            if (url == null)
            {
                ShowMessage("Не удалось открыть файл");
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true))
            {
                yield return request.SendWebRequest();
                if (version != _loadVersion) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[Media] Не удалось загрузить " + url + ": " + request.error);
                    ShowMessage("Не удалось открыть файл");
                    yield break;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    ShowMessage("Не удалось открыть файл");
                    yield break;
                }
                texture.wrapMode = TextureWrapMode.Clamp;
                _texture = texture;
                _picture.texture = texture;
                _picture.color = Color.white;
                _message.gameObject.SetActive(false);
                ResetTransform();
            }
        }

        // Локальный путь превращаем в адрес file://, адреса http(s) и file:// оставляем как есть.
        private static string ToUrl(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return null;
            string value = uri.Trim();
            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                return value;
            try
            {
                if (Path.IsPathRooted(value)) return new System.Uri(value).AbsoluteUri;
            }
            catch (Exception) { }
            return null;
        }

        private void ShowMessage(string text)
        {
            _message.text = text;
            _message.gameObject.SetActive(true);
        }

        private void ClearPicture()
        {
            if (_picture != null)
            {
                _picture.texture = null;
                _picture.color = new Color(1f, 1f, 1f, 0f);
            }
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }

        private void ResetTransform()
        {
            _scale = 1f;
            _offset = Vector2.zero;
            _swipe = Vector2.zero;
            _swipeRaw = Vector2.zero;
            _axis = -1;
            Apply();
        }

        // Размер картинки при масштабе 1: вписана в область целиком.
        private Vector2 FitSize()
        {
            if (_texture == null || _area == null) return Vector2.zero;
            Rect rect = _area.rect;
            float k = Mathf.Min(rect.width / _texture.width, rect.height / _texture.height);
            return new Vector2(_texture.width * k, _texture.height * k);
        }

        private void Apply()
        {
            if (_picture == null) return;
            RectTransform rect = _picture.rectTransform;
            rect.sizeDelta = FitSize() * _scale;
            rect.anchoredPosition = _offset + _swipe;
        }

        private void ClampOffset()
        {
            if (_area == null) return;
            Vector2 shown = FitSize() * _scale;
            Rect rect = _area.rect;
            float maxX = Mathf.Max(0f, (shown.x - rect.width) * 0.5f);
            float maxY = Mathf.Max(0f, (shown.y - rect.height) * 0.5f);
            _offset.x = Mathf.Clamp(_offset.x, -maxX, maxX);
            _offset.y = Mathf.Clamp(_offset.y, -maxY, maxY);
        }

        // ---- жесты ----

        private sealed class TouchSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
        {
            public ImageViewer Owner;
            public void OnPointerDown(PointerEventData eventData) { if (Owner != null) Owner.PointerDown(eventData); }
            public void OnPointerUp(PointerEventData eventData) { if (Owner != null) Owner.PointerUp(eventData); }
            public void OnDrag(PointerEventData eventData) { if (Owner != null) Owner.PointerDrag(eventData); }
        }

        private bool TwoPoints(out Vector2 a, out Vector2 b)
        {
            a = Vector2.zero;
            b = Vector2.zero;
            int n = 0;
            foreach (KeyValuePair<int, Vector2> pair in _pointers)
            {
                if (n == 0) a = pair.Value;
                else { b = pair.Value; return true; }
                n++;
            }
            return false;
        }

        private void PointerDown(PointerEventData e)
        {
            if (_pointers.Count == 0)
            {
                _downPosition = e.position;
                _downTime = Time.unscaledTime;
                _swipeRaw = Vector2.zero;
                _swipe = Vector2.zero;
                _axis = -1;
            }
            _pointers[e.pointerId] = e.position;

            Vector2 a, b;
            if (_pointers.Count >= 2 && TwoPoints(out a, out b))
            {
                _pinching = true;
                _pinchDistance = Mathf.Max(1f, Vector2.Distance(a, b));
                _pinchScale = _scale;
                _pinchMid = (a + b) * 0.5f;
                _pinchOffset = _offset;
                _swipe = Vector2.zero;
                Apply();
            }
        }

        private void PointerDrag(PointerEventData e)
        {
            if (!_pointers.ContainsKey(e.pointerId)) return;
            _pointers[e.pointerId] = e.position;

            Vector2 a, b;
            if (_pinching)
            {
                if (_pointers.Count >= 2 && TwoPoints(out a, out b))
                {
                    float distance = Mathf.Max(1f, Vector2.Distance(a, b));
                    float newScale = Mathf.Clamp(_pinchScale * distance / _pinchDistance, MinScale, MaxScale);
                    Vector2 mid = (a + b) * 0.5f;
                    _offset = _pinchOffset * (newScale / _pinchScale) + (mid - _pinchMid) / CanvasScale;
                    _scale = newScale;
                    ClampOffset();
                    Apply();
                }
                return;
            }

            Vector2 delta = e.delta / CanvasScale;
            if (_scale > 1.02f)
            {
                _offset += delta;
                ClampOffset();
            }
            else
            {
                _swipeRaw += delta;
                if (_axis < 0 && _swipeRaw.magnitude > 12f)
                    _axis = Mathf.Abs(_swipeRaw.x) >= Mathf.Abs(_swipeRaw.y) ? 0 : 1;
                if (_axis == 0) _swipe = new Vector2(_swipeRaw.x, 0f);
                else if (_axis == 1) _swipe = new Vector2(0f, Mathf.Min(0f, _swipeRaw.y));
            }
            Apply();
        }

        private void PointerUp(PointerEventData e)
        {
            if (!_pointers.Remove(e.pointerId)) return;
            if (_pointers.Count > 0) return;

            if (_pinching)
            {
                _pinching = false;
                if (_scale < 1.05f)
                {
                    _scale = 1f;
                    _offset = Vector2.zero;
                }
                ClampOffset();
                Apply();
                return;
            }

            float moved = (e.position - _downPosition).magnitude;
            float held = Time.unscaledTime - _downTime;
            bool tap = moved < TapMaxMove && held < TapMaxSeconds;

            if (tap)
            {
                float now = Time.unscaledTime;
                if (now - _lastTapTime < DoubleTapSeconds && (e.position - _lastTapPosition).magnitude < 80f)
                {
                    ToggleZoom(e.position);
                    _lastTapTime = -10f;
                }
                else
                {
                    _lastTapTime = now;
                    _lastTapPosition = e.position;
                }
                _swipe = Vector2.zero;
                Apply();
                return;
            }

            Vector2 raw = _swipeRaw;
            int axis = _axis;
            bool zoomed = _scale > 1.02f;
            _swipe = Vector2.zero;
            _swipeRaw = Vector2.zero;
            _axis = -1;
            Apply();
            if (zoomed) return;

            if (axis == 0 && raw.x <= -SwipeChangeDistance) Next();
            else if (axis == 0 && raw.x >= SwipeChangeDistance) Previous();
            else if (axis == 1 && raw.y <= -SwipeCloseDistance) Close();
        }

        private void ToggleZoom(Vector2 screenPosition)
        {
            if (_scale > 1.1f)
            {
                _scale = 1f;
                _offset = Vector2.zero;
            }
            else
            {
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screenPosition, null, out local);
                _scale = DoubleTapScale;
                _offset = local * (1f - DoubleTapScale);
                ClampOffset();
            }
            Apply();
        }

        // ---- обновление ----

        private void Update()
        {
            if (!IsOpen) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            // Колесо мыши (в редакторе и на ПК) меняет масштаб.
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && _texture != null)
                {
                    _scale = Mathf.Clamp(_scale * (1f + wheel * 0.0015f), MinScale, MaxScale);
                    if (_scale < 1.02f)
                    {
                        _scale = 1f;
                        _offset = Vector2.zero;
                    }
                    ClampOffset();
                    Apply();
                }
            }

            // Поворот экрана или смена выреза.
            if (Screen.width != _lastScreen.x || Screen.height != _lastScreen.y || Screen.safeArea != _lastSafeArea)
            {
                ApplyLayout();
                ClampOffset();
                Apply();
            }
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }
    }
}
