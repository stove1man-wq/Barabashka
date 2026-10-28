using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Barabashka.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Barabashka.Media
{
    /// <summary>
    /// Видеоплеер поверх приложения (этап 2 из docs/MEDIA.md): файлы с телефона и прямые ссылки.
    /// Кнопки: назад, перемотка на 10 секунд, пауза, предыдущее/следующее, скорость, громкость; ползунок перемотки;
    /// касание видео ставит на паузу. Команды голосом приходят через IMediaViewer и IMediaPlayback.
    /// </summary>
    public sealed class VideoPlayerView : MonoBehaviour, IMediaViewer, IMediaPlayback
    {
        private const float TopBarHeight = 150f;
        private const float ControlsHeight = 540f;
        private const float PrepareTimeoutSeconds = 25f;
        private const double SeekStepSeconds = 10.0;
        private static readonly float[] Speeds = { 1f, 1.25f, 1.5f, 2f, 0.5f };

        private static readonly Color Panel = new Color(0.16f, 0.21f, 0.30f, 1f);
        private static readonly Color Accent = new Color(0.36f, 0.62f, 1f, 1f);

        private readonly List<MediaItem> _items = new List<MediaItem>();

        private int _index;
        private Font _font;
        private GameObject _root;
        private Canvas _canvas;
        private RectTransform _topBar;
        private RectTransform _controls;
        private RectTransform _videoArea;
        private RawImage _picture;
        private Text _message;
        private Text _title;
        private Text _counter;
        private Text _timeText;
        private Text _durationText;
        private Text _speedText;
        private Image _playIcon;
        private Button _prevButton;
        private Button _nextButton;
        private ScrubBar _seek;
        private ScrubBar _volume;

        private VideoPlayer _player;
        private int _loadVersion;
        private bool _ready;
        private bool _failed;
        private bool _ended;
        private bool _advance;
        private bool _wasPlaying;
        private float _speed = 1f;
        private float _volumeValue = 1f;

        private Vector2Int _lastScreen;
        private Rect _lastSafeArea;

        public event Action Closed;

        public bool IsOpen { get { return _root != null && _root.activeSelf; } }

        public bool HasVideo { get { return IsOpen; } }

        public bool IsPlaying { get { return _ready && _player != null && _player.isPlaying; } }

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
                    if (item == null || item.Kind != MediaKind.Video || string.IsNullOrEmpty(item.Uri)) continue;
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
            StopPlayback();
            _root.SetActive(false);
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

        // ---- IMediaPlayback ----

        public void Play()
        {
            if (!_ready || _player == null) return;
            if (_ended)
            {
                _player.time = 0.0;
                _ended = false;
            }
            _player.Play();
            UpdatePlayIcon();
        }

        public void Pause()
        {
            if (!_ready || _player == null) return;
            _player.Pause();
            UpdatePlayIcon();
        }

        public void SeekBy(double seconds)
        {
            if (!_ready || _player == null) return;
            double length = _player.length;
            double target = _player.time + seconds;
            if (length > 0.5) target = Math.Min(target, length - 0.3);
            _player.time = Math.Max(0.0, target);
            _ended = false;
        }

        public void SetSpeed(float speed)
        {
            _speed = Mathf.Clamp(speed, 0.25f, 4f);
            ApplySpeed();
        }

        // ---- построение интерфейса ----

        private void EnsureUi()
        {
            if (_root != null) return;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Сам проигрыватель живёт вне интерфейса, чтобы не отключаться вместе с ним.
            GameObject playerObject = new GameObject("BarabashkaVideoPlayer", typeof(VideoPlayer));
            playerObject.transform.SetParent(transform, false);
            _player = playerObject.GetComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.waitForFirstFrame = true;
            _player.skipOnDrop = true;
            _player.renderMode = VideoRenderMode.APIOnly;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.source = VideoSource.Url;
            _player.errorReceived += OnPlayerError;
            _player.loopPointReached += OnLoopPoint;

            _root = new GameObject("BarabashkaVideoViewer", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            _root.transform.SetParent(transform, false);
            _canvas = _root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            _root.GetComponent<Image>().color = new Color(0.01f, 0.015f, 0.03f, 1f);

            // Область видео; касание ставит на паузу и снимает её.
            GameObject areaObject = new GameObject("VideoArea", typeof(RectTransform), typeof(Image), typeof(Button), typeof(RectMask2D));
            areaObject.transform.SetParent(_root.transform, false);
            _videoArea = areaObject.GetComponent<RectTransform>();
            Image areaImage = areaObject.GetComponent<Image>();
            areaImage.color = new Color(0f, 0f, 0f, 0f);
            Button areaButton = areaObject.GetComponent<Button>();
            areaButton.transition = Selectable.Transition.None;
            areaButton.onClick.AddListener(TogglePlay);

            GameObject pictureObject = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            pictureObject.transform.SetParent(_videoArea, false);
            _picture = pictureObject.GetComponent<RawImage>();
            _picture.raycastTarget = false;
            _picture.color = new Color(1f, 1f, 1f, 0f);
            RectTransform pictureRect = _picture.rectTransform;
            pictureRect.anchorMin = pictureRect.anchorMax = pictureRect.pivot = new Vector2(0.5f, 0.5f);

            _message = MakeText("Message", _videoArea, 40, new Color(0.78f, 0.84f, 0.93f, 1f), TextAnchor.MiddleCenter);
            Stretch(_message.rectTransform, new Vector2(60f, 60f), new Vector2(-60f, -60f));

            BuildTopBar();
            BuildControls();
            _root.SetActive(false);
        }

        private void BuildTopBar()
        {
            _topBar = MakeBar("TopBar", true);
            RectTransform content = MakeBarContent(_topBar, true, TopBarHeight);
            Image dummy;
            Button back = MakeButton("Back", content, ViewerArt.Icon("back"), new Vector2(120f, 120f), out dummy);
            SetAnchoredLeft(back.GetComponent<RectTransform>(), 30f);
            back.onClick.AddListener(Close);

            _title = MakeText("Title", content, 40, new Color(0.93f, 0.95f, 0.98f, 1f), TextAnchor.MiddleLeft);
            _title.horizontalOverflow = HorizontalWrapMode.Wrap;
            _title.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(_title.rectTransform, new Vector2(170f, 10f), new Vector2(-230f, -10f));

            _counter = MakeText("Counter", content, 36, new Color(0.62f, 0.7f, 0.82f, 1f), TextAnchor.MiddleRight);
            RectTransform counterRect = _counter.rectTransform;
            counterRect.anchorMin = new Vector2(1f, 0f);
            counterRect.anchorMax = new Vector2(1f, 1f);
            counterRect.pivot = new Vector2(1f, 0.5f);
            counterRect.offsetMin = new Vector2(-230f, 10f);
            counterRect.offsetMax = new Vector2(-30f, -10f);
        }

        private void BuildControls()
        {
            _controls = MakeBar("Controls", false);
            RectTransform content = MakeBarContent(_controls, false, ControlsHeight);
            Color white = new Color(0.62f, 0.7f, 0.82f, 1f);

            // Время.
            RectTransform timeRow = MakeRow("TimeRow", content, 10f, 50f);
            _timeText = MakeText("Time", timeRow, 34, new Color(0.93f, 0.95f, 0.98f, 1f), TextAnchor.MiddleLeft);
            Stretch(_timeText.rectTransform, new Vector2(40f, 0f), new Vector2(-540f, 0f));
            _durationText = MakeText("Duration", timeRow, 34, white, TextAnchor.MiddleRight);
            Stretch(_durationText.rectTransform, new Vector2(540f, 0f), new Vector2(-40f, 0f));

            // Перемотка.
            RectTransform seekRow = MakeRow("SeekRow", content, 60f, 80f);
            _seek = ScrubBar.Create(seekRow, "Seek", Accent, new Color(0.28f, 0.34f, 0.46f, 1f));
            Stretch(_seek.GetComponent<RectTransform>(), new Vector2(14f, 0f), new Vector2(-14f, 0f));
            _seek.Changed += OnSeekChanged;
            _seek.Ended += OnSeekEnded;

            // Кнопки: предыдущее, -10, пауза, +10, следующее.
            RectTransform transport = MakeRow("Transport", content, 150f, 170f);
            Image icon;
            _prevButton = MakeButton("Prev", transport, VideoArt.Icon("prev"), new Vector2(130f, 130f), out icon);
            PlaceCenter(_prevButton, -400f);
            _prevButton.onClick.AddListener(() => Previous());

            Button rewind = MakeButton("Rewind", transport, VideoArt.Icon("rew"), new Vector2(130f, 130f), out icon);
            PlaceCenter(rewind, -200f);
            AddCaption(rewind, "10");
            rewind.onClick.AddListener(() => SeekBy(-SeekStepSeconds));

            Button play = MakeButton("Play", transport, VideoArt.Icon("pause"), new Vector2(170f, 170f), out _playIcon);
            PlaceCenter(play, 0f);
            play.GetComponent<Image>().color = Accent;
            play.onClick.AddListener(TogglePlay);

            Button forward = MakeButton("Forward", transport, VideoArt.Icon("fwd"), new Vector2(130f, 130f), out icon);
            PlaceCenter(forward, 200f);
            AddCaption(forward, "10");
            forward.onClick.AddListener(() => SeekBy(SeekStepSeconds));

            _nextButton = MakeButton("Next", transport, VideoArt.Icon("nexttrack"), new Vector2(130f, 130f), out icon);
            PlaceCenter(_nextButton, 400f);
            _nextButton.onClick.AddListener(() => Next());

            // Скорость и громкость.
            RectTransform second = MakeRow("SecondRow", content, 340f, 110f);
            Button speed = MakeButton("Speed", second, null, new Vector2(200f, 90f), out icon);
            RectTransform speedRect = speed.GetComponent<RectTransform>();
            speedRect.anchorMin = speedRect.anchorMax = new Vector2(0f, 0.5f);
            speedRect.pivot = new Vector2(0f, 0.5f);
            speedRect.anchoredPosition = new Vector2(40f, 0f);
            _speedText = MakeText("Label", speed.transform, 40, Color.white, TextAnchor.MiddleCenter);
            Stretch(_speedText.rectTransform, Vector2.zero, Vector2.zero);
            speed.onClick.AddListener(CycleSpeed);

            GameObject speakerObject = new GameObject("Speaker", typeof(RectTransform), typeof(Image));
            speakerObject.transform.SetParent(second, false);
            Image speaker = speakerObject.GetComponent<Image>();
            speaker.sprite = VideoArt.Icon("speaker");
            speaker.color = white;
            speaker.raycastTarget = false;
            RectTransform speakerRect = speaker.rectTransform;
            speakerRect.anchorMin = speakerRect.anchorMax = new Vector2(0f, 0.5f);
            speakerRect.pivot = new Vector2(0f, 0.5f);
            speakerRect.sizeDelta = new Vector2(80f, 80f);
            speakerRect.anchoredPosition = new Vector2(290f, 0f);

            _volume = ScrubBar.Create(second, "Volume", Accent, new Color(0.28f, 0.34f, 0.46f, 1f));
            Stretch(_volume.GetComponent<RectTransform>(), new Vector2(380f, 10f), new Vector2(-30f, -10f));
            _volume.Value = _volumeValue;
            _volume.Changed += OnVolumeChanged;

            // Подсказка.
            RectTransform hintRow = MakeRow("HintRow", content, 462f, 60f);
            Text hint = MakeText("Hint", hintRow, 28, new Color(0.55f, 0.63f, 0.76f, 1f), TextAnchor.MiddleCenter);
            hint.text = "Скажите: «пауза», «продолжи», «на 10 секунд вперёд», «дальше», «закрой»";
            Stretch(hint.rectTransform, new Vector2(30f, 0f), new Vector2(-30f, 0f));
        }

        private RectTransform MakeBar(string name, bool top)
        {
            GameObject bar = new GameObject(name, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(_root.transform, false);
            bar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            return rect;
        }

        // Содержимое панели занимает её рабочую часть; отступ под системные панели остаётся пустым.
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

        // Строка внутри панели: отступ сверху и высота.
        private static RectTransform MakeRow(string name, RectTransform parent, float top, float height)
        {
            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(0f, height);
            return rect;
        }

        private static void PlaceCenter(Button button, float x)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private void AddCaption(Button button, string text)
        {
            Text caption = MakeText("Caption", button.transform, 30, Color.white, TextAnchor.LowerCenter);
            caption.text = text;
            Stretch(caption.rectTransform, new Vector2(0f, 10f), Vector2.zero);
        }

        private Button MakeButton(string name, Transform parent, Sprite iconSprite, Vector2 size, out Image icon)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = ViewerArt.Rounded();
            image.type = Image.Type.Sliced;
            image.color = Panel;
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

            icon = null;
            if (iconSprite != null)
            {
                GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(go.transform, false);
                icon = iconObject.GetComponent<Image>();
                icon.sprite = iconSprite;
                icon.raycastTarget = false;
                Stretch(icon.rectTransform, new Vector2(18f, 18f), new Vector2(-18f, -18f));
            }
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

        // Размеры панелей и области видео с учётом вырезов и системных панелей.
        private void ApplyLayout()
        {
            if (_root == null) return;
            float scale = CanvasScale;
            Rect safe = Screen.safeArea;
            float topInset = Mathf.Max(0f, (Screen.height - safe.yMax) / scale);
            float bottomInset = Mathf.Max(0f, safe.yMin / scale);

            _topBar.sizeDelta = new Vector2(0f, TopBarHeight + topInset);
            _topBar.anchoredPosition = Vector2.zero;
            _controls.sizeDelta = new Vector2(0f, ControlsHeight + bottomInset);
            _controls.anchoredPosition = Vector2.zero;

            _videoArea.anchorMin = Vector2.zero;
            _videoArea.anchorMax = Vector2.one;
            _videoArea.offsetMin = new Vector2(0f, ControlsHeight + bottomInset);
            _videoArea.offsetMax = new Vector2(0f, -(TopBarHeight + topInset));

            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            _lastSafeArea = safe;
        }

        // ---- воспроизведение ----

        private void GoTo(int index)
        {
            _index = Mathf.Clamp(index, 0, _items.Count - 1);
            UpdateChrome();
            StartCoroutine(Open(_items[_index], ++_loadVersion));
        }

        private void UpdateChrome()
        {
            MediaItem item = _items[_index];
            _title.text = string.IsNullOrEmpty(item.Title) ? string.Empty : item.Title;
            _counter.text = _items.Count > 1 ? (_index + 1) + " / " + _items.Count : string.Empty;
            _prevButton.interactable = _index > 0;
            _nextButton.interactable = _index < _items.Count - 1;
            _speedText.text = FormatSpeed(_speed);
            _durationText.text = item.DurationSeconds > 0 ? FormatTime(item.DurationSeconds) : "--:--";
            _timeText.text = "0:00";
            _seek.Value = 0f;
        }

        private IEnumerator Open(MediaItem item, int version)
        {
            StopPlayback();
            ShowMessage("Загрузка…");

            List<string> urls = Candidates(item.Uri);
            for (int attempt = 0; attempt < urls.Count; attempt++)
            {
                _failed = false;
                _player.url = urls[attempt];
                _player.Prepare();
                float started = Time.realtimeSinceStartup;
                while (!_player.isPrepared && !_failed && Time.realtimeSinceStartup - started < PrepareTimeoutSeconds)
                {
                    yield return null;
                    if (version != _loadVersion) yield break;
                }
                if (version != _loadVersion) yield break;

                if (_player.isPrepared)
                {
                    _ready = true;
                    _ended = false;
                    _message.gameObject.SetActive(false);
                    ApplyVolume();
                    ApplySpeed();
                    double length = _player.length;
                    if (length > 0.0) _durationText.text = FormatTime(length);
                    _player.Play();
                    UpdatePlayIcon();
                    yield break;
                }
                _player.Stop();
            }
            ShowMessage("Не удалось воспроизвести видео");
        }

        // Локальный файл пробуем в двух видах (file:// и обычный путь), адрес из сети один.
        private static List<string> Candidates(string uri)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(uri)) return result;
            string value = uri.Trim();
            string local = MediaCodec.ToLocalPath(value);
            if (local == null)
            {
                result.Add(value);
            }
            else
            {
                result.Add("file://" + local);
                result.Add(local);
            }
            return result;
        }

        private void StopPlayback()
        {
            _ready = false;
            _ended = false;
            _advance = false;
            _failed = false;
            if (_player != null && (_player.isPlaying || _player.isPrepared)) _player.Stop();
            if (_picture != null)
            {
                _picture.texture = null;
                _picture.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        private void TogglePlay()
        {
            if (!_ready) return;
            if (_player.isPlaying) Pause();
            else Play();
        }

        private void CycleSpeed()
        {
            int next = 0;
            for (int i = 0; i < Speeds.Length; i++)
            {
                if (Mathf.Abs(Speeds[i] - _speed) < 0.01f)
                {
                    next = (i + 1) % Speeds.Length;
                    break;
                }
            }
            SetSpeed(Speeds[next]);
        }

        private void ApplySpeed()
        {
            if (_speedText != null) _speedText.text = FormatSpeed(_speed);
            if (_ready && _player.canSetPlaybackSpeed) _player.playbackSpeed = _speed;
        }

        private void ApplyVolume()
        {
            if (!_ready || _player.audioOutputMode != VideoAudioOutputMode.Direct || _player.audioTrackCount == 0) return;
            _player.SetDirectAudioVolume(0, _volumeValue);
        }

        private void OnVolumeChanged(float value)
        {
            _volumeValue = value;
            ApplyVolume();
        }

        private void OnSeekChanged(float value)
        {
            if (_ready && _player.length > 0.0) _timeText.text = FormatTime(value * _player.length);
        }

        private void OnSeekEnded(float value)
        {
            if (!_ready || _player.length <= 0.0) return;
            _player.time = Math.Min(value * _player.length, Math.Max(0.0, _player.length - 0.3));
            _ended = false;
        }

        private void OnPlayerError(VideoPlayer source, string message)
        {
            Debug.LogWarning("[Media] Ошибка видео: " + message);
            _failed = true;
        }

        private void OnLoopPoint(VideoPlayer source)
        {
            _ended = true;
            _advance = true;
        }

        private void ShowMessage(string text)
        {
            _message.text = text;
            _message.gameObject.SetActive(true);
        }

        private void UpdatePlayIcon()
        {
            if (_playIcon == null) return;
            bool playing = IsPlaying;
            _wasPlaying = playing;
            _playIcon.sprite = VideoArt.Icon(playing ? "pause" : "play");
        }

        private static string FormatSpeed(float speed)
        {
            return speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0.0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0.0;
            int total = (int)seconds;
            int h = total / 3600, m = (total % 3600) / 60, s = total % 60;
            return h > 0
                ? h.ToString(CultureInfo.InvariantCulture) + ":" + m.ToString("00", CultureInfo.InvariantCulture) + ":" + s.ToString("00", CultureInfo.InvariantCulture)
                : m.ToString(CultureInfo.InvariantCulture) + ":" + s.ToString("00", CultureInfo.InvariantCulture);
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

            if (Screen.width != _lastScreen.x || Screen.height != _lastScreen.y || Screen.safeArea != _lastSafeArea)
                ApplyLayout();

            if (_ready)
            {
                Texture texture = _player.texture;
                if (texture != null && texture.width > 8)
                {
                    if (_picture.texture != texture) _picture.texture = texture;
                    _picture.color = Color.white;
                    FitPicture(texture.width, texture.height);
                }

                if (!_seek.Dragging)
                {
                    double length = _player.length;
                    double time = _player.time;
                    if (length > 0.0) _seek.Value = (float)(time / length);
                    _timeText.text = FormatTime(time);
                }
                if (_player.isPlaying != _wasPlaying) UpdatePlayIcon();
            }

            if (_advance)
            {
                _advance = false;
                if (_index + 1 < _items.Count) GoTo(_index + 1);
                else UpdatePlayIcon();
            }
        }

        // Видео вписывается в область целиком, с сохранением пропорций.
        private void FitPicture(int width, int height)
        {
            Rect rect = _videoArea.rect;
            if (rect.width < 1f || rect.height < 1f || width < 1 || height < 1) return;
            float k = Mathf.Min(rect.width / width, rect.height / height);
            _picture.rectTransform.sizeDelta = new Vector2(width * k, height * k);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && IsOpen && _ready && _player.isPlaying) Pause();
        }
    }
}
