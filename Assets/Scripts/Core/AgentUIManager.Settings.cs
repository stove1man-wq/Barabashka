using System.Collections.Generic;
using System.Threading;
using Barabashka.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Стартовая кнопка «Настрой меня» и окно настроек. Окно открывается кнопкой (пока настройки ни разу не подтверждали),
/// а позже - командой «открой настройки» голосом или в чате.
/// </summary>
public partial class AgentUIManager
{
    private static readonly Color ToggleOn = new Color(0.12f, 0.48f, 0.82f, 1f);
    private static readonly Color ToggleOff = new Color(0.20f, 0.25f, 0.32f, 1f);

    private GameObject _startSettingsButton;
    private GameObject _settingsPanel;
    private Text _settingsInfo;
    private RectTransform _settingsContent;
    private ScrollRect _settingsScroll;
    private bool _settingsOpen;

    private bool _setSpeak, _setLlm, _setDevice;
    private Button _speakButton, _llmButton, _deviceButton, _actionsButton, _keepButton, _trashButton;
    private ChatKeep _setKeep;
    private bool _setTrash;
    private ActionLevel _setActions;
    private InputField _ollamaInput, _cityInput, _openRouterInput, _groqInput;

    private void BuildSettingsUi(Transform canvas)
    {
        // Кнопка на стартовом экране (в голосовой группе: в полном режиме прячется вместе с ней).
        Button start = CreateButton("SettingsStartButton", _voiceGroup.transform, "Настрой меня", ToggleOn, 30);
        Image startImage = start.targetGraphic as Image;
        startImage.color = Color.white;
        ColorBlock startColors = start.colors;
        startColors.normalColor = ToggleOn;
        startColors.highlightedColor = Color.Lerp(ToggleOn, Color.white, 0.12f);
        startColors.pressedColor = Color.Lerp(ToggleOn, Color.black, 0.18f);
        start.colors = startColors;
        Place(start.GetComponent<RectTransform>(), new Vector2(0.06f, 0.055f), new Vector2(0.78f, 0.13f), Vector2.zero, Vector2.zero);
        Text startLabel = start.transform.Find("Label").GetComponent<Text>();
        Place(startLabel.rectTransform, new Vector2(0.2f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        AddGearIcon(start.transform, ToggleOn);
        start.onClick.AddListener(ShowSettings);
        _startSettingsButton = start.gameObject;
        _startSettingsButton.SetActive(!AppSettings.Configured);

        BuildSettingsPanel(canvas);
    }

    // Значок шестерёнки из простых фигур (шрифт может не содержать нужного символа).
    private void AddGearIcon(Transform parent, Color holeColor)
    {
        GameObject gear = new GameObject("Gear");
        gear.transform.SetParent(parent, false);
        RectTransform rect = gear.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.10f, 0.5f);
        rect.anchorMax = new Vector2(0.10f, 0.5f);
        rect.sizeDelta = new Vector2(64f, 64f);
        rect.anchoredPosition = Vector2.zero;

        Color light = new Color(0.92f, 0.96f, 1f, 1f);
        float[] angles = { 0f, 45f, 90f, 135f };
        foreach (float angle in angles)
        {
            Image bar = CreateImage("Tooth", gear.transform, light);
            bar.raycastTarget = false;
            Place(bar.rectTransform, new Vector2(0.02f, 0.40f), new Vector2(0.98f, 0.60f), Vector2.zero, Vector2.zero);
            bar.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
        }
        Image ring = CreateImage("Ring", gear.transform, light);
        SetBuiltInRoundSprite(ring);
        ring.raycastTarget = false;
        Place(ring.rectTransform, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f), Vector2.zero, Vector2.zero);
        Image hole = CreateImage("Hole", gear.transform, holeColor);
        SetBuiltInRoundSprite(hole);
        hole.raycastTarget = false;
        Place(hole.rectTransform, new Vector2(0.34f, 0.34f), new Vector2(0.66f, 0.66f), Vector2.zero, Vector2.zero);
    }

