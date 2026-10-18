using System;
using System.Threading;
using Barabashka.Contracts;
using UnityEngine;

public class AgentManager : MonoBehaviour
{
    public string testInputMessage;

    /// <summary>Источник последнего запроса: голосовые запросы озвучиваются, текстовые - нет.</summary>
    public static InputSource LastInputSource { get; private set; }

    /// <summary>Пользователь попросил именно показать (покажи, выведи, отобрази): ответ не озвучивается.</summary>
    public static bool LastRequestShowOnly { get; private set; }

    private IBrain _brain;
    private AgentCommandRouter _router;
    private AgentUIManager _uiManagerCache;

    // Интерфейс может быть добавлен на объект уже после Awake (его создаёт AppBootstrap), поэтому ищем его при первом обращении.
    private AgentUIManager _uiManager
    {
        get
        {
            if (_uiManagerCache == null) _uiManagerCache = GetComponent<AgentUIManager>();
            return _uiManagerCache;
        }
    }
    private CancellationTokenSource _cts;
    private int _requestVersion;
    private bool _requestRunning;

    private void Awake()
    {
        _brain = new LegacyBrainAdapter(new IsolatedBrain());
        _router = GetComponent<AgentCommandRouter>();

        if (_router == null)
            Debug.LogError("[AgentManager] Компонент AgentCommandRouter не найден!");
    }

    private void Start()
    {
        Debug.Log("[AgentManager] Изолированное ядро и UI успешно инициализированы.");
    }

    private void OnDestroy()
    {
        if (_cts != null) _cts.Cancel();
    }

    /// <summary>
    /// Служебное сообщение пользователю (состояние голоса и т.п.): в строку статуса или в чат.
    /// Безопасно вызывать из главного потока.
    /// </summary>
    public void ShowSystemNotice(string text, bool inChat)
    {
        if (string.IsNullOrWhiteSpace(text) || _uiManager == null) return;
        _uiManager.SetStatus(text);
        if (inChat) _uiManager.AddSystemMessage(text);
    }

    /// <summary>Текстовый ввод из UI. Сохранён для совместимости.</summary>
    public void SendUserMessage(string message)
    {
        SendUtterance(new Utterance(message, null, true, InputSource.Text));
    }

    /// <summary>
    /// Handles voice recognition results.
    /// Partial results (IsFinal=false) are shown in status only.
    /// Final results (IsFinal=true) are processed through SendUtterance like text input.
    /// </summary>
    public void ShowRecognizedVoiceText(Utterance utterance)
    {
        if (utterance == null || string.IsNullOrWhiteSpace(utterance.Text))
            return;

        if (!utterance.IsFinal)
        {
            // Partial result: only update status, don't add to chat
            if (_uiManager != null)
                _uiManager.SetStatus("Слышу: " + utterance.Text);
            return;
        }

        // Final result: process through the same pipeline as text input
        SendUtterance(utterance);
    }
    public async void SendUtterance(Utterance utterance)
    {
        if (utterance == null || _brain == null || _router == null) return;

        if (!utterance.IsFinal)
        {
            if (_uiManager != null && !string.IsNullOrWhiteSpace(utterance.Text))
                _uiManager.SetStatus("Слышу: " + utterance.Text);
            return;
        }

        string message = utterance.Text;
        if (string.IsNullOrWhiteSpace(message)) return;

        LastInputSource = utterance.Source;
        LastRequestShowOnly = WantsDisplay(message);

        // Голосом «покажи…» в режиме аватара: сразу открываем интерфейс, иначе ответ не будет виден.
        if (LastRequestShowOnly && utterance.Source == InputSource.Voice && _uiManager != null)
            _uiManager.SetMode(true);

        // Управление режимом голосом или текстом: ответ только в чат, без озвучки.
        if (_router != null && !_router.HasPendingConfirmation && _uiManager != null)
        {
            if (WantsInterface(message))
            {
                _uiManager.AddUserMessage(message);
                _uiManager.SetMode(true);
                _uiManager.AddAgentMessage("Открываю интерфейс, можно писать.");
                AgentActivityBus.PublishResting();
                return;
            }
            if (WantsAvatarMode(message))
            {
                _uiManager.AddUserMessage(message);
                _uiManager.AddAgentMessage("Возвращаюсь к режиму аватара.");
                _uiManager.SetMode(false);
                AgentActivityBus.PublishResting();
                return;
            }
        }

        // «Стоп», «хватит», «отмена»: останавливаем озвучку и текущий запрос (если ждём подтверждения, «отмена» - ответ на вопрос).
        if (_router != null && !_router.HasPendingConfirmation && IsStopPhrase(message))
        {
            if (_uiManager != null) _uiManager.AddUserMessage(message);
            AgentVoiceControl.RequestStopSpeaking();
            if (_requestRunning) StopCurrentTask();
            else AgentActivityBus.PublishResting();
            return;
        }

        int requestVersion = ++_requestVersion;
        _requestRunning = true;

        if (_cts != null) _cts.Cancel();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        if (_uiManager != null)
        {
            _uiManager.AddUserMessage(message);
        }
        AgentActivityBus.Publish(AgentActivity.Thinking);

        Debug.Log("[User Input]: " + message);
        try
        {
            // Если агент ждёт подтверждения («Удалить задачу? да/нет»), эта фраза - ответ на вопрос.
            if (_router.TryHandlePending(message))
            {
                AgentActivityBus.PublishResting();
                return;
            }

            AgentResponse response = await _brain.RespondAsync(utterance, token);
            if (requestVersion != _requestVersion) return;

            // Роутер пока принимает строку: команды с префиксом COMMAND:, остальное - обычный ответ.
            string routed = string.IsNullOrEmpty(response.Command)
                ? response.Text
                : "COMMAND:" + response.Command;

            Debug.Log("[Brain Response]: " + routed);
            _router.RouteCommand(routed);
            AgentActivityBus.PublishResting();
        }
        catch (OperationCanceledException)
        {
            // Запрос отменён (Stop или новый запрос) - ничего не показываем.
        }
        catch (Exception ex)
        {
            if (requestVersion != _requestVersion) return;
            Debug.LogException(ex);
            if (_uiManager != null)
            {
                _uiManager.AddAgentMessage("Не удалось обработать запрос. Подробности записаны в журнале Unity.");
                _uiManager.SetStatus("Ошибка");
            }
            AgentActivityBus.PublishResting();
        }
        finally
        {
            if (requestVersion == _requestVersion) _requestRunning = false;
        }
    }

