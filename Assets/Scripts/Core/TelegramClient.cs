using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;

/// <summary>
/// Telegram под личным аккаунтом человека через TDLib (официальная библиотека Telegram).
/// Вход: /телеграм настроить, войти, код, пароль. Агент может читать чаты и отправлять сообщения от имени человека
/// (отправка идёт по режиму действий и политике риска). Данные аккаунта хранит TDLib в папке приложения, ключ базы - в SecretStore.
/// </summary>
public sealed class TelegramClient
{
    public static readonly TelegramClient Instance = new TelegramClient();

    public const string KeyApiId = "tg.api_id";
    public const string KeyApiHash = "tg.api_hash";
    private const string KeyDb = "tg.dbkey";

    private readonly object _lock = new object();
    private readonly Dictionary<string, TaskCompletionSource<Dictionary<string, object>>> _pending =
        new Dictionary<string, TaskCompletionSource<Dictionary<string, object>>>();
    private readonly Dictionary<long, string> _titles = new Dictionary<long, string>();
    private readonly Dictionary<long, int> _unread = new Dictionary<long, int>();
    private readonly Dictionary<long, string> _users = new Dictionary<long, string>();
    private readonly Dictionary<long, string> _sendOutcome = new Dictionary<long, string>();

    private int _client;
    private Thread _thread;
    private volatile bool _running;
    private long _extra;
    private string _authState = string.Empty;
    private string _hint = string.Empty;
    private string _me = string.Empty;
    private string _error = string.Empty;
    private bool _unsupported;
    private string _paramsJson = string.Empty;   // готовится в главном потоке: из потока приёма Unity API (PlayerPrefs, пути) вызывать нельзя

    public string AuthState { get { lock (_lock) return _authState; } }
    public string Me { get { lock (_lock) return _me; } }
    public string LastError { get { lock (_lock) return _error; } }
    public bool IsReady { get { return AuthState == "ready"; } }
    public bool Unsupported { get { return _unsupported; } }

    public static bool HasCredentials
    {
        get { return SecretStore.Has(KeyApiId) && SecretStore.Has(KeyApiHash); }
    }

    /// <summary>Вход уже выполнялся: есть ключи и папка базы TDLib (сессия поднимется сама).</summary>
    public static bool HasSession
    {
        get { return HasCredentials && Directory.Exists(DbDir); }
    }

    private static string DbDir { get { return Path.Combine(Application.persistentDataPath, "tdlib"); } }