    private void BuildSettingsPanel(Transform canvas)
    {
        _settingsPanel = new GameObject("SettingsPanel");
        _settingsPanel.transform.SetParent(canvas, false);
        RectTransform panelRect = _settingsPanel.AddComponent<RectTransform>();
        Stretch(panelRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image background = _settingsPanel.AddComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.065f, 1f);
        background.raycastTarget = true;

        Text title = CreateText("SettingsTitle", _settingsPanel.transform, "Настройки", 40,
            new Color(0.78f, 0.88f, 1f), TextAnchor.MiddleLeft);
        Place(title.rectTransform, new Vector2(0.06f, 0.925f), new Vector2(0.94f, 0.985f), Vector2.zero, Vector2.zero);

        GameObject scrollObject = new GameObject("SettingsScroll");
        scrollObject.transform.SetParent(_settingsPanel.transform, false);
        _settingsScroll = scrollObject.AddComponent<ScrollRect>();
        Stretch(scrollObject.GetComponent<RectTransform>(), new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.915f), Vector2.zero, Vector2.zero);
        _settingsScroll.horizontal = false;
        _settingsScroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        Stretch(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewport.AddComponent<RectMask2D>();
        _settingsScroll.viewport = viewportRect;

        GameObject content = new GameObject("SettingsContent");
        content.transform.SetParent(viewport.transform, false);
        _settingsContent = content.AddComponent<RectTransform>();
        _settingsContent.anchorMin = new Vector2(0f, 1f);
        _settingsContent.anchorMax = new Vector2(1f, 1f);
        _settingsContent.pivot = new Vector2(0.5f, 1f);
        _settingsContent.anchoredPosition = Vector2.zero;
        _settingsContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 24);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _settingsScroll.content = _settingsContent;

        AddSectionLabel("ГОЛОС");
        _speakButton = AddToggle(() => { _setSpeak = !_setSpeak; RefreshToggles(); });

        AddSectionLabel("ДЕЙСТВИЯ АГЕНТА");
        AddHint("Что делать после просьбы позвонить или написать. Нажмите, чтобы сменить режим.");
        _actionsButton = AddToggle(() =>
        {
            _setActions = _setActions == ActionLevel.Manual ? ActionLevel.Confirm
                : _setActions == ActionLevel.Confirm ? ActionLevel.Auto : ActionLevel.Manual;
            RefreshToggles();
        });

        AddSectionLabel("ЧАТ");
        AddHint("Сообщения можно смахнуть вправо. То же голосом: «удали последнее сообщение», «очисти чат», «верни сообщение». Команда «/чат» напомнит всё.");
        _keepButton = AddToggle(() =>
        {
            _setKeep = _setKeep == ChatKeep.Never ? ChatKeep.Week : _setKeep == ChatKeep.Week ? ChatKeep.Forever : ChatKeep.Never;
            RefreshToggles();
        });
        _trashButton = AddToggle(() => { _setTrash = !_setTrash; RefreshToggles(); });

        AddSectionLabel("УМНЫЕ ОТВЕТЫ (необязательно)");
        AddHint("Агент сам понимает все команды. Модель нужна для вопросов и рассуждений вне команд.");
        _llmButton = AddToggle(() => { _setLlm = !_setLlm; RefreshToggles(); });
        _ollamaInput = AddInput("Адрес Ollama на вашем компьютере", "http://192.168.0.5:11434", false);
        _openRouterInput = AddInput("Ключ OpenRouter (бесплатные модели)", string.Empty, true);
        _groqInput = AddInput("Ключ Groq (бесплатные модели)", string.Empty, true);
        _deviceButton = AddToggle(() =>
        {
            if (Barabashka.Contracts.LocalLlmHost.Current == null)
            {
                SetSettingsInfo("Для модели в телефоне нужен пакет LLMUnity в проекте.");
                return;
            }
            _setDevice = !_setDevice;
            RefreshToggles();
        });
        AddHint("Модель в телефоне работает без сети, но медленно (около 1 ГБ, скачивается один раз).");

        AddSectionLabel("ПОГОДА");
        _cityInput = AddInput("Город по умолчанию", "Например: Москва", false);

        _settingsInfo = CreateText("SettingsInfo", _settingsPanel.transform, string.Empty, 19,
            new Color(0.95f, 0.78f, 0.40f), TextAnchor.MiddleCenter);
        Place(_settingsInfo.rectTransform, new Vector2(0.06f, 0.125f), new Vector2(0.94f, 0.17f), Vector2.zero, Vector2.zero);

        Button done = CreateButton("SettingsDone", _settingsPanel.transform, "Готово", ToggleOn, 34);
        (done.targetGraphic as Image).color = Color.white;
        ColorBlock doneColors = done.colors;
        doneColors.normalColor = ToggleOn;
        doneColors.highlightedColor = Color.Lerp(ToggleOn, Color.white, 0.12f);
        doneColors.pressedColor = Color.Lerp(ToggleOn, Color.black, 0.18f);
        done.colors = doneColors;
        Place(done.GetComponent<RectTransform>(), new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.115f), Vector2.zero, Vector2.zero);
        done.onClick.AddListener(ApplySettings);

        _settingsPanel.SetActive(false);
    }

    private void AddSectionLabel(string text)
    {
        Text label = CreateText("Section", _settingsContent, text, 20, new Color(0.43f, 0.66f, 0.84f), TextAnchor.LowerLeft);
        LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 44f;
        element.preferredHeight = 44f;
    }

    private void AddHint(string text)
    {
        Text hint = CreateText("Hint", _settingsContent, text, 18, new Color(0.55f, 0.65f, 0.76f), TextAnchor.UpperLeft);
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        LayoutElement element = hint.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 26f;
    }

    private Button AddToggle(UnityEngine.Events.UnityAction onClick)
    {
        Button button = CreateButton("Toggle", _settingsContent, string.Empty, ToggleOff, 26);
        (button.targetGraphic as Image).color = Color.white;
        LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 84f;
        element.preferredHeight = 84f;
        button.onClick.AddListener(onClick);
        return button;
    }

    private InputField AddInput(string caption, string placeholderText, bool secret)
    {
        Text label = CreateText("InputCaption", _settingsContent, caption, 19, new Color(0.70f, 0.80f, 0.90f), TextAnchor.LowerLeft);
        LayoutElement labelElement = label.gameObject.AddComponent<LayoutElement>();
        labelElement.minHeight = 32f;
        labelElement.preferredHeight = 32f;

        Image background = CreateImage("InputField", _settingsContent, new Color(0.10f, 0.14f, 0.20f, 1f));
        SetRoundedSprite(background);
        LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 76f;
        element.preferredHeight = 76f;

        InputField field = background.gameObject.AddComponent<InputField>();
        field.targetGraphic = background;
        field.lineType = InputField.LineType.SingleLine;
        field.contentType = secret ? InputField.ContentType.Password : InputField.ContentType.Standard;
        Text text = CreateText("Text", background.transform, string.Empty, 24, Color.white, TextAnchor.MiddleLeft);
        Place(text.rectTransform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f), Vector2.zero, Vector2.zero);
        Text placeholder = CreateText("Placeholder", background.transform, placeholderText, 22, new Color(0.48f, 0.57f, 0.68f), TextAnchor.MiddleLeft);
        Place(placeholder.rectTransform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f), Vector2.zero, Vector2.zero);
        field.textComponent = text;
        field.placeholder = placeholder;
        return field;
    }

    private void SetToggleVisual(Button button, string title, bool on)
    {
        Image image = button.targetGraphic as Image;
        Color color = on ? ToggleOn : ToggleOff;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        button.colors = colors;
        image.color = Color.white;
        button.transform.Find("Label").GetComponent<Text>().text = title + ":  " + (on ? "ВКЛ" : "ВЫКЛ");
    }

    private void SetCycleVisual(Button button, string title, Color color)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        button.colors = colors;
        (button.targetGraphic as Image).color = Color.white;
        button.transform.Find("Label").GetComponent<Text>().text = title;
    }

    private void RefreshToggles()
    {
        {
            Color color = _setActions == ActionLevel.Auto ? new Color(0.78f, 0.45f, 0.10f, 1f)
                : _setActions == ActionLevel.Confirm ? ToggleOn : ToggleOff;
            ColorBlock colors = _actionsButton.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            _actionsButton.colors = colors;
            (_actionsButton.targetGraphic as Image).color = Color.white;
            _actionsButton.transform.Find("Label").GetComponent<Text>().text = AppSettings.ActionsTitle(_setActions);
        }
        SetCycleVisual(_keepButton, AppSettings.ChatKeepTitle(_setKeep), _setKeep == ChatKeep.Never ? ToggleOff : ToggleOn);
        SetCycleVisual(_trashButton, AppSettings.ChatTrashTitle(_setTrash), _setTrash ? ToggleOn : ToggleOff);
        SetToggleVisual(_speakButton, "Озвучивать ответы", _setSpeak);
        SetToggleVisual(_llmButton, "Умные ответы через сеть", _setLlm);
        SetToggleVisual(_deviceButton, "Модель в телефоне (без сети)", _setDevice);
    }

    private void SetSettingsInfo(string text)
    {
        if (_settingsInfo != null) _settingsInfo.text = text ?? string.Empty;
    }

    private static string KeyPlaceholder(string profileName)
    {
        foreach (LlmProfile p in LlmProfiles.Extra())
            if (p.name == profileName && !string.IsNullOrEmpty(p.apiKey)) return "ключ задан (пусто - не менять)";
        return "вставьте ключ";
    }

    private static bool HasDeviceProfile()
    {
        foreach (LlmProfile p in LlmProfiles.Extra()) if (p.IsDevice) return true;
        return false;
    }

    /// <summary>Открыть окно настроек (кнопка «Настрой меня», команда «открой настройки»).</summary>
    public void ShowSettings()
    {
        if (_settingsPanel == null) return;
        _setSpeak = AppSettings.SpeakReplies;
        _setActions = AppSettings.Actions;
        _setKeep = AppSettings.ChatHistory;
        _setTrash = AppSettings.ChatTrash;
        _setLlm = LlmSettings.Enabled;
        _setDevice = HasDeviceProfile();

        _ollamaInput.SetTextWithoutNotify(LlmSettings.HasCustomUrl ? LlmSettings.Url : string.Empty);
        _openRouterInput.SetTextWithoutNotify(string.Empty);
        _groqInput.SetTextWithoutNotify(string.Empty);
        ((Text)_openRouterInput.placeholder).text = KeyPlaceholder("openrouter");
        ((Text)_groqInput.placeholder).text = KeyPlaceholder("groq");
        AgentWeather.City city = AgentWeather.DefaultCity();
        _cityInput.SetTextWithoutNotify(city != null ? city.Name : string.Empty);

        RefreshToggles();
        SetSettingsInfo(string.Empty);
        _settingsPanel.SetActive(true);
        _settingsPanel.transform.SetAsLastSibling();
        _settingsOpen = true;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_settingsContent);
        _settingsScroll.verticalNormalizedPosition = 1f;
    }

    private void HideSettings()
    {
        _settingsOpen = false;
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
    }

    // Кнопка «Назад» на Android закрывает окно без сохранения.
    private bool HandleSettingsBack()
    {
        if (!_settingsOpen) return false;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HideSettings();
        return true;
    }

    private void ApplySettings()
    {
        // Сначала проверяем то, что может быть введено неверно.
        string url = _ollamaInput.text.Trim();
        if (url.Length > 0)
        {
            if (url.IndexOf("://", System.StringComparison.Ordinal) < 0) url = "http://" + url;
            System.Uri uri;
            if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out uri) ||
                (uri.Scheme != System.Uri.UriSchemeHttp && uri.Scheme != System.Uri.UriSchemeHttps))
            {
                SetSettingsInfo("Адрес Ollama не похож на адрес. Пример: http://192.168.0.5:11434");
                return;
            }
        }

        AppSettings.SpeakReplies = _setSpeak;
        AppSettings.Actions = _setActions;
        AppSettings.ChatHistory = _setKeep;
        AppSettings.ChatTrash = _setTrash;
        Persist();
        LlmSettings.Enabled = _setLlm;
        if (url.Length == 0) LlmSettings.ClearUrl(); else LlmSettings.Url = url;
        SaveCloudKey("openrouter", _openRouterInput.text.Trim());
        SaveCloudKey("groq", _groqInput.text.Trim());

        string deviceNote = null;
        if (_setDevice)
        {
            if (LocalModel.Exists(LocalModel.DefaultPath))
                LlmProfiles.AddOrReplace(new LlmProfile { name = LocalModel.ProfileName, kind = "device", url = string.Empty, model = LocalModel.DefaultPath, apiKey = string.Empty });
            else if (LocalModel.StartDownload(null))
                deviceNote = "Скачиваю модель для работы без сети (около 1 ГБ), скажу, когда закончу.";
        }
        else if (HasDeviceProfile())
        {
            LlmProfiles.Remove(LocalModel.ProfileName);
            if (Barabashka.Contracts.LocalLlmHost.Current != null) Barabashka.Contracts.LocalLlmHost.Current.Unload();
        }

        string city = _cityInput.text.Trim();
        AgentWeather.City current = AgentWeather.DefaultCity();
        if (city.Length > 0 && (current == null || !string.Equals(current.Name, city, System.StringComparison.OrdinalIgnoreCase)))
            ApplyCityAsync(city);

        AppSettings.Configured = true;
        if (_startSettingsButton != null) _startSettingsButton.SetActive(false);
        HideSettings();
        SetStatus("Настройки сохранены");
        if (deviceNote != null) AddSystemMessage(deviceNote);
    }

    private static void SaveCloudKey(string name, string key)
    {
        if (key.Length == 0) return;
        LlmProfiles.Preset preset;
        if (!LlmProfiles.TryGetPreset(name, out preset)) return;
        string model = preset.Model;
        foreach (LlmProfile p in LlmProfiles.Extra())
            if (p.name == name && !string.IsNullOrEmpty(p.model)) model = p.model;
        LlmProfiles.AddOrReplace(new LlmProfile { name = name, kind = preset.Kind, url = preset.Url, model = model, apiKey = key });
    }

    private async void ApplyCityAsync(string city)
    {
        IAgentTool tool;
        if (!AgentToolRegistry.Default.TryGet("set_city", out tool)) return;
        try
        {
            var args = new Dictionary<string, string> { { "city", city } };
            ToolResult result = await tool.ExecuteAsync(args, CancellationToken.None);
            AddSystemMessage(result.Success ? result.Output : result.Error);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Settings] Город: " + ex.Message);
        }
    }

    /// <summary>Приветствие и «готов» показываются только после загрузки голосовой модели.</summary>
    public void ShowReady()
    {
        string greeting = AppSettings.Configured
            ? "Привет! Я готов помочь. Говорите или откройте интерфейс, чтобы написать."
            : "Привет! Я Барабашка. Можно сразу говорить, а чтобы настроить меня под себя, нажмите «Настрой меня».";
        AddMessage("Barabashka", greeting, false, true, true, ChatKind.Dialog);
        SetStatus("Готов к работе");
    }
}