    private static string[] Words(string text)
    {
        return (text ?? string.Empty).ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool WantsDisplay(string text)
    {
        string lower = (text ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("покаж") || lower.Contains("выведи") || lower.Contains("выводи") ||
            lower.Contains("вывест") || lower.Contains("отобраз") || lower.Contains("на экран"))
            return true;

        // «Открой список задач / чат / интерфейс» - тоже показать (а «открой сайт» - это браузер, не сюда).
        return lower.Contains("откр") &&
               (lower.Contains("список") || lower.Contains("задач") || lower.Contains("интерфейс") || lower.Contains("чат"));
    }

    private static bool HasAny(string[] words, params string[] options)
    {
        foreach (string w in words)
            foreach (string o in options)
                if (w == o) return true;
        return false;
    }

    private static bool HasStem(string[] words, params string[] stems)
    {
        foreach (string w in words)
            foreach (string s in stems)
                if (w.StartsWith(s, StringComparison.Ordinal)) return true;
        return false;
    }

    // Просьба открыть полный интерфейс: «открой интерфейс», «дай писать», «я не могу говорить» (слова в любом порядке).
    private static bool WantsInterface(string text)
    {
        string[] words = Words(text);
        if (words.Length == 0 || words.Length > 10) return false;

        // Команды добавления задач не трогаем: «добавь задачу проверить интерфейс».
        if (HasStem(words, "добав", "запиш", "созда", "удал", "перемен")) return false;

        bool open = HasStem(words, "откр", "покаж", "включ", "дай", "верн", "хоч", "нужн", "надо");
        if ((HasStem(words, "интерфейс", "клавиатур") || HasAny(words, "чат", "чата", "чатик")) && open) return true;

        bool negation = HasAny(words, "не", "нельзя", "неудобно");
        bool speak = HasStem(words, "говор", "разговар", "голос");
        if (negation && speak) return true;

        bool write = HasAny(words, "писать", "написать", "печатать", "напечатать", "текстом", "набрать", "вводить");
        bool wish = HasAny(words, "дай", "дайте", "давай", "хочу", "могу", "хотел", "хотела", "нужно", "надо",
            "позволь", "возможность", "можно", "буду", "лучше");
        return write && wish;
    }

    // Просьба вернуться к аватару: «сверни интерфейс», «закрой чат», «режим аватара».
    private static bool WantsAvatarMode(string text)
    {
        string[] words = Words(text);
        if (words.Length == 0 || words.Length > 6) return false;

        bool close = HasStem(words, "сверн", "скро", "спряч", "закро");
        bool target = HasStem(words, "интерфейс", "чат", "окн", "экран");
        return (close && target) || (HasStem(words, "аватар") && HasAny(words, "режим", "вернись", "верни"));
    }

    private static bool IsStopPhrase(string text)
    {
        string[] words = Words(text);
        if (words.Length == 0 || words.Length > 3) return false;
        foreach (string w in words)
        {
            if (w == "стоп" || w == "стой" || w == "хватит" || w == "отмена" || w == "отмени" ||
                w == "прекрати" || w == "замолчи" || w == "остановись" || w == "остановить" || w == "молчи")
                return true;
        }
        return false;
    }

    public void StopCurrentTask()
    {
        if (!_requestRunning)
        {
            if (_uiManager != null) _uiManager.SetStatus("Нет активной задачи");
            return;
        }

        // Токен отменяется, но старый IsolatedBrain отмену не поддерживает, поэтому
        // результат дополнительно отбрасывается через номер версии запроса.
        if (_cts != null) _cts.Cancel();
        _requestVersion++;
        _requestRunning = false;
        AgentActivityBus.PublishResting();
        if (_uiManager != null)
        {
            _uiManager.AddAgentMessage("Остановлено.");
            _uiManager.SetStatus("Остановлено");
        }
        Debug.Log("[AgentManager] Current response invalidated by Stop.");
    }

    [ContextMenu("Send Test Message")]
    public void SendInspectorMessage()
    {
        if (!string.IsNullOrEmpty(testInputMessage))
            SendUserMessage(testInputMessage);
    }
}