    // Если человек уже входил, при запуске на телефоне тихо поднимаем клиента: так агент видит новые сообщения.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoStart()
    {
        try
        {
            if (Application.platform != RuntimePlatform.Android) return;
            if (!HasCredentials || !Directory.Exists(DbDir)) return;
            string error;
            Instance.Start(out error);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Telegram] Автозапуск: " + ex.Message);
        }
    }

    public bool Start(out string error)
    {
        error = null;
        if (_running) return true;
        if (!HasCredentials)
        {
            error = "Сначала задайте ключи приложения: /телеграм настроить <api_id> <api_hash> (получить на my.telegram.org).";
            return false;
        }
        try
        {
            TdJson.td_execute("{\"@type\":\"setLogVerbosityLevel\",\"new_verbosity_level\":1}");
            _client = TdJson.td_create_client_id();
        }
        catch (Exception ex)
        {
            if (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException)
            {
                _unsupported = true;
                error = "Библиотека Telegram (tdjson) не найдена в приложении. Её нужно собрать и добавить в проект, см. docs/TELEGRAM.md.";
                return false;
            }
            error = "Не удалось запустить Telegram: " + ex.Message;
            return false;
        }

        _paramsJson = BuildParameters();
        _running = true;
        _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "tdlib-receive" };
        _thread.Start();
        TdJson.td_send(_client, "{\"@type\":\"getAuthorizationState\"}");
        return true;
    }

    private void ReceiveLoop()
    {
        while (_running)
        {
            try
            {
                string json = TdJson.ReadString(TdJson.td_receive(1.0));
                if (json != null) Handle(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Telegram] " + ex.Message);
            }
        }
    }

    // ---- запросы ----

    /// <summary>Отправляет запрос TDLib (JSON-объект) и ждёт ответ; при тайм-ауте возвращает объект типа error.</summary>
    public async Task<Dictionary<string, object>> RequestAsync(string body, int timeoutMs = 30000)
    {
        string id;
        var tcs = new TaskCompletionSource<Dictionary<string, object>>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            id = "r" + (++_extra).ToString();
            _pending[id] = tcs;
        }
        TdJson.td_send(_client, "{\"@extra\":\"" + id + "\"," + body.Substring(1));

        Task finished = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        if (finished == tcs.Task) return tcs.Task.Result;
        lock (_lock) _pending.Remove(id);
        return ErrorObject("Telegram не ответил вовремя");
    }

    private static Dictionary<string, object> ErrorObject(string message)
    {
        return new Dictionary<string, object> { { "@type", "error" }, { "code", 0L }, { "message", message } };
    }

    private static bool IsError(Dictionary<string, object> r, out string message)
    {
        message = null;
        if (r == null) { message = "нет ответа"; return true; }
        if (Json.Str(r, "@type") != "error") return false;
        message = Json.Str(r, "message");
        return true;
    }

    // ---- обработка ответов и обновлений (поток приёма) ----

    private void Handle(string json)
    {
        var obj = Json.Parse(json) as Dictionary<string, object>;
        if (obj == null) return;
        string type = Json.Str(obj, "@type");

        string extra = Json.Str(obj, "@extra");
        if (extra.Length > 0)
        {
            TaskCompletionSource<Dictionary<string, object>> tcs = null;
            lock (_lock)
            {
                if (_pending.TryGetValue(extra, out tcs)) _pending.Remove(extra);
            }
            if (tcs != null) tcs.TrySetResult(obj);
        }

        switch (type)
        {
            case "updateAuthorizationState":
                OnAuthState(Json.Obj(obj, "authorization_state"));
                break;
            case "updateNewChat":
            {
                Dictionary<string, object> chat = Json.Obj(obj, "chat");
                long id = Json.Long(chat, "id");
                lock (_lock)
                {
                    _titles[id] = Json.Str(chat, "title");
                    _unread[id] = (int)Json.Long(chat, "unread_count");
                }
                break;
            }
            case "updateChatTitle":
                lock (_lock) _titles[Json.Long(obj, "chat_id")] = Json.Str(obj, "title");
                break;
            case "updateChatReadInbox":
                lock (_lock) _unread[Json.Long(obj, "chat_id")] = (int)Json.Long(obj, "unread_count");
                break;
            case "updateMessageSendSucceeded":
                lock (_lock) _sendOutcome[Json.Long(obj, "old_message_id")] = "ok";
                break;
            case "updateMessageSendFailed":
                lock (_lock) _sendOutcome[Json.Long(obj, "old_message_id")] = "fail:" + Json.Str(obj, "error_message");
                break;
        }
    }

    private void OnAuthState(Dictionary<string, object> state)
    {
        string type = Json.Str(state, "@type");
        string newState;
        switch (type)
        {
            case "authorizationStateWaitTdlibParameters":
                newState = "wait_params";
                SendParameters();
                break;
            case "authorizationStateWaitPhoneNumber": newState = "wait_phone"; break;
            case "authorizationStateWaitCode": newState = "wait_code"; break;
            case "authorizationStateWaitPassword":
                newState = "wait_password";
                lock (_lock) _hint = Json.Str(state, "password_hint");
                break;
            case "authorizationStateReady":
                newState = "ready";
                OnReady();
                break;
            case "authorizationStateClosed":
                newState = "closed";
                _running = false;
                break;
            case "authorizationStateLoggingOut":
            case "authorizationStateClosing":
                newState = "closing";
                break;
            default:
                newState = "unsupported:" + type;   // почта, регистрация нового аккаунта, подтверждение на другом устройстве
                break;
        }
        lock (_lock) _authState = newState;
    }

    private void SendParameters()
    {
        TdJson.td_send(_client, _paramsJson);
    }

    // Параметры TDLib. Вызывается из главного потока (читает хранилище секретов, пути и сведения об устройстве).
    private static string BuildParameters()
    {
        string dbKey = SecretStore.Get(KeyDb);
        if (dbKey.Length == 0)
        {
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            dbKey = Convert.ToBase64String(bytes);
            SecretStore.Set(KeyDb, dbKey);
        }

        Directory.CreateDirectory(DbDir);
        long apiId;
        long.TryParse(SecretStore.Get(KeyApiId), out apiId);
        var sb = new StringBuilder();
        sb.Append("{\"@type\":\"setTdlibParameters\",\"use_test_dc\":false");
        sb.Append(",\"database_directory\":").Append(MiniJson.Quote(DbDir));
        sb.Append(",\"files_directory\":").Append(MiniJson.Quote(Path.Combine(DbDir, "files")));
        sb.Append(",\"database_encryption_key\":").Append(MiniJson.Quote(dbKey));
        sb.Append(",\"use_file_database\":false,\"use_chat_info_database\":true,\"use_message_database\":true,\"use_secret_chats\":false");
        sb.Append(",\"api_id\":").Append(apiId);
        sb.Append(",\"api_hash\":").Append(MiniJson.Quote(SecretStore.Get(KeyApiHash)));
        sb.Append(",\"system_language_code\":\"ru\",\"device_model\":").Append(MiniJson.Quote(SystemInfo.deviceModel));
        sb.Append(",\"system_version\":").Append(MiniJson.Quote(SystemInfo.operatingSystem));
        sb.Append(",\"application_version\":\"0.1\"}");
        return sb.ToString();
    }

    private async void OnReady()
    {
        try
        {
            Dictionary<string, object> me = await RequestAsync("{\"@type\":\"getMe\"}", 15000);
            string text;
            if (!IsError(me, out text))
                lock (_lock) _me = (Json.Str(me, "first_name") + " " + Json.Str(me, "last_name")).Trim();
            await RequestAsync("{\"@type\":\"loadChats\",\"chat_list\":{\"@type\":\"chatListMain\"},\"limit\":100}", 30000);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Telegram] " + ex.Message);
        }
    }

    // ---- вход ----

    private async Task<string> WaitStateAsync(Func<string, bool> done, int timeoutMs)
    {
        int waited = 0;
        while (waited < timeoutMs)
        {
            string state = AuthState;
            if (done(state)) return state;
            await Task.Delay(250);
            waited += 250;
        }
        return AuthState;
    }

    public string DescribeState()
    {
        string state = AuthState;
        switch (state)
        {
            case "ready": return "Telegram подключён" + (Me.Length > 0 ? ": " + Me : string.Empty) + ".";
            case "wait_phone": return "Введите номер телефона аккаунта Telegram: /телеграм войти +79001234567";
            case "wait_code": return "Telegram отправил код (в приложение Telegram или SMS). Введите: /телеграм код 12345";
            case "wait_password":
                return "Включена двухэтапная защита. Введите пароль: /телеграм пароль ВАШ_ПАРОЛЬ" + (_hint.Length > 0 ? " (подсказка: " + _hint + ")" : string.Empty) +
                       ". Пароль останется в истории чата, после ввода очистите её.";
            case "closing": return "Telegram закрывается…";
            case "closed": return "Telegram отключён.";
            case "": case "wait_params": return "Telegram запускается…";
            default: return "Telegram просит шаг, который пока не поддерживается (" + state.Replace("unsupported:", string.Empty) + ").";
        }
    }

    /// <summary>Запускает клиента и ждёт, пока он определит, что нужно для входа.</summary>
    public async Task<string> BeginAsync()
    {
        string error;
        if (!Start(out error)) return error;
        await WaitStateAsync(s => s.Length > 0 && s != "wait_params", 15000);
        return DescribeState();
    }

    public async Task<string> SubmitPhoneAsync(string phone)
    {
        string start = await BeginAsync();
        if (AuthState != "wait_phone") return start;
        Dictionary<string, object> r = await RequestAsync("{\"@type\":\"setAuthenticationPhoneNumber\",\"phone_number\":" + MiniJson.Quote(phone.Trim()) + "}");
        string error;
        if (IsError(r, out error)) return "Telegram не принял номер: " + error;
        await WaitStateAsync(s => s != "wait_phone", 20000);
        return DescribeState();
    }

    public async Task<string> SubmitCodeAsync(string code)
    {
        if (AuthState != "wait_code") return DescribeState();
        Dictionary<string, object> r = await RequestAsync("{\"@type\":\"checkAuthenticationCode\",\"code\":" + MiniJson.Quote(code.Trim()) + "}");
        string error;
        if (IsError(r, out error)) return "Код не принят: " + error;
        await WaitStateAsync(s => s != "wait_code", 20000);
        return DescribeState();
    }

    public async Task<string> SubmitPasswordAsync(string password)
    {
        if (AuthState != "wait_password") return DescribeState();
        Dictionary<string, object> r = await RequestAsync("{\"@type\":\"checkAuthenticationPassword\",\"password\":" + MiniJson.Quote(password) + "}");
        string error;
        if (IsError(r, out error)) return "Пароль не принят: " + error;
        await WaitStateAsync(s => s != "wait_password", 20000);
        return DescribeState();
    }

    public async Task<string> LogOutAsync()
    {
        if (!_running) return "Telegram не запущен.";
        await RequestAsync("{\"@type\":\"logOut\"}", 15000);
        SecretStore.Remove(KeyDb);
        return "Вышел из Telegram на этом телефоне.";
    }

    /// <summary>Подключение готово или поднимается из сохранённой сессии (ждёт до 15 секунд).</summary>
    public async Task<string> EnsureReadyAsync()
    {
        if (IsReady) return null;
        string error;
        if (!Start(out error)) return error;
        await WaitStateAsync(s => s == "ready" || s.StartsWith("wait_p", StringComparison.Ordinal) || s == "wait_code" || s.StartsWith("unsupported", StringComparison.Ordinal), 15000);
        return IsReady ? null : DescribeState();
    }

    // ---- чаты и сообщения ----

    /// <summary>Известные чаты как «контакты»: Name - название чата, Number - идентификатор чата. Для подбора имени по тем же правилам, что и у контактов.</summary>
    public List<ContactBook.Contact> ChatContacts()
    {
        var list = new List<ContactBook.Contact>();
        lock (_lock)
            foreach (KeyValuePair<long, string> pair in _titles)
                if (!string.IsNullOrEmpty(pair.Value)) list.Add(new ContactBook.Contact { Name = pair.Value, Number = pair.Key.ToString() });
        return list;
    }

    public string ChatTitle(long chatId)
    {
        lock (_lock)
        {
            string title;
            return _titles.TryGetValue(chatId, out title) ? title : string.Empty;
        }
    }

    /// <summary>Чаты с непрочитанными сообщениями, больше непрочитанных - раньше.</summary>
    public List<KeyValuePair<string, int>> UnreadChats(int max)
    {
        var list = new List<KeyValuePair<string, int>>();
        lock (_lock)
            foreach (KeyValuePair<long, int> pair in _unread)
            {
                string title;
                if (pair.Value > 0 && _titles.TryGetValue(pair.Key, out title)) list.Add(new KeyValuePair<string, int>(title, pair.Value));
            }
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        if (list.Count > max) list.RemoveRange(max, list.Count - max);
        return list;
    }

    public async Task<string> LastMessagesAsync(long chatId, int count)
    {
        Dictionary<string, object> r = await RequestAsync("{\"@type\":\"getChatHistory\",\"chat_id\":" + chatId + ",\"from_message_id\":0,\"offset\":0,\"limit\":" + count + ",\"only_local\":false}");
        string error;
        if (IsError(r, out error)) return "Не удалось получить сообщения: " + error;
        List<object> messages = Json.List(r, "messages");
        // TDLib при первом обращении может вернуть меньше сообщений, чем есть: пробуем ещё раз.
        if (messages == null || messages.Count < Math.Min(count, 2))
        {
            await Task.Delay(800);
            r = await RequestAsync("{\"@type\":\"getChatHistory\",\"chat_id\":" + chatId + ",\"from_message_id\":0,\"offset\":0,\"limit\":" + count + ",\"only_local\":false}");
            messages = Json.List(r, "messages");
        }
        if (messages == null || messages.Count == 0) return "В чате нет сообщений.";

        var sb = new StringBuilder();
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            object m = messages[i];
            string who = Json.Bool(m, "is_outgoing") ? "Я" : await SenderNameAsync(chatId, m);
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(who).Append(": ").Append(ContentText(Json.Obj(m, "content")));
        }
        return sb.ToString();
    }

    private async Task<string> SenderNameAsync(long chatId, object message)
    {
        if (chatId > 0) return ChatTitle(chatId);   // личный чат: собеседник - название чата
        Dictionary<string, object> sender = Json.Obj(message, "sender_id");
        long userId = Json.Long(sender, "user_id");
        if (userId == 0) return ChatTitle(chatId);
        lock (_lock)
        {
            string cached;
            if (_users.TryGetValue(userId, out cached)) return cached;
        }
        Dictionary<string, object> r = await RequestAsync("{\"@type\":\"getUser\",\"user_id\":" + userId + "}", 10000);
        string name = (Json.Str(r, "first_name") + " " + Json.Str(r, "last_name")).Trim();
        if (name.Length == 0) name = "кто-то";
        lock (_lock) _users[userId] = name;
        return name;
    }

    private static string ContentText(Dictionary<string, object> content)
    {
        string type = Json.Str(content, "@type");
        string text;
        switch (type)
        {
            case "messageText": text = Json.Str(Json.Obj(content, "text"), "text"); break;
            case "messagePhoto": text = "[фото] " + Json.Str(Json.Obj(content, "caption"), "text"); break;
            case "messageVideo": text = "[видео] " + Json.Str(Json.Obj(content, "caption"), "text"); break;
            case "messageVoiceNote": text = "[голосовое сообщение]"; break;
            case "messageDocument": text = "[файл] " + Json.Str(Json.Obj(content, "caption"), "text"); break;
            case "messageSticker": text = "[стикер]"; break;
            default: text = "[" + type.Replace("message", string.Empty) + "]"; break;
        }
        text = text.Trim();
        return text.Length > 300 ? text.Substring(0, 300) + "…" : text;
    }

    /// <summary>Отправляет текст и проверяет результат: ждёт подтверждения от Telegram (до 10 секунд).</summary>
    public async Task<string> SendTextAsync(long chatId, string text)
    {
        string body = "{\"@type\":\"sendMessage\",\"chat_id\":" + chatId +
                      ",\"input_message_content\":{\"@type\":\"inputMessageText\",\"text\":{\"@type\":\"formattedText\",\"text\":" + MiniJson.Quote(text) + "}}}";
        Dictionary<string, object> r = await RequestAsync(body);
        string error;
        if (IsError(r, out error)) return "Не удалось отправить: " + error;

        long messageId = Json.Long(r, "id");
        for (int waited = 0; waited < 10000; waited += 250)
        {
            string outcome;
            lock (_lock) _sendOutcome.TryGetValue(messageId, out outcome);
            if (outcome == "ok") return null;
            if (outcome != null) return "Telegram не доставил сообщение: " + outcome.Substring(5);
            await Task.Delay(250);
        }
        return "Отправил, но подтверждения от Telegram пока нет (возможно, плохая связь).";
    }
}

/// <summary>Telegram как подключение (для /подключения и проверки возможностей).</summary>
public sealed class TelegramConnector : IConnector
{
    public string Id { get { return "telegram"; } }
    public string Title { get { return "Telegram"; } }
    public AuthKind Auth { get { return AuthKind.UserLogin; } }

    public ConnectorState State
    {
        get
        {
            TelegramClient t = TelegramClient.Instance;
            if (t.IsReady) return ConnectorState.Connected;
            if (t.Unsupported) return ConnectorState.Error;
            return TelegramClient.HasSession ? ConnectorState.Connected : ConnectorState.NotConfigured;
        }
    }

    public string Note
    {
        get
        {
            TelegramClient t = TelegramClient.Instance;
            if (t.IsReady) return t.Me;
            if (TelegramClient.HasSession) return "подключается";
            if (t.Unsupported) return "нет библиотеки tdjson, см. docs/TELEGRAM.md";
            if (!TelegramClient.HasCredentials) return "начните: /телеграм настроить";
            return "вход: /телеграм войти";
        }
    }
}
