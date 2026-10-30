using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ISettingsPage = Barabashka.Contracts.ISettingsPage;
using ISettingsView = Barabashka.Contracts.ISettingsView;
using SettingsRegistry = Barabashka.Contracts.SettingsRegistry;

/// <summary>
/// Режим настроек внутри чата. Шестерёнка в ряду кнопок включает его: поле чата показывает страницу настроек,
/// кнопки разделов становятся кнопками страниц (SettingsRegistry), повторное нажатие шестерёнки, Escape или «Назад» возвращают чат.
/// Значения применяются сразу; подтверждения (в том числе от голосовых команд) видны над страницей.
/// Страницы строятся через ISettingsView и не знают про интерфейс.
/// </summary>
public partial class AgentUIManager
{
    private static readonly Color SettingsOn = new Color(0.12f, 0.48f, 0.82f, 1f);
    private static readonly Color SettingsOff = new Color(0.20f, 0.25f, 0.32f, 1f);
    private const int MaxConfirmations = 3;

    private GameObject _startSettingsButton;
    private GameObject _settingsRoot;
    private Text _settingsTitleText, _settingsInfoText;
    private RectTransform _settingsContent;
    private ScrollRect _settingsScroll;
    private bool _settingsMode;
    private string _currentSettingsPageId;
    private readonly List<string> _settingsConfirmations = new List<string>();

    // ---- построение ----

    // Кнопка «Настрой меня» на стартовом экране и панель страницы внутри панели чата.
    private void BuildSettingsUi(Transform canvas)
    {
        Button start = CreateButton("SettingsStartButton", _voiceGroup.transform, "Настрой меня", SettingsOn, 30);
        SetButtonColor(start, SettingsOn);
        Place(start.GetComponent<RectTransform>(), new Vector2(0.06f, 0.055f), new Vector2(0.78f, 0.13f), Vector2.zero, Vector2.zero);
        Text startLabel = start.transform.Find("Label").GetComponent<Text>();
        Place(startLabel.rectTransform, new Vector2(0.2f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        AddGearIcon(start.transform, SettingsOn);
        start.onClick.AddListener(() => ShowSettings());
        _startSettingsButton = start.gameObject;
        _startSettingsButton.SetActive(!AppSettings.Configured);

        BuildSettingsPanel();
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

    private void BuildSettingsPanel()
    {
        Transform chatPanel = _scrollRect != null ? _scrollRect.transform.parent : null;
        if (chatPanel == null) return;

        _settingsRoot = new GameObject("SettingsRoot");
        _settingsRoot.transform.SetParent(chatPanel, false);
        RectTransform rootRect = _settingsRoot.AddComponent<RectTransform>();
        Stretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        _settingsTitleText = CreateText("SettingsTitle", rootRect, "Настройки", 32, new Color(0.78f, 0.88f, 1f), TextAnchor.MiddleLeft);
        Place(_settingsTitleText.rectTransform, new Vector2(0.05f, 0.915f), new Vector2(0.95f, 0.985f), Vector2.zero, Vector2.zero);

        // Подтверждения изменений (последние три): сюда же попадают ответы на голосовые команды настроек.
        _settingsInfoText = CreateText("SettingsInfo", rootRect, string.Empty, 20, new Color(0.95f, 0.78f, 0.40f), TextAnchor.UpperLeft);
        _settingsInfoText.verticalOverflow = VerticalWrapMode.Truncate;
        Place(_settingsInfoText.rectTransform, new Vector2(0.05f, 0.805f), new Vector2(0.95f, 0.91f), Vector2.zero, Vector2.zero);

        GameObject scrollObject = new GameObject("SettingsScroll");
        scrollObject.transform.SetParent(rootRect, false);
        _settingsScroll = scrollObject.AddComponent<ScrollRect>();
        Stretch(scrollObject.GetComponent<RectTransform>(), new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.8f), Vector2.zero, Vector2.zero);
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

        _settingsRoot.SetActive(false);
    }

    // ---- вход и выход ----

    /// <summary>
    /// Открыть режим настроек (шестерёнка, кнопка «Настрой меня», команда «открой настройки»).
    /// pageId - идентификатор или алиас страницы; без него остаётся текущая (или первая).
    /// confirmation - текст, который сразу показывается над страницей (ответ на голосовую команду).
    /// </summary>
    public void ShowSettings(string pageId = null, string confirmation = null)
    {
        if (_settingsRoot == null) return;
        AppSettings.Configured = true;
        if (_startSettingsButton != null) _startSettingsButton.SetActive(false);
        if (!_fullMode) SetMode(true);

        if (!_settingsMode)
        {
            _settingsMode = true;
            _settingsConfirmations.Clear();
            if (_settingsInfoText != null) _settingsInfoText.text = string.Empty;
            if (_scrollRect != null) _scrollRect.gameObject.SetActive(false);
            _settingsRoot.SetActive(true);
            SettingsRegistry.PagesChanged += OnSettingsPagesChanged;
            ApplyTabsForSettings();
        }

        ShowSettingsPage(pageId);
        if (!string.IsNullOrEmpty(confirmation)) ShowSettingsConfirmation(confirmation);
    }

    /// <summary>Выйти из режима настроек: вернуть чат, кнопки разделов и прежний фильтр.</summary>
    public void HideSettings()
    {
        ExitSettingsMode();
    }

    private void ExitSettingsMode()
    {
        if (!_settingsMode) return;
        _settingsMode = false;
        _currentSettingsPageId = null;
        SettingsRegistry.PagesChanged -= OnSettingsPagesChanged;
        if (_settingsRoot != null) _settingsRoot.SetActive(false);
        if (_scrollRect != null) _scrollRect.gameObject.SetActive(true);
        ClearSettingsContent();
        RestoreTabsFromSettings();
        ApplyFilter();
    }

    // Кнопка «Назад» на Android приходит как Escape: сначала закрываем режим настроек.
    private bool HandleSettingsBack()
    {
        if (!_settingsMode) return false;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ExitSettingsMode();
            return true;
        }
        return false;
    }

    private void OnSettingsPagesChanged()
    {
        if (_settingsMode) ApplyTabsForSettings();
    }

    // ---- кнопки в ряду ----

    // Номер шестерёнки (раздел System) в ряду кнопок: ряд начинается с кнопки «Все».
    private static int GearTabIndex()
    {
        for (int i = 1; i <= ChatKinds.Count; i++)
            if (ChatKinds.All[i - 1] == ChatKind.System) return i;
        return ChatKinds.Count;
    }

    private void OnTabClicked(int index)
    {
        int gear = GearTabIndex();
        if (_settingsMode)
        {
            if (index == gear) { ExitSettingsMode(); return; }
            IReadOnlyList<ISettingsPage> pages = SettingsRegistry.Pages;
            if (index >= 0 && index < pages.Count) ShowSettingsPage(pages[index].Id);
            return;
        }
        if (index == gear) { ShowSettings(); return; }
        SelectTab(index);
    }

    private static void SetTabIcon(Button button, string iconId)
    {
        if (button == null) return;
        Transform icon = button.transform.Find("Icon");
        if (icon != null) icon.GetComponent<Image>().sprite = ChatIcons.Get(iconId);
    }

    // Кнопки 0..N-1 показывают страницы, шестерёнка остаётся на месте, лишние прячутся.
    private void ApplyTabsForSettings()
    {
        IReadOnlyList<ISettingsPage> pages = SettingsRegistry.Pages;
        int gear = GearTabIndex();
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            Button button = _tabButtons[i];
            if (button == null) continue;
            if (i == gear) { button.gameObject.SetActive(true); SetTabIcon(button, "system"); continue; }
            bool hasPage = i < pages.Count;
            button.gameObject.SetActive(hasPage);
            if (hasPage) SetTabIcon(button, pages[i].IconId);
        }
        RefreshSettingsTabs();
    }

    private void RestoreTabsFromSettings()
    {
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            Button button = _tabButtons[i];
            if (button == null) continue;
            button.gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(_tabIconIds[i])) SetTabIcon(button, _tabIconIds[i]);
        }
    }

    private void RefreshSettingsTabs()
    {
        IReadOnlyList<ISettingsPage> pages = SettingsRegistry.Pages;
        int gear = GearTabIndex();
        Color baseColor = ChatKinds.Tab(ChatKind.System);
        Color dim = Color.Lerp(ChatKinds.Bubble(ChatKind.System, true), baseColor, 0.35f);
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            Button button = _tabButtons[i];
            if (button == null || !button.gameObject.activeSelf) continue;
            bool isGear = i == gear;
            bool selected = isGear || (i < pages.Count && pages[i].Id == _currentSettingsPageId);
            Color color = isGear ? Color.Lerp(baseColor, Color.white, 0.30f) : (selected ? baseColor : dim);
            SetButtonColor(button, color);
            Transform icon = button.transform.Find("Icon");
            if (icon != null) icon.GetComponent<Image>().color = selected ? Color.white : new Color(1f, 1f, 1f, 0.8f);
            Transform dot = button.transform.Find("Dot");
            if (dot != null) dot.gameObject.SetActive(false);
        }
    }

    // ---- страница ----

    private void ShowSettingsPage(string pageId)
    {
        ISettingsPage page = null;
        if (string.IsNullOrEmpty(pageId))
        {
            if (_currentSettingsPageId != null) page = SettingsRegistry.Find(_currentSettingsPageId);
            if (page == null && SettingsRegistry.Pages.Count > 0) page = SettingsRegistry.Pages[0];
        }
        else
        {
            page = SettingsRegistry.Find(pageId);
        }

        if (page == null)
        {
            ShowSettingsConfirmation("Такой страницы настроек нет.");
            return;
        }

        _currentSettingsPageId = page.Id;
        if (_settingsTitleText != null) _settingsTitleText.text = page.Title;
        ClearSettingsContent();
        try
        {
            page.Build(new SettingsView(this));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Settings] Страница «" + page.Id + "»: " + ex.Message);
            ShowSettingsConfirmation("Не удалось показать страницу: " + ex.Message);
        }
        RefreshSettingsTabs();
        RebuildSettingsLayout();
        StartCoroutine(RebuildSettingsNextFrame());
    }

    private void ClearSettingsContent()
    {
        if (_settingsContent == null) return;
        for (int i = _settingsContent.childCount - 1; i >= 0; i--)
        {
            GameObject child = _settingsContent.GetChild(i).gameObject;
            child.SetActive(false);   // уничтожение откладывается до конца кадра: пока не мешаем раскладке
            Destroy(child);
        }
    }

    private void RebuildSettingsLayout()
    {
        if (_settingsContent == null) return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_settingsContent);
        if (_settingsScroll != null) _settingsScroll.verticalNormalizedPosition = 1f;
    }

    private IEnumerator RebuildSettingsNextFrame()
    {
        yield return null;
        if (_settingsMode) RebuildSettingsLayout();
    }

    /// <summary>Подтверждение изменения (или ошибка) над страницей: последние три. Вызывается и голосовыми командами.</summary>
    public void ShowSettingsConfirmation(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _settingsConfirmations.Add(text.Trim());
        while (_settingsConfirmations.Count > MaxConfirmations) _settingsConfirmations.RemoveAt(0);
        if (_settingsInfoText != null) _settingsInfoText.text = string.Join("\n", _settingsConfirmations.ToArray());
    }

    /// <summary>Приветствие и «готов» показываются только после загрузки голосовой модели (вызывает AgentManager).</summary>
    public void ShowReady()
    {
        string greeting = AppSettings.Configured
            ? "Привет! Я готов помочь. Говорите или откройте интерфейс, чтобы написать."
            : "Привет! Я Барабашка. Можно сразу говорить, а чтобы настроить меня под себя, нажмите «Настрой меня».";
        AddMessage("Barabashka", greeting, false, true, true, ChatKind.Dialog);
        SetStatus("Готов к работе");
    }

    private void AfterSettingChanged(string message)
    {
        ShowSettingsConfirmation(message);
        // Хранение и корзина чата влияют на сохранённую историю: перезаписываем её по новым правилам.
        if (_currentSettingsPageId == "chat") Persist();
    }

    private static string SafeSet(Func<string> action)
    {
        try { return action(); }
        catch (Exception ex)
        {
            Debug.LogWarning("[Settings] " + ex.Message);
            return "Не удалось применить: " + ex.Message;
        }
    }

    // ---- виджеты ----

    private void AddSettingsSection(string title)
    {
        Text label = CreateText("Section", _settingsContent, (title ?? string.Empty).ToUpperInvariant(), 21, new Color(0.43f, 0.66f, 0.84f), TextAnchor.LowerLeft);
        LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 44f;
        element.preferredHeight = 44f;
    }

    private void AddSettingsHint(string text)
    {
        Text hint = CreateText("Hint", _settingsContent, text, 19, new Color(0.55f, 0.65f, 0.76f), TextAnchor.UpperLeft);
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        LayoutElement element = hint.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 26f;
    }

    private void AddSettingsCaption(string text)
    {
        Text label = CreateText("Caption", _settingsContent, text, 20, new Color(0.70f, 0.80f, 0.90f), TextAnchor.LowerLeft);
        LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 32f;
        element.preferredHeight = 32f;
    }

    private Button AddSettingsButton()
    {
        Button button = CreateButton("SettingsButton", _settingsContent, string.Empty, SettingsOff, 25);
        LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 84f;
        element.preferredHeight = 84f;
        return button;
    }

    private static void SetSettingsButton(Button button, string label, bool on)
    {
        SetButtonColor(button, on ? SettingsOn : SettingsOff);
        button.transform.Find("Label").GetComponent<Text>().text = label;
    }

    private InputField AddSettingsInputField(string placeholderText, bool secret)
    {
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
        Text placeholder = CreateText("Placeholder", background.transform, placeholderText ?? string.Empty, 22, new Color(0.48f, 0.57f, 0.68f), TextAnchor.MiddleLeft);
        Place(placeholder.rectTransform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f), Vector2.zero, Vector2.zero);
        field.textComponent = text;
        field.placeholder = placeholder;
        return field;
    }

    // Страница строит себя через этот класс и ничего не знает про интерфейс.
    private sealed class SettingsView : ISettingsView
    {
        private readonly AgentUIManager _ui;

        public SettingsView(AgentUIManager ui) { _ui = ui; }

        public void Section(string title) { _ui.AddSettingsSection(title); }

        public void Hint(string text) { _ui.AddSettingsHint(text); }

        public void Toggle(string title, Func<bool> get, Func<bool, string> set)
        {
            Button button = _ui.AddSettingsButton();
            Action refresh = () => SetSettingsButton(button, title + ":  " + (get() ? "ВКЛ" : "ВЫКЛ"), get());
            refresh();
            button.onClick.AddListener(() =>
            {
                string result = SafeSet(() => set(!get()));
                refresh();
                _ui.AfterSettingChanged(result);
            });
        }

        public void Choice(string title, string[] options, Func<int> get, Func<int, string> set)
        {
            if (options == null || options.Length == 0) return;
            _ui.AddSettingsCaption(title);
            Button button = _ui.AddSettingsButton();
            Action refresh = () =>
            {
                int current = Mathf.Clamp(get(), 0, options.Length - 1);
                SetSettingsButton(button, options[current], current != 0);
            };
            refresh();
            button.onClick.AddListener(() =>
            {
                int next = (Mathf.Clamp(get(), 0, options.Length - 1) + 1) % options.Length;
                string result = SafeSet(() => set(next));
                refresh();
                _ui.AfterSettingChanged(result);
            });
        }

        public void Input(string caption, string placeholder, bool secret, Func<string> get, Func<string, string> set)
        {
            _ui.AddSettingsCaption(caption);
            InputField field = _ui.AddSettingsInputField(placeholder, secret);
            string initial = secret ? string.Empty : (get() ?? string.Empty);
            field.SetTextWithoutNotify(initial);
            string lastApplied = initial;
            field.onEndEdit.AddListener(value =>
            {
                string v = (value ?? string.Empty).Trim();
                if (secret) { if (v.Length == 0) return; }
                else if (v == lastApplied) return;
                lastApplied = v;
                string result = SafeSet(() => set(v));
                if (secret) field.SetTextWithoutNotify(string.Empty);
                _ui.AfterSettingChanged(result);
            });
        }
    }
}
