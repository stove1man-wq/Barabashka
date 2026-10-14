using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class AgentUIManager : MonoBehaviour
{
    private AgentManager _agentManager;
    private InputField _inputField;
    private Text _statusText;
    private RectTransform _messageContent;
    private ScrollRect _scrollRect;
    private readonly List<string> _messageHistory = new List<string>();

    private Font _font;
    private const float ReferenceWidth = 1080f;
    private const float ReferenceHeight = 1920f;

    private void Start()
    {
        _agentManager = GetComponent<AgentManager>();
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildInterface();
        AddMessage("Barabashka", "Привет! Я готов помочь. Напиши задачу в поле ниже.", false);
        SetStatus("Готов к работе");
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

        Text title = CreateText("AppTitle", canvasObject.transform, "BARABASHKA", 30,
            new Color(0.78f, 0.88f, 1f), TextAnchor.MiddleLeft);
        Place(title.rectTransform, new Vector2(0.06f, 0.93f), new Vector2(0.72f, 0.985f),
            Vector2.zero, Vector2.zero);

        Text subtitle = CreateText("AppSubtitle", canvasObject.transform, "PERSONAL AI AGENT", 15,
            new Color(0.38f, 0.57f, 0.76f), TextAnchor.MiddleLeft);
        Place(subtitle.rectTransform, new Vector2(0.06f, 0.905f), new Vector2(0.72f, 0.935f),
            Vector2.zero, Vector2.zero);

        Image avatarHalo = CreateImage("AvatarHalo", canvasObject.transform,
            new Color(0.12f, 0.55f, 0.95f, 0.10f));
        avatarHalo.rectTransform.anchorMin = new Vector2(0.5f, 0.82f);
        avatarHalo.rectTransform.anchorMax = new Vector2(0.5f, 0.82f);
        avatarHalo.rectTransform.sizeDelta = new Vector2(300f, 300f);
        avatarHalo.rectTransform.anchoredPosition = Vector2.zero;

        Image avatarRing = CreateImage("AvatarRing", canvasObject.transform,
            new Color(0.15f, 0.65f, 1f, 0.32f));
        avatarRing.rectTransform.anchorMin = new Vector2(0.5f, 0.82f);
        avatarRing.rectTransform.anchorMax = new Vector2(0.5f, 0.82f);
        avatarRing.rectTransform.sizeDelta = new Vector2(212f, 212f);
        avatarRing.rectTransform.anchoredPosition = Vector2.zero;

        Text avatarMark = CreateText("AvatarMark", canvasObject.transform, "B", 86,
            new Color(0.83f, 0.94f, 1f), TextAnchor.MiddleCenter);
        avatarMark.rectTransform.anchorMin = new Vector2(0.5f, 0.82f);
        avatarMark.rectTransform.anchorMax = new Vector2(0.5f, 0.82f);
        avatarMark.rectTransform.sizeDelta = new Vector2(180f, 180f);
        avatarMark.rectTransform.anchoredPosition = Vector2.zero;

        _statusText = CreateText("AgentStatus", canvasObject.transform, "Готов к работе", 15,
            new Color(0.42f, 0.75f, 0.95f), TextAnchor.MiddleCenter);
        Place(_statusText.rectTransform, new Vector2(0.12f, 0.715f), new Vector2(0.86f, 0.745f),
            Vector2.zero, Vector2.zero);

        Text chatLabel = CreateText("ConversationLabel", canvasObject.transform, "ДИАЛОГ", 15,
            new Color(0.43f, 0.66f, 0.84f), TextAnchor.MiddleLeft);
        Place(chatLabel.rectTransform, new Vector2(0.06f, 0.675f), new Vector2(0.83f, 0.705f),
            Vector2.zero, Vector2.zero);

        Image chatPanel = CreateImage("ConversationPanel", canvasObject.transform,
            new Color(0.055f, 0.075f, 0.115f, 0.96f));
        Place(chatPanel.rectTransform, new Vector2(0.045f, 0.19f), new Vector2(0.89f, 0.67f),
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

        Image inputPanel = CreateImage("InputPanel", canvasObject.transform,
            new Color(0.075f, 0.105f, 0.155f, 1f));
        Place(inputPanel.rectTransform, new Vector2(0.045f, 0.055f), new Vector2(0.81f, 0.145f),
            Vector2.zero, Vector2.zero);

        GameObject inputObject = new GameObject("MessageInput");
        inputObject.transform.SetParent(inputPanel.transform, false);
        Image inputBackground = inputObject.AddComponent<Image>();
        inputBackground.color = new Color(0.10f, 0.14f, 0.20f, 1f);
        _inputField = inputObject.AddComponent<InputField>();
        _inputField.targetGraphic = inputBackground;
        _inputField.lineType = InputField.LineType.MultiLineNewline;
        _inputField.characterLimit = 4000;
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        Stretch(inputRect, new Vector2(0.035f, 0.12f), new Vector2(0.83f, 0.88f),
            Vector2.zero, Vector2.zero);

        Text inputText = CreateText("InputText", inputObject.transform, "", 22,
            Color.white, TextAnchor.MiddleLeft);
        Place(inputText.rectTransform, new Vector2(0.025f, 0.05f), new Vector2(0.975f, 0.95f),
            Vector2.zero, Vector2.zero);
        Text placeholder = CreateText("Placeholder", inputObject.transform, "Напишите сообщение…", 22,
            new Color(0.48f, 0.57f, 0.68f), TextAnchor.MiddleLeft);
        Place(placeholder.rectTransform, new Vector2(0.025f, 0.05f), new Vector2(0.975f, 0.95f),
            Vector2.zero, Vector2.zero);
        _inputField.textComponent = inputText;
        _inputField.placeholder = placeholder;
        _inputField.onEndEdit.AddListener(OnInputEndEdit);

        Button sendButton = CreateButton("SendButton", inputPanel.transform, "↑",
            new Color(0.12f, 0.48f, 0.82f, 1f), 34);
        RectTransform sendRect = sendButton.GetComponent<RectTransform>();
        sendRect.anchorMin = new Vector2(0.85f, 0.12f);
        sendRect.anchorMax = new Vector2(0.97f, 0.88f);
        sendRect.offsetMin = Vector2.zero;
        sendRect.offsetMax = Vector2.zero;
        sendButton.onClick.AddListener(SubmitText);

        Button stopButton = CreateButton("StopStrip", canvasObject.transform, "■\n\nС\nТ\nО\nП",
            new Color(0.42f, 0.12f, 0.17f, 0.94f), 20);
        RectTransform stopRect = stopButton.GetComponent<RectTransform>();
        stopRect.anchorMin = new Vector2(0.92f, 0.02f);
        stopRect.anchorMax = new Vector2(0.985f, 0.985f);
        stopRect.offsetMin = Vector2.zero;
        stopRect.offsetMax = Vector2.zero;
        stopButton.onClick.AddListener(() =>
        {
            if (_agentManager != null) _agentManager.StopCurrentTask();
        });

        Text footer = CreateText("Footer", canvasObject.transform, "Локальный интерфейс агента", 13,
            new Color(0.30f, 0.40f, 0.53f), TextAnchor.MiddleLeft);
        Place(footer.rectTransform, new Vector2(0.06f, 0.015f), new Vector2(0.82f, 0.045f),
            Vector2.zero, Vector2.zero);
    }

    public void AddUserMessage(string text) { AddMessage("Вы", text, true); }
    public void AddAgentMessage(string text) { AddMessage("BARABASHKA", text, false); }
    public void UpdateResponseText(string text) { AddAgentMessage(text); }

    public void SetStatus(string status)
    {
        if (_statusText != null) _statusText.text = status;
        Debug.Log("[Agent UI] " + status);
    }

    private void AddMessage(string speaker, string message, bool isUser)
    {
        if (_messageContent == null) return;
        string safeMessage = string.IsNullOrWhiteSpace(message) ? "…" : message.Trim();
        _messageHistory.Add(speaker + ": " + safeMessage);

        GameObject card = new GameObject(isUser ? "UserMessage" : "AgentMessage");
        card.transform.SetParent(_messageContent, false);
        Image cardImage = card.AddComponent<Image>();
        cardImage.color = isUser
            ? new Color(0.10f, 0.23f, 0.36f, 1f)
            : new Color(0.085f, 0.115f, 0.17f, 1f);
        VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(14, 14, 10, 10);
        cardLayout.spacing = 6f;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandHeight = false;
        cardLayout.childForceExpandWidth = true;

        Text speakerText = CreateText("Speaker", card.transform, speaker, 14,
            isUser ? new Color(0.55f, 0.78f, 1f) : new Color(0.35f, 0.78f, 0.95f),
            TextAnchor.MiddleLeft);
        LayoutElement speakerLayout = speakerText.gameObject.AddComponent<LayoutElement>();
        speakerLayout.preferredHeight = 24f;

        Text messageText = CreateText("MessageText", card.transform, safeMessage, 21,
            new Color(0.89f, 0.93f, 0.98f), TextAnchor.UpperLeft);
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        messageText.verticalOverflow = VerticalWrapMode.Overflow;
        messageText.supportRichText = false;
        LayoutElement messageLayout = messageText.gameObject.AddComponent<LayoutElement>();
        messageLayout.minHeight = 30f;
        messageLayout.preferredHeight = -1f;

        Button copyButton = CreateButton("CopyMessage", card.transform, "Копировать",
            new Color(0.13f, 0.19f, 0.27f, 1f), 14);
        LayoutElement copyLayout = copyButton.gameObject.AddComponent<LayoutElement>();
        copyLayout.preferredHeight = 34f;
        copyLayout.preferredWidth = 150f;
        copyButton.onClick.AddListener(() =>
        {
            GUIUtility.systemCopyBuffer = safeMessage;
            SetStatus("Сообщение скопировано");
        });

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_messageContent);
        _scrollRect.verticalNormalizedPosition = 0f;
    }

    private void OnInputEndEdit(string value)
    {
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            SubmitText();
    }

    private void SubmitText()
    {
        if (_inputField == null || string.IsNullOrWhiteSpace(_inputField.text)) return;
        string message = _inputField.text.Trim();
        _inputField.SetTextWithoutNotify(string.Empty);
        if (_agentManager != null) _agentManager.SendUserMessage(message);
        _inputField.ActivateInputField();
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
        Button button = image.gameObject.AddComponent<Button>();
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