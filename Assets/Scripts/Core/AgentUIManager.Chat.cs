using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Общий чат с разделами: цвет подложки по виду сообщения, цветные кнопки разделов вверху, смахивание вправо,
/// корзина и хранение истории. Всё, что делается руками, дублируется голосовыми командами (ChatCommands).
/// </summary>
public partial class AgentUIManager
{
    private sealed class CardView
    {
        public GameObject Root;
        public Image Background;
    }

    private struct UndoItem
    {
        public ChatRecord Record;
        public int Index;
    }

    private const float UndoSeconds = 7f;

    private readonly List<ChatRecord> _records = new List<ChatRecord>();
    private readonly Dictionary<long, CardView> _cards = new Dictionary<long, CardView>();
    private readonly List<ChatRecord> _turn = new List<ChatRecord>();
    private readonly List<UndoItem> _undo = new List<UndoItem>();
    private readonly Button[] _tabButtons = new Button[ChatKinds.Count + 1];
    private readonly bool[] _unread = new bool[ChatKinds.Count];
    private long _nextId = 1;
    private bool _filterAll = true;
    private ChatKind _filterKind;
    private Button _undoButton;
    private Coroutine _undoTimer;

    // ---- построение ----

    // Ряд цветных кнопок в «брови» чата: «Все» и по одной на раздел.
    private void BuildChatTabs(Transform parent)
    {
        int total = ChatKinds.Count + 1;
        for (int i = 0; i < total; i++)
        {
            int index = i;
            string title = i == 0 ? "Все" : ChatKinds.Title(ChatKinds.All[i - 1]);
            Button button = CreateButton("Tab" + i, parent, title, new Color(0.2f, 0.25f, 0.32f, 1f), 17);
            float x0 = 0.05f + 0.75f * i / total;
            float x1 = 0.05f + 0.75f * (i + 1) / total;
            Place(button.GetComponent<RectTransform>(), new Vector2(x0, 0.783f), new Vector2(x1, 0.818f),
                new Vector2(3f, 0f), new Vector2(-3f, 0f));
            button.onClick.AddListener(() => SelectTab(index));
            AddTabIcon(button, i == 0 ? "all" : ChatKinds.All[i - 1].ToString().ToLowerInvariant());
            _tabButtons[i] = button;
        }
        RefreshTabs();
    }

    // Вместо надписи на кнопке раздела - значок; точка в углу значит «есть непрочитанное».
    private void AddTabIcon(Button button, string iconId)
    {
        Transform label = button.transform.Find("Label");
        if (label != null) label.gameObject.SetActive(false);

        Image icon = CreateImage("Icon", button.transform, Color.white);
        icon.sprite = ChatIcons.Get(iconId);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Stretch(icon.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 4f), new Vector2(-5f, -4f));

        Image dot = CreateImage("Dot", button.transform, new Color(1f, 0.30f, 0.25f, 1f));
        SetRoundedSprite(dot);
        dot.raycastTarget = false;
        Place(dot.rectTransform, new Vector2(0.70f, 0.58f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);
        dot.gameObject.SetActive(false);
    }

    // Строка «Сообщение удалено - Вернуть» поверх нижней части чата.
    private void BuildUndoToast(Transform parent)
    {
        _undoButton = CreateButton("UndoToast", parent, string.Empty, new Color(0.30f, 0.22f, 0.08f, 1f), 21);
        SetButtonColor(_undoButton, new Color(0.30f, 0.22f, 0.08f, 1f));
        Place(_undoButton.GetComponent<RectTransform>(), new Vector2(0.10f, 0.20f), new Vector2(0.90f, 0.245f), Vector2.zero, Vector2.zero);
        _undoButton.onClick.AddListener(() => { RestoreLast(); });
        _undoButton.gameObject.SetActive(false);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        button.colors = colors;
        (button.targetGraphic as Image).color = Color.white;
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            Button button = _tabButtons[i];
            if (button == null) continue;
            bool selected = i == 0 ? _filterAll : (!_filterAll && _filterKind == ChatKinds.All[i - 1]);
            Color color;
            bool hasUnread = false;
            if (i == 0)
            {
                color = selected ? new Color(0.45f, 0.55f, 0.68f, 1f) : new Color(0.20f, 0.25f, 0.32f, 1f);
            }
            else
            {
                ChatKind kind = ChatKinds.All[i - 1];
                color = selected ? ChatKinds.Tab(kind) : Color.Lerp(ChatKinds.Bubble(kind, true), ChatKinds.Tab(kind), 0.35f);
                hasUnread = _unread[i - 1] && !selected;
            }
            SetButtonColor(button, color);

            Transform icon = button.transform.Find("Icon");
            if (icon != null) icon.GetComponent<Image>().color = selected ? Color.white : new Color(1f, 1f, 1f, 0.8f);
            Transform dot = button.transform.Find("Dot");
            if (dot != null) dot.gameObject.SetActive(hasUnread);
        }
    }

    private void SelectTab(int index)
    {
        if (index == 0) { _filterAll = true; }
        else { _filterAll = false; _filterKind = ChatKinds.All[index - 1]; _unread[index - 1] = false; }
        ApplyFilter();
    }

    private bool IsVisible(ChatRecord rec)
    {
        return !rec.Deleted && (_filterAll || rec.Kind == _filterKind);
    }

    private void ApplyFilter()
    {
        foreach (ChatRecord rec in _records)
        {
            CardView view;
            if (_cards.TryGetValue(rec.Id, out view) && view.Root != null) view.Root.SetActive(IsVisible(rec));
        }
        RefreshTabs();
        RebuildChatLayout();
        StartCoroutine(RebuildChatNextFrame());
    }

    // ---- карточки ----

    private void BuildCard(ChatRecord rec)
    {
        GameObject card = new GameObject(rec.IsUser ? "UserMessage" : "AgentMessage");
        card.transform.SetParent(_messageContent, false);
        Image cardImage = card.AddComponent<Image>();
        SetRoundedSprite(cardImage);
        cardImage.color = ChatKinds.Bubble(rec.Kind, rec.IsUser);
        VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(14, 14, 10, 10);
        cardLayout.spacing = 6f;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandHeight = false;
        cardLayout.childForceExpandWidth = false;

        Text speakerText = CreateText("Speaker", card.transform, rec.Speaker, 16,
            rec.IsUser ? new Color(0.55f, 0.78f, 1f) : new Color(0.35f, 0.78f, 0.95f),
            TextAnchor.MiddleLeft);
        LayoutElement speakerLayout = speakerText.gameObject.AddComponent<LayoutElement>();
        speakerLayout.preferredHeight = 24f;
        speakerLayout.flexibleWidth = 1f;

        Text messageText = CreateText("MessageText", card.transform, rec.Text, 23,
            new Color(0.89f, 0.93f, 0.98f), TextAnchor.UpperLeft);
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        messageText.verticalOverflow = VerticalWrapMode.Overflow;
        messageText.supportRichText = false;
        LayoutElement messageLayout = messageText.gameObject.AddComponent<LayoutElement>();
        messageLayout.minHeight = 30f;
        messageLayout.flexibleWidth = 1f;

        if (!string.IsNullOrEmpty(rec.Media)) AddMediaThumbnail(card.transform, rec);

        string copied = rec.Text;
        Button copyButton = CreateButton("CopyMessage", card.transform, "Копировать",
            new Color(0.13f, 0.19f, 0.27f, 1f), 14);
        LayoutElement copyLayout = copyButton.gameObject.AddComponent<LayoutElement>();
        copyLayout.preferredHeight = 34f;
        copyLayout.preferredWidth = 150f;
        copyLayout.flexibleWidth = 0f;
        copyButton.onClick.AddListener(() =>
        {
            GUIUtility.systemCopyBuffer = copied;
            SetStatus("Сообщение скопировано");
        });

        long id = rec.Id;
        SwipeToDismiss swipe = card.AddComponent<SwipeToDismiss>();
        swipe.Init(_scrollRect, () => DeleteRecords(new List<ChatRecord> { FindRecord(id) }));

        _cards[rec.Id] = new CardView { Root = card, Background = cardImage };
        card.SetActive(IsVisible(rec));
    }

    // Миниатюра вложения: нажатие открывает полноэкранный просмотр (его даёт блок Media через MediaServices).
    private void AddMediaThumbnail(Transform parent, ChatRecord rec)
    {
        Barabashka.Contracts.MediaItem item = Barabashka.Contracts.MediaCodec.Decode(rec.Media);
        if (item == null) return;

        Image frame = CreateImage("MediaThumb", parent, new Color(0.10f, 0.13f, 0.19f, 1f));
        SetRoundedSprite(frame);
        LayoutElement frameLayout = frame.gameObject.AddComponent<LayoutElement>();
        frameLayout.preferredHeight = 320f;
        frameLayout.flexibleWidth = 1f;

        bool isVideo = item.Kind == Barabashka.Contracts.MediaKind.Video;
        Texture2D texture = ChatThumbnails.Load(item, 512);
        Image picture = CreateImage("Picture", frame.transform, Color.white);
        picture.raycastTarget = false;
        if (texture != null)
        {
            picture.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            picture.preserveAspect = true;
            frame.gameObject.AddComponent<ThumbnailOwner>().Init(texture, picture.sprite);
            Stretch(picture.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        }
        else
        {
            // Файла нет или он не читается: значок раздела «Медиа» на тёмной подложке.
            picture.sprite = ChatIcons.Get("media");
            picture.color = new Color(1f, 1f, 1f, 0.45f);
            picture.preserveAspect = true;
            Place(picture.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, -60f), new Vector2(60f, 60f));
        }

        if (isVideo)
        {
            // Кнопка воспроизведения поверх кадра и длительность в углу.
            Image play = CreateImage("PlayBadge", frame.transform, new Color(1f, 1f, 1f, 0.92f));
            play.sprite = ChatIcons.Get("play");
            play.raycastTarget = false;
            Place(play.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-55f, -55f), new Vector2(55f, 55f));
            if (item.DurationSeconds > 0)
            {
                int total = (int)item.DurationSeconds;
                string label = total >= 3600
                    ? (total / 3600) + ":" + ((total % 3600) / 60).ToString("00") + ":" + (total % 60).ToString("00")
                    : (total / 60) + ":" + (total % 60).ToString("00");
                Text duration = CreateText("Duration", frame.transform, label, 30, Color.white, TextAnchor.LowerRight);
                duration.raycastTarget = false;
                Stretch(duration.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-16f, -10f));
            }

            // Кнопка «на весь экран» в углу миниатюры.
            Button fullscreenBtn = CreateButton("FullscreenBadge", frame.transform, string.Empty, new Color(0f, 0f, 0f, 0f), 1);
            Image fullscreenIcon = CreateImage("FullscreenIcon", fullscreenBtn.transform, new Color(1f, 1f, 1f, 0.92f));
            fullscreenIcon.sprite = ChatIcons.Get("fullscreen");
            fullscreenIcon.preserveAspect = true;
            fullscreenIcon.raycastTarget = false;
            Stretch(fullscreenIcon.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            fullscreenBtn.onClick.AddListener(() =>
            {
                // Открываем просмотр и сразу включаем полноэкранный режим
                OpenMedia(id);
                // Небольшая задержка, чтобы просмотр успел открыться
                StartCoroutine(EnableFullscreenAfterOpen());
            });
        }

        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        long id = rec.Id;
        button.onClick.AddListener(() => OpenMedia(id));
    }

    // Открывает просмотр: все вложения того же вида (картинки или видео), начиная с выбранного, чтобы листать их.
    private void OpenMedia(long id)
    {
        Barabashka.Contracts.IMediaViewer viewer = Barabashka.Contracts.MediaServices.Viewer;
        if (viewer == null)
        {
            SetStatus("Просмотр картинок сейчас недоступен");
            return;
        }

        var items = new List<Barabashka.Contracts.MediaItem>();
        int start = 0;
        ChatRecord tapped = FindRecord(id);
        Barabashka.Contracts.MediaItem tappedItem = tapped != null && !string.IsNullOrEmpty(tapped.Media)
            ? Barabashka.Contracts.MediaCodec.Decode(tapped.Media) : null;
        if (tappedItem == null)
        {
            SetStatus("Это вложение пока нельзя открыть");
            return;
        }
        foreach (ChatRecord other in _records)
        {
            if (other.Deleted || string.IsNullOrEmpty(other.Media)) continue;
            Barabashka.Contracts.MediaItem item = Barabashka.Contracts.MediaCodec.Decode(other.Media);
            if (item == null || item.Kind != tappedItem.Kind) continue;
            if (other.Id == id) start = items.Count;
            items.Add(item);
        }
        if (items.Count == 0)
        {
            SetStatus("Это вложение пока нельзя открыть");
            return;
        }
        viewer.Show(items, start);
    }

    private System.Collections.IEnumerator EnableFullscreenAfterOpen()
    {
        // Ждём один кадр, чтобы просмотр успел открыться
        yield return null;
        var viewer = Barabashka.Contracts.MediaServices.Viewer as Barabashka.Contracts.IMediaPlayback;
        if (viewer != null && viewer.HasVideo)
        {
            viewer.SetFullscreen(true);
        }
    }

    private ChatRecord FindRecord(long id)
    {
        foreach (ChatRecord rec in _records) if (rec.Id == id) return rec;
        return null;
    }

    private void DestroyCard(long id)
    {
        CardView view;
        if (_cards.TryGetValue(id, out view))
        {
            if (view.Root != null) Destroy(view.Root);
            _cards.Remove(id);
        }
    }

    // Новая карточка встаёт на своё место по порядку записей.
    private void PlaceCard(ChatRecord rec)
    {
        CardView view;
        if (!_cards.TryGetValue(rec.Id, out view) || view.Root == null) return;
        int index = 0;
        foreach (ChatRecord other in _records)
        {
            if (other == rec) break;
            if (_cards.ContainsKey(other.Id)) index++;
        }
        view.Root.transform.SetSiblingIndex(index);
    }

    private void Persist()
    {
        ChatStore.Save(_records);
    }

    /// <summary>Читает историю с диска и показывает её (вызывается один раз при старте).</summary>
    private void RestoreChat()
    {
        List<ChatRecord> loaded = ChatStore.Load();
        foreach (ChatRecord rec in loaded)
        {
            _records.Add(rec);
            if (rec.Id >= _nextId) _nextId = rec.Id + 1;
            if (!rec.Deleted) BuildCard(rec);
        }
        if (loaded.Count > 0)
        {
            RebuildChatLayout();
            StartCoroutine(RebuildChatNextFrame());
        }
    }

    /// <summary>Сообщение уже получило раздел (или раздел уточнён): перекрашиваем сообщения текущего хода.</summary>
    private void OnKindChanged(ChatKind kind)
    {
        if (_turn.Count == 0) return;
        foreach (ChatRecord rec in _turn)
        {
            rec.Kind = kind;
            CardView view;
            if (_cards.TryGetValue(rec.Id, out view) && view.Background != null)
            {
                view.Background.color = ChatKinds.Bubble(kind, rec.IsUser);
                view.Root.SetActive(IsVisible(rec));
            }
            if (!IsVisible(rec) && !rec.Deleted) _unread[(int)kind] = true;
        }
        RefreshTabs();
        RebuildChatLayout();
        Persist();
    }

    // ---- удаление и возврат ----

    private void DeleteRecords(List<ChatRecord> records)
    {
        _undo.Clear();
        bool trash = AppSettings.ChatTrash;
        foreach (ChatRecord rec in records)
        {
            if (rec == null || rec.Deleted) continue;
            int index = _records.IndexOf(rec);
            if (index < 0) continue;
            DestroyCard(rec.Id);
            _turn.Remove(rec);
            if (trash) rec.DeletedTicks = DateTime.UtcNow.Ticks; else _records.Remove(rec);
            _undo.Add(new UndoItem { Record = rec, Index = index });
        }
        if (_undo.Count == 0) return;
        Persist();
        RebuildChatLayout();
        ShowUndo(_undo.Count == 1 ? "Сообщение удалено. Вернуть" : "Удалено сообщений: " + _undo.Count + ". Вернуть");
    }

    private void ShowUndo(string text)
    {
        if (_undoButton == null) return;
        _undoButton.transform.Find("Label").GetComponent<Text>().text = text;
        _undoButton.gameObject.SetActive(true);
        _undoButton.transform.SetAsLastSibling();
        if (_undoTimer != null) StopCoroutine(_undoTimer);
        _undoTimer = StartCoroutine(HideUndoLater());
    }

    private System.Collections.IEnumerator HideUndoLater()
    {
        yield return new WaitForSecondsRealtime(UndoSeconds);
        HideUndo(true);
    }

    private void HideUndo(bool forget)
    {
        if (_undoButton != null) _undoButton.gameObject.SetActive(false);
        _undoTimer = null;
        if (forget) _undo.Clear();
    }

    private void Revive(ChatRecord rec, int indexHint)
    {
        if (!_records.Contains(rec))
            _records.Insert(Mathf.Clamp(indexHint, 0, _records.Count), rec);
        rec.DeletedTicks = 0;
        if (!_cards.ContainsKey(rec.Id)) BuildCard(rec);
        PlaceCard(rec);
    }

    /// <summary>Вернуть то, что только что смахнули (или, если давно, последнее из корзины).</summary>
    public string RestoreLast()
    {
        int count = 0;
        if (_undo.Count > 0)
        {
            foreach (UndoItem item in _undo) { Revive(item.Record, item.Index); count++; }
            _undo.Clear();
        }
        else
        {
            ChatRecord last = null;
            foreach (ChatRecord rec in _records) if (rec.Deleted && (last == null || rec.DeletedTicks > last.DeletedTicks)) last = rec;
            if (last != null) { Revive(last, _records.IndexOf(last)); count = 1; }
        }
        HideUndo(false);
        if (count == 0) return "Возвращать нечего: корзина пуста.";
        Persist();
        ApplyFilter();
        return "Вернул сообщений: " + count + ".";
    }

    public string RestoreAll()
    {
        int count = 0;
        foreach (ChatRecord rec in new List<ChatRecord>(_records))
            if (rec.Deleted) { Revive(rec, _records.IndexOf(rec)); count++; }
        _undo.Clear();
        HideUndo(false);
        if (count == 0) return "Возвращать нечего: корзина пуста.";
        Persist();
        ApplyFilter();
        return "Вернул сообщений: " + count + ".";
    }

    public int TrashCount
    {
        get { int n = 0; foreach (ChatRecord rec in _records) if (rec.Deleted) n++; return n; }
    }

    public string EmptyTrash()
    {
        int n = _records.RemoveAll(rec => rec.Deleted);
        _undo.Clear();
        HideUndo(false);
        Persist();
        return n == 0 ? "Корзина и так пуста." : "Корзина очищена, удалено навсегда: " + n + ".";
    }

    // ---- команды (то же, что кнопки и смахивание, но голосом) ----

    /// <summary>Что произойдёт: для вопроса «да/нет» перед необратимой очисткой.</summary>
    public string DescribeChatCommand(ChatCommand c)
    {
        if (c.Cmd == ChatCmd.EmptyTrash) return "Очистить корзину (" + TrashCount + ")?";
        return "Очистить " + TargetName(c) + "?";
    }

    private string TargetName(ChatCommand c)
    {
        if (c.HasKind) return "раздел «" + ChatKinds.Title(c.Kind) + "»";
        if (c.Everything || _filterAll) return "весь чат";
        return "раздел «" + ChatKinds.Title(_filterKind) + "»";
    }

    public string RunChatCommand(ChatCommand c)
    {
        switch (c.Cmd)
        {
            case ChatCmd.Help:
                return "Чат. " + AppSettings.ChatKeepTitle(AppSettings.ChatHistory) + ". " + AppSettings.ChatTrashTitle(AppSettings.ChatTrash) +
                       ". В корзине: " + TrashCount + ".\nГолосом или текстом: «раздел телефон / заметки / телеграм / система / диалог / все», " +
                       "«удали последнее сообщение», «очисти чат», «верни сообщение», «верни удалённые», «очисти корзину», " +
                       "«храни историю неделю / всегда», «не храни историю», «смахнутое в корзину», «удалять насовсем». " +
                       "Сообщение можно смахнуть вправо.";

            case ChatCmd.Section:
                if (!_fullMode) SetMode(true);
                if (c.HasKind) { _filterAll = false; _filterKind = c.Kind; _unread[(int)c.Kind] = false; }
                else _filterAll = true;
                ApplyFilter();
                return c.HasKind ? "Раздел: " + ChatKinds.Title(c.Kind) : "Показываю все сообщения";

            case ChatCmd.DeleteLast:
            {
                var picked = new List<ChatRecord>();
                for (int i = _records.Count - 1; i >= 0 && picked.Count < c.Count; i--)
                    if (IsVisible(_records[i])) picked.Add(_records[i]);
                if (picked.Count == 0) return "Нечего удалять.";
                DeleteRecords(picked);
                return "Удалено: " + picked.Count + ". Вернуть: «верни сообщение».";
            }

            case ChatCmd.DeleteSection:
            {
                var picked = new List<ChatRecord>();
                foreach (ChatRecord rec in _records)
                {
                    if (rec.Deleted) continue;
                    bool match = c.HasKind ? rec.Kind == c.Kind : (c.Everything || _filterAll || rec.Kind == _filterKind);
                    if (match) picked.Add(rec);
                }
                if (picked.Count == 0) return "Здесь нечего очищать.";
                DeleteRecords(picked);
                return AppSettings.ChatTrash
                    ? "Очищено: " + picked.Count + ". Вернуть: «верни удалённые»."
                    : "Очищено: " + picked.Count + ".";
            }

            case ChatCmd.Restore: return RestoreLast();
            case ChatCmd.RestoreAll: return RestoreAll();
            case ChatCmd.EmptyTrash: return EmptyTrash();

            case ChatCmd.KeepNever: AppSettings.ChatHistory = ChatKeep.Never; Persist(); return "Историю чата больше не храню: после закрытия приложения она пропадёт.";
            case ChatCmd.KeepWeek: AppSettings.ChatHistory = ChatKeep.Week; Persist(); return "Храню историю чата неделю.";
            case ChatCmd.KeepForever: AppSettings.ChatHistory = ChatKeep.Forever; Persist(); return "Храню историю чата, пока вы сами не удалите.";
            case ChatCmd.TrashOn: AppSettings.ChatTrash = true; return "Смахнутые сообщения будут лежать в корзине неделю.";
            case ChatCmd.TrashOff: AppSettings.ChatTrash = false; return "Смахнутые сообщения буду удалять сразу и насовсем.";
        }
        return string.Empty;
    }

    /// <summary>Сообщение, пришедшее само (Telegram, напоминание): сразу в свой раздел.</summary>
    public void AddAgentMessageAs(ChatKind kind, string text) { AddMessage("BARABASHKA", text, false, true, false, kind); }

    public void AddIncomingMessage(ChatKind kind, string speaker, string text) { AddMessage(speaker, text, false, false, false, kind); }
}
