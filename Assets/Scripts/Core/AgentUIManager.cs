using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public partial class AgentUIManager : MonoBehaviour
{
    private AgentManager _agentManager;
    private InputField _inputField;
    private Text _statusText;
    private RectTransform _messageContent;
    private ScrollRect _scrollRect;
    private readonly List<string> _messageHistory = new List<string>();

    // Режимы интерфейса: «голосовой» (аватар на пол-экрана) и «полный» (аватар сверху, чат и ввод).
    private struct ModeRect
    {
        public RectTransform Rect;
        public Vector2 MinVoice, MaxVoice, MinFull, MaxFull;
    }
    private readonly List<ModeRect> _modeRects = new List<ModeRect>();
    private bool _fullMode;
    private float _modeT;
    private CanvasGroup _chatGroup, _inputGroup, _voiceGroup;
    private Text _subtitleText;
    private GameObject _modeIconMenu, _modeIconClose;
    private string _subtitleUser = string.Empty;
    private string _subtitleAgent = string.Empty;

    private Font _font;
    private const float ReferenceWidth = 1080f;
    private const float ReferenceHeight = 1920f;

    private void OnEnable()
    {
        Barabashka.Contracts.AgentActivityBus.Changed += OnActivityChanged;
        ChatKinds.Changed += OnKindChanged;
    }

    private void OnDisable()
    {
        Barabashka.Contracts.AgentActivityBus.Changed -= OnActivityChanged;
        ChatKinds.Changed -= OnKindChanged;
    }

    // Состояние агента показываем только в строке статуса: в чат оно не попадает.
    private void OnActivityChanged(Barabashka.Contracts.AgentActivity activity)
    {
        switch (activity)
        {
            case Barabashka.Contracts.AgentActivity.Listening: SetStatus("Слушаю…"); break;
            case Barabashka.Contracts.AgentActivity.Thinking: SetStatus("Думаю…"); break;
            case Barabashka.Contracts.AgentActivity.Speaking: SetStatus("Говорю…"); break;
            case Barabashka.Contracts.AgentActivity.Error:
                SetStatus("Голос недоступен, текстовый ввод работает");
                SetMode(true); // без голоса аватарный режим бесполезен: сразу открываем текстовый интерфейс
                break;
            default:
                SetStatus(Barabashka.Contracts.AgentVoiceControl.Muted ? "Микрофон выключен" : "Готов к работе");
                break;
        }
    }

    private void Start()
    {
        _agentManager = GetComponent<AgentManager>();
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildInterface();
        ApplyLayout();
        RestoreChat();
        // Приветствие и «Готов к работе» появятся после загрузки голосовой модели (ShowReady), пока - только строка о загрузке.
        SetStatus("Подготовка голосового ввода…");
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Barabashka_Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        Image background = CreateImage("Background", canvasObject.transform,
            new Color(0.025f, 0.035f, 0.065f, 1f));
        Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Text title = CreateText("AppTitle", canvasObject.transform, "BARABASHKA", 34,
            new Color(0.78f, 0.88f, 1f), TextAnchor.MiddleLeft);
        Place(title.rectTransform, new Vector2(0.06f, 0.93f), new Vector2(0.66f, 0.985f),
            Vector2.zero, Vector2.zero);

        Text subtitle = CreateText("AppSubtitle", canvasObject.transform, "PERSONAL AI AGENT", 15,
            new Color(0.38f, 0.57f, 0.76f), TextAnchor.MiddleLeft);
        Place(subtitle.rectTransform, new Vector2(0.06f, 0.905f), new Vector2(0.66f, 0.935f),
            Vector2.zero, Vector2.zero);

        // Аватар: 3D-персонаж снимается отдельной камерой в RenderTexture, здесь он показывается как картинка.
        GameObject avatarFrame = new GameObject("AvatarFrame");
        avatarFrame.transform.SetParent(canvasObject.transform, false);
        RectTransform avatarFrameRect = avatarFrame.AddComponent<RectTransform>();
        GameObject avatarImageObject = new GameObject("AvatarImage");
        avatarImageObject.transform.SetParent(avatarFrame.transform, false);
        RawImage avatarImage = avatarImageObject.AddComponent<RawImage>();
        avatarImage.raycastTarget = true;
        AvatarVisualizer avatar = GetComponent<AvatarVisualizer>();
        if (avatar != null && avatar.AvatarTexture != null) avatarImage.texture = avatar.AvatarTexture;
        else avatarImage.color = new Color(0f, 0f, 0f, 0f);
        Stretch(avatarImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AspectRatioFitter avatarFit = avatarImageObject.AddComponent<AspectRatioFitter>();
        avatarFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        avatarFit.aspectRatio = 1f;
        // Касание аватара: прерывает озвучку или текущий запрос, иначе аватар приветственно машет рукой.
        Button avatarButton = avatarImageObject.AddComponent<Button>();
        avatarButton.transition = Selectable.Transition.None;
        avatarButton.targetGraphic = avatarImage;
        avatarButton.onClick.AddListener(OnAvatarTapped);
        Track(avatarFrameRect,
            new Vector2(0.04f, 0.41f), new Vector2(0.96f, 0.92f),
            new Vector2(0.67f, 0.84f), new Vector2(0.985f, 0.995f));

        _statusText = CreateText("AgentStatus", canvasObject.transform, "Готов к работе", 22,
            new Color(0.42f, 0.75f, 0.95f), TextAnchor.MiddleCenter);
        Track(_statusText.rectTransform,
            new Vector2(0.06f, 0.355f), new Vector2(0.94f, 0.405f),
            new Vector2(0.06f, 0.865f), new Vector2(0.66f, 0.905f));

        _chatGroup = CreateGroup("ChatGroup", canvasObject.transform);
        _inputGroup = CreateGroup("InputGroup", canvasObject.transform);
        _voiceGroup = CreateGroup("VoiceGroup", canvasObject.transform);

        BuildChatTabs(_chatGroup.transform);

        Image chatPanel = CreateImage("ConversationPanel", _chatGroup.transform,
            new Color(0.055f, 0.075f, 0.115f, 0.96f));
        SetRoundedSprite(chatPanel);
        Place(chatPanel.rectTransform, new Vector2(0.045f, 0.19f), new Vector2(0.955f, 0.78f),
            Vector2.zero, Vector2.zero);

        GameObject scrollObject = new GameObject("ConversationScroll");
        scrollObject.transform.SetParent(chatPanel.transform, false);
        _scrollRect = scrollObject.AddComponent<ScrollRect>();
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        Stretch(scrollRectTransform, new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.975f),
            Vector2.zero, Vector2.zero);
        _scrollRect.horizontal = false;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        Stretch(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;
        viewport.AddComponent<RectMask2D>();
        _scrollRect.viewport = viewportRect;

        GameObject content = new GameObject("MessageContent");
        content.transform.SetParent(viewport.transform, false);
        _messageContent = content.AddComponent<RectTransform>();
        _messageContent.anchorMin = new Vector2(0f, 1f);
        _messageContent.anchorMax = new Vector2(1f, 1f);
        _messageContent.pivot = new Vector2(0.5f, 1f);
        _messageContent.anchoredPosition = Vector2.zero;
        _messageContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _scrollRect.content = _messageContent;
        BuildUndoToast(_chatGroup.transform);

        Image inputPanel = CreateImage("InputPanel", _inputGroup.transform,
            new Color(0.075f, 0.105f, 0.155f, 1f));
        SetRoundedSprite(inputPanel);
        Place(inputPanel.rectTransform, new Vector2(0.045f, 0.045f), new Vector2(0.955f, 0.16f),
            Vector2.zero, Vector2.zero);

        GameObject inputObject = new GameObject("MessageInput");
        inputObject.transform.SetParent(inputPanel.transform, false);
        Image inputBackground = inputObject.AddComponent<Image>();
        inputBackground.color = new Color(0.10f, 0.14f, 0.20f, 1f);
        SetRoundedSprite(inputBackground);
        _inputField = inputObject.AddComponent<InputField>();
        _inputField.targetGraphic = inputBackground;
        // На телефоне однострочное поле: на экранной клавиатуре появляется кнопка «Готово/Отправить».
        _inputField.lineType = Application.isMobilePlatform
            ? InputField.LineType.SingleLine
            : InputField.LineType.MultiLineNewline;
        _inputField.characterLimit = 4000;
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        Stretch(inputRect, new Vector2(0.035f, 0.12f), new Vector2(0.77f, 0.88f),
            Vector2.zero, Vector2.zero);

        Text inputText = CreateText("InputText", inputObject.transform, "", 24,
            Color.white, TextAnchor.MiddleLeft);
        Place(inputText.rectTransform, new Vector2(0.025f, 0.05f), new Vector2(0.975f, 0.95f),
            Vector2.zero, Vector2.zero);
        Text placeholder = CreateText("Placeholder", inputObject.transform, "Напишите сообщение…", 24,
            new Color(0.48f, 0.57f, 0.68f), TextAnchor.MiddleLeft);
        Place(placeholder.rectTransform, new Vector2(0.025f, 0.05f), new Vector2(0.975f, 0.95f),
            Vector2.zero, Vector2.zero);
        _inputField.textComponent = inputText;
        _inputField.placeholder = placeholder;
        _inputField.onEndEdit.AddListener(OnInputEndEdit);
        // Диагностика ввода с экранной клавиатуры (видно в Logcat по метке [Input]).
        _inputField.onValueChanged.AddListener(value => Debug.Log("[Input] длина текста: " + (value == null ? 0 : value.Length)));

        Button attachmentButton = CreateButton("AttachmentButton", inputPanel.transform, "＋",
            new Color(0.12f, 0.18f, 0.27f, 1f), 30);
        RectTransform attachmentRect = attachmentButton.GetComponent<RectTransform>();
        attachmentRect.anchorMin = new Vector2(0.78f, 0.22f);
        attachmentRect.anchorMax = new Vector2(0.86f, 0.78f);
        attachmentRect.offsetMin = Vector2.zero;
        attachmentRect.offsetMax = Vector2.zero;
        attachmentButton.onClick.AddListener(() => SetStatus("Вложения будут добавлены отдельным этапом"));

        Button sendButton = CreateButton("SendButton", inputPanel.transform, "↑",
            new Color(0.12f, 0.48f, 0.82f, 1f), 34);
        RectTransform sendRect = sendButton.GetComponent<RectTransform>();
        sendRect.anchorMin = new Vector2(0.87f, 0.12f);
        sendRect.anchorMax = new Vector2(0.97f, 0.88f);
        sendRect.offsetMin = Vector2.zero;
        sendRect.offsetMax = Vector2.zero;
        sendButton.onClick.AddListener(SubmitText);

        // Голосовой режим: субтитры, кнопка микрофона и переключатель режима.
        _subtitleText = CreateText("Subtitles", _voiceGroup.transform, string.Empty, 28,
            new Color(0.89f, 0.93f, 0.98f), TextAnchor.UpperCenter);
        _subtitleText.supportRichText = true;
        _subtitleText.resizeTextForBestFit = true;
        _subtitleText.resizeTextMinSize = 16;
        _subtitleText.resizeTextMaxSize = 30;
        _subtitleText.verticalOverflow = VerticalWrapMode.Truncate;
        Place(_subtitleText.rectTransform, new Vector2(0.07f, 0.13f), new Vector2(0.93f, 0.345f),
            Vector2.zero, Vector2.zero);

        // Переключатель режима: маленькая круглая кнопка со значком сбоку, без подписи.
        Button modeButton = CreateButton("ModeButton", canvasObject.transform, string.Empty,
            Color.white, 20);
        Image modeRing = modeButton.targetGraphic as Image;
        SetBuiltInRoundSprite(modeRing);
        modeRing.color = new Color(0.40f, 0.72f, 1f, 1f); // светлая обводка
        ColorBlock modeColors = modeButton.colors;
        modeColors.normalColor = Color.white;
        modeColors.highlightedColor = Color.white;
        modeColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        modeButton.colors = modeColors;
        Image modeFill = CreateImage("Fill", modeButton.transform, new Color(0.10f, 0.20f, 0.34f, 1f));
        SetBuiltInRoundSprite(modeFill);
        modeFill.raycastTarget = false;
        Stretch(modeFill.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        _modeIconMenu = new GameObject("IconMenu");
        _modeIconMenu.transform.SetParent(modeButton.transform, false);
        AddIconBar(_modeIconMenu.transform, 0.5f, 0.66f, 0.46f, 0.07f, 0f);
        AddIconBar(_modeIconMenu.transform, 0.5f, 0.50f, 0.46f, 0.07f, 0f);
        AddIconBar(_modeIconMenu.transform, 0.5f, 0.34f, 0.46f, 0.07f, 0f);
        _modeIconClose = new GameObject("IconCollapse");
        _modeIconClose.transform.SetParent(modeButton.transform, false);
        AddIconBar(_modeIconClose.transform, 0.39f, 0.50f, 0.34f, 0.08f, -42f);
        AddIconBar(_modeIconClose.transform, 0.61f, 0.50f, 0.34f, 0.08f, 42f);
        Track(modeButton.GetComponent<RectTransform>(),
            new Vector2(0.82f, 0.055f), new Vector2(0.94f, 0.055f + 0.12f * ReferenceWidth / ReferenceHeight),
            new Vector2(0.86f, 0.793f), new Vector2(0.86f + 0.09f, 0.793f + 0.09f * ReferenceWidth / ReferenceHeight));
        modeButton.onClick.AddListener(() => SetMode(!_fullMode));

        Text footer = CreateText("Footer", canvasObject.transform, "Локальный интерфейс агента", 13,
            new Color(0.30f, 0.40f, 0.53f), TextAnchor.MiddleLeft);
        Place(footer.rectTransform, new Vector2(0.06f, 0.015f), new Vector2(0.82f, 0.045f),
            Vector2.zero, Vector2.zero);

        BuildSettingsUi(canvasObject.transform);
    }

    public void AddUserMessage(string text)
    {
        _turn.Clear();
        ChatKinds.Begin(ChatKinds.ForText(text));
        AddMessage("Вы", text, true);
    }
    public void AddAgentMessage(string text) { AddMessage("BARABASHKA", text, false); }
    public void UpdateResponseText(string text) { AddAgentMessage(text); }

    public void SetStatus(string status)
    {
        if (_statusText != null) _statusText.text = status;
        Debug.Log("[Agent UI] " + status);
    }

    /// <summary>Служебное сообщение в историю чата: в субтитры голосового режима оно не попадает (там уже есть статус).</summary>
    public void AddSystemMessage(string text) { AddMessage("BARABASHKA", text, false, false, true, ChatKind.System); }

    private void AddMessage(string speaker, string message, bool isUser, bool updateSubtitles = true,
        bool transient = false, ChatKind? kind = null)
    {
        if (_messageContent == null) return;
        string safeMessage = string.IsNullOrWhiteSpace(message) ? "…" : message.Trim();
        _messageHistory.Add(speaker + ": " + safeMessage);

        if (updateSubtitles)
        {
            if (isUser)
            {
                _subtitleUser = safeMessage;
                _subtitleAgent = string.Empty;
            }
            else
            {
                _subtitleAgent = safeMessage;
            }
            RefreshSubtitles();
        }

        // Вложение (выбранная картинка) прикрепляется к ближайшему ответу агента.
        string attachment = null;
        if (!isUser)
        {
            Barabashka.Contracts.MediaItem pending = Barabashka.Contracts.MediaServices.TakePendingAttachment();
            if (pending != null) attachment = Barabashka.Contracts.MediaCodec.Encode(pending);
        }

        var rec = new ChatRecord
        {
            Id = _nextId++,
            Kind = kind.HasValue ? kind.Value : (attachment != null ? ChatKind.Media : ChatKinds.Current),
            IsUser = isUser,
            Speaker = speaker,
            Text = safeMessage,
            Ticks = DateTime.UtcNow.Ticks,
            Transient = transient,
            Media = attachment
        };
        _records.Add(rec);
        if (!kind.HasValue) _turn.Add(rec);
        BuildCard(rec);
        if (!IsVisible(rec)) { _unread[(int)rec.Kind] = true; RefreshTabs(); }
        if (!transient) Persist();

        Debug.Log("[Chat] " + speaker + ": " + safeMessage.Length + " симв., строк: " + (safeMessage.Split('\n').Length));
        RebuildChatLayout();
        // На телефоне ширина области чата может быть известна не сразу: повторяем пересчёт на следующем кадре.
        StartCoroutine(RebuildChatNextFrame());
    }

    private void RebuildChatLayout()
    {
        if (_messageContent == null || _scrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_messageContent);
        _scrollRect.verticalNormalizedPosition = 0f;
    }

    private System.Collections.IEnumerator RebuildChatNextFrame()
    {
        yield return null;
        RebuildChatLayout();
    }

    private void OnInputEndEdit(string value)
    {
        bool enterPressed = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;

        // На телефоне «Готово» на экранной клавиатуре завершает ввод. Кнопка «назад» отменяет ввод
        // и возвращает пустой текст, поэтому пустое сообщение не отправится.
        if (enterPressed || Application.isMobilePlatform)
            SubmitText();
    }

    private void SubmitText()
    {
        if (_inputField == null || string.IsNullOrWhiteSpace(_inputField.text)) return;
        string message = _inputField.text.Trim();
        _inputField.SetTextWithoutNotify(string.Empty);
        if (_agentManager != null) _agentManager.SendUserMessage(message);

        // На телефоне клавиатуру заново не открываем: она закрыта пользователем.
        if (!Application.isMobilePlatform) _inputField.ActivateInputField();
    }

    // ---- Режимы интерфейса ----

    private void Track(RectTransform rect, Vector2 minVoice, Vector2 maxVoice, Vector2 minFull, Vector2 maxFull)
    {
        ModeRect entry = new ModeRect();
        entry.Rect = rect;
        entry.MinVoice = minVoice;
        entry.MaxVoice = maxVoice;
        entry.MinFull = minFull;
        entry.MaxFull = maxFull;
        _modeRects.Add(entry);
        rect.anchorMin = minVoice;
        rect.anchorMax = maxVoice;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Полоска значка (центр и размер заданы долями кнопки, угол - поворот).
    private void AddIconBar(Transform parent, float cx, float cy, float width, float height, float angle)
    {
        Image bar = CreateImage("Bar", parent, new Color(0.92f, 0.96f, 1f, 1f));
        bar.raycastTarget = false;
        Place(bar.rectTransform, new Vector2(cx - width * 0.5f, cy - height * 0.5f),
            new Vector2(cx + width * 0.5f, cy + height * 0.5f), Vector2.zero, Vector2.zero);
        bar.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    private CanvasGroup CreateGroup(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return obj.AddComponent<CanvasGroup>();
    }

    public void SetMode(bool fullInterface)
    {
        if (_fullMode == fullInterface) return;
        _fullMode = fullInterface;
        if (!fullInterface && _inputField != null) _inputField.DeactivateInputField();
        Debug.Log("[UI] Режим интерфейса: " + (fullInterface ? "полный" : "голосовой (аватар)"));
    }

    private void Update()
    {
        if (HandleSettingsBack()) return;

        // Кнопка «Назад» на Android приходит как Escape.
        if (_fullMode && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetMode(false);

        float target = _fullMode ? 1f : 0f;
        if (!Mathf.Approximately(_modeT, target))
        {
            _modeT = Mathf.MoveTowards(_modeT, target, Time.unscaledDeltaTime / 0.35f);
            ApplyLayout();
        }
    }

    private void ApplyLayout()
    {
        float s = Mathf.SmoothStep(0f, 1f, _modeT);
        for (int i = 0; i < _modeRects.Count; i++)
        {
            ModeRect entry = _modeRects[i];
            if (entry.Rect == null) continue;
            entry.Rect.anchorMin = Vector2.Lerp(entry.MinVoice, entry.MinFull, s);
            entry.Rect.anchorMax = Vector2.Lerp(entry.MaxVoice, entry.MaxFull, s);
            entry.Rect.offsetMin = Vector2.zero;
            entry.Rect.offsetMax = Vector2.zero;
        }

        SetGroup(_chatGroup, Mathf.Clamp01((s - 0.5f) * 2f));
        SetGroup(_inputGroup, Mathf.Clamp01((s - 0.5f) * 2f));
        SetGroup(_voiceGroup, Mathf.Clamp01(1f - s * 2f));

        bool full = s > 0.5f;
        if (_statusText != null)
        {
            _statusText.alignment = full ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            _statusText.fontSize = full ? 17 : 22;
        }
        if (_modeIconMenu != null) _modeIconMenu.SetActive(!full);
        if (_modeIconClose != null) _modeIconClose.SetActive(full);
    }

    private static void SetGroup(CanvasGroup group, float alpha)
    {
        if (group == null) return;
        group.alpha = alpha;
        bool active = alpha > 0.95f;
        group.interactable = active;
        group.blocksRaycasts = active;
    }

    private void OnAvatarTapped()
    {
        Barabashka.Contracts.AgentActivity activity = Barabashka.Contracts.AgentActivityBus.Current;
        if (activity == Barabashka.Contracts.AgentActivity.Speaking)
        {
            Barabashka.Contracts.AgentVoiceControl.RequestStopSpeaking();
            return;
        }
        if (activity == Barabashka.Contracts.AgentActivity.Thinking)
        {
            if (_agentManager != null) _agentManager.StopCurrentTask();
            return;
        }
        AvatarVisualizer avatar = GetComponent<AvatarVisualizer>();
        if (avatar != null) avatar.TriggerGreet();
    }

    private static string ShortText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        string clean = text.Replace("<", "\u2039").Trim();
        return clean.Length <= maxLength ? clean : clean.Substring(0, maxLength).TrimEnd() + "…";
    }

    // Субтитры голосового режима: что сказали вы (приглушённо) и что ответил агент.
    private void RefreshSubtitles()
    {
        if (_subtitleText == null) return;
        string user = ShortText(_subtitleUser, 140);
        string agent = ShortText(_subtitleAgent, 360);
        string result = string.Empty;
        if (user.Length > 0) result = "<color=#6F93B5>" + user + "</color>";
        if (agent.Length > 0) result += (result.Length > 0 ? "\n\n" : string.Empty) + agent;
        _subtitleText.text = result;
    }

    private static Sprite _runtimeCircleSprite;
    private static Sprite _runtimeRoundedSprite;

    private void SetBuiltInRoundSprite(Image image)
    {
        // Do not depend on Unity's internal editor skin assets: they are not
        // guaranteed to exist in player builds or across Unity versions.
        if (_runtimeCircleSprite == null)
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Barabashka_RuntimeCircle";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius + 0.75f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _runtimeCircleSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            _runtimeCircleSprite.name = "Barabashka_RuntimeCircleSprite";
        }

        image.sprite = _runtimeCircleSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }

    private void SetRoundedSprite(Image image)
    {
        if (_runtimeRoundedSprite == null)
        {
            const int size = 128;
            const float radius = 22f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Barabashka_RoundedPanel";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x, radius, size - radius - 1f);
                    float cy = Mathf.Clamp(y, radius, size - radius - 1f);
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01(radius + 0.75f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _runtimeRoundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect,
                new Vector4(28f, 28f, 28f, 28f));
            _runtimeRoundedSprite.name = "Barabashka_RoundedPanelSprite";
        }

        image.sprite = _runtimeRoundedSprite;
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label, Color color, int fontSize)
    {
        Image image = CreateImage(name, parent, color);
        SetRoundedSprite(image);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        button.colors = colors;
        Text text = CreateText("Label", image.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        return button;
    }

    private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        Place(rect, min, max, offsetMin, offsetMax);
    }
}