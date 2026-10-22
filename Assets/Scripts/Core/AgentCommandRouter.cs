using System;
using System.Threading;
using Barabashka.Contracts;
using UnityEngine;

public class AgentCommandRouter : MonoBehaviour
{
    private AgentUIManager _uiManager;
    private AvatarVisualizer _avatar;

    private void Awake()
    {
        _avatar = GetComponent<AvatarVisualizer>();
    }

    private void Start()
    {
        _uiManager = GetComponent<AgentUIManager>();
        _avatar = GetComponent<AvatarVisualizer>();
    }

    public void RouteCommand(string brainResponse)
    {
        if (string.IsNullOrEmpty(brainResponse))
        {
            PlayAnimation("Idle");
            return;
        }

        if (brainResponse.StartsWith("COMMAND:", StringComparison.Ordinal))
        {
            string command = brainResponse.Substring("COMMAND:".Length);
            if (command == "TIME")
            {
                ShowResponse("Текущее точное время: " + AgentToolset.GetCurrentSystemTime());
                PlayAnimation("LookUp");
            }
            else if (command.StartsWith("OPEN_URL|", StringComparison.Ordinal))
            {
                string url = command.Substring("OPEN_URL|".Length);
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri parsed) &&
                    (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
                {
                    AgentToolset.OpenWebBrowser(parsed.AbsoluteUri);
                    ShowResponse("Открываю браузер: " + parsed.Host);
                    PlayAnimation("Greet");
                }
                else ShowResponse("Не удалось открыть ссылку: допустимы только HTTP и HTTPS.");
            }
            else if (command == "DATE")
                ShowResponse(AgentDates.Today(DateTime.Now));
            else if (command.StartsWith("TASK_ADD|", StringComparison.Ordinal))
            {
                // TASK_ADD|<название>[|<срок yyyy-MM-dd>]
                string[] parts = command.Substring("TASK_ADD|".Length).Split('|');
                ShowResponse(AgentToolset.AddTask(parts[0], parts.Length > 1 ? parts[1] : null));
            }
            else if (command == "TASK_LIST")
                ShowResponse(AgentToolset.ListTasks());
            else if (command.StartsWith("TASK_DONE|", StringComparison.Ordinal))
                ShowResponse(AgentToolset.CompleteTask(command.Substring("TASK_DONE|".Length)));
            else if (command.StartsWith("TASK_RENAME|", StringComparison.Ordinal))
            {
                // TASK_RENAME|<номер>|<новое название>
                string[] parts = command.Substring("TASK_RENAME|".Length).Split('|');
                ShowResponse(parts.Length < 2
                    ? "Не понял, как переименовать задачу."
                    : AgentToolset.RenameTask(parts[0], parts[1]));
            }
            else if (command.StartsWith("TASK_DELETE|", StringComparison.Ordinal))
                AskToConfirmDelete(command.Substring("TASK_DELETE|".Length));
            else if (command.StartsWith("TOOL|", StringComparison.Ordinal))
                RunRegisteredTool(command.Substring("TOOL|".Length));
            else
                ShowResponse("Команда не поддерживается: " + command);

            PlayAnimation("Idle");
            return;
        }

        ShowResponse(brainResponse);
        if (brainResponse.IndexOf("привет", StringComparison.OrdinalIgnoreCase) >= 0 ||
            brainResponse.IndexOf("рад помочь", StringComparison.OrdinalIgnoreCase) >= 0)
            PlayAnimation("Greet");
        else if (brainResponse.IndexOf("погода", StringComparison.OrdinalIgnoreCase) >= 0)
            PlayAnimation("LookUp");
        else
            PlayAnimation("Idle");
    }

    // A06: удаление необратимо, поэтому перед ним агент просит подтверждение.
    private const float ConfirmTimeoutSec = 30f;
    private string _pendingDeleteIndex;
    private float _pendingUntil;

    /// <summary>Агент ждёт ответа «да»/«нет» на вопрос об удалении (и ожидание ещё не истекло).</summary>
    public bool HasPendingConfirmation
    {
        get { return (_pendingDeleteIndex != null && Time.unscaledTime <= _pendingUntil) || ActionGate.HasPending; }
    }

    private void AskToConfirmDelete(string index)
    {
        string what = AgentToolset.DescribeTasks(index);
        if (what == null)
        {
            // Номера нет: пусть инструмент сам объяснит ошибку.
            ShowResponse(AgentToolset.DeleteTask(index));
            return;
        }

        Debug.Log("[Confirm] Жду подтверждения удаления: " + index + " (" + what + ")");
        _pendingDeleteIndex = index;
        _pendingUntil = Time.unscaledTime + ConfirmTimeoutSec;
        // Просим ответ чуть длиннее, чем «да»: так распознавание надёжнее.
        ShowResponse("Удалить " + what + "? Скажите «да, удалить» или «нет, отмена».");
    }

    /// <summary>
    /// Если агент ждёт подтверждения, разбирает ответ «да»/«нет». Возвращает true, когда фраза
    /// была ответом на вопрос (и дальше её обрабатывать не нужно). Любая другая фраза отменяет ожидание.
    /// </summary>
    public bool TryHandlePending(string userText)
    {
        if (ActionGate.HasPending) return HandleActionGate(userText);
        if (_pendingDeleteIndex == null) return false;

        Debug.Log("[Confirm] Получен ответ: «" + userText + "»");
        string index = _pendingDeleteIndex;
        bool expired = Time.unscaledTime > _pendingUntil;
        _pendingDeleteIndex = null;
        if (expired) return false;

        string[] words = (userText ?? string.Empty).ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);

        // Длинная фраза - это новая команда, а не короткое «да»/«нет».
        if (words.Length == 0 || words.Length > 4) return false;

        bool no = false;
        bool yes = false;
        foreach (string w in words)
        {
            if (w == "нет" || w == "не" || w == "отмена" || w == "отмени" || w == "стоп" || w == "отбой") no = true;
            if (w == "да" || w == "ага" || w == "угу" || w == "конечно" || w == "давай" ||
                w == "подтверждаю" || w == "удаляй" || w == "удалить" || w == "ок" || w == "окей" ||
                w == "верно" || w == "точно" || w == "хорошо") yes = true;
        }

        if (no)
        {
            ShowResponse("Хорошо, ничего не удаляю.");
            return true;
        }
        if (yes)
        {
            Debug.Log("[Confirm] Подтверждено, удаляю: " + index);
            ShowResponse(AgentToolset.DeleteTask(index));
            return true;
        }

        // Ответ не похож на «да»/«нет»: считаем, что пользователь передумал, и разбираем фразу как обычную.
        return false;
    }

    // Ответ на вопрос перед звонком/сообщением (режим «С подтверждением»).
    private bool HandleActionGate(string userText)
    {
        Func<System.Threading.Tasks.Task<string>> action = ActionGate.Take();
        if (action == null) return false;

        string[] words = (userText ?? string.Empty).ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Length > 4) return false;

        bool no = false, yes = false;
        foreach (string w in words)
        {
            if (w == "нет" || w == "не" || w == "отмена" || w == "отмени" || w == "стоп" || w == "отбой") no = true;
            if (w == "да" || w == "ага" || w == "угу" || w == "конечно" || w == "давай" || w == "подтверждаю" ||
                w == "делай" || w == "сделай" || w == "ок" || w == "окей" || w == "верно" || w == "звони" || w == "отправляй") yes = true;
        }

        if (no) { ShowResponse("Хорошо, не делаю."); return true; }
        if (!yes) return false;
        RunConfirmedAction(action);
        return true;
    }

    private async void RunConfirmedAction(Func<System.Threading.Tasks.Task<string>> action)
    {
        try { ShowResponse(await action()); }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ShowResponse("Не получилось выполнить действие: " + ex.Message);
        }
    }

    // Инструменты нового образца: «TOOL|имя|ключ=значение&ключ=значение».
    private async void RunRegisteredTool(string spec)
    {
        string[] parts = spec.Split(new[] { '|' }, 2);
        IAgentTool tool;
        if (!AgentToolRegistry.Default.TryGet(parts[0], out tool))
        {
            ShowResponse("Не нашёл такой возможности: " + parts[0]);
            return;
        }

        try
        {
            var args = AgentToolRegistry.ParseQuery(parts.Length > 1 ? parts[1] : string.Empty);
            ToolResult result = await tool.ExecuteAsync(args, CancellationToken.None);
            ShowResponse(result.Success ? result.Output : result.Error);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ShowResponse("Не получилось выполнить действие. Подробности записаны в журнале Unity.");
        }
    }

    /// <summary>Сообщение «по инициативе агента» (напоминание, таймер): показывается всегда, озвучивается по желанию.</summary>
    public static event Action<string> AnnouncementShown;

    public void Announce(string text, bool speak)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Debug.Log("[Router] Напоминание: " + text);

        if (_uiManager == null) _uiManager = GetComponent<AgentUIManager>();
        if (_uiManager != null) _uiManager.UpdateResponseText(text);
        PlayAnimation("Greet");

        if (!speak) return;
        try { AnnouncementShown?.Invoke(text); }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    /// <summary>Вызывается каждый раз, когда агент показал ответ пользователю (для озвучки и т.п.).</summary>
    public static event Action<string> ResponseShown;

    private void ShowResponse(string text)
    {
        Debug.Log("[Router] Ответ агента: " + (text == null ? 0 : text.Length) + " симв.");
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { ResponseShown?.Invoke(text); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        if (_uiManager == null) _uiManager = GetComponent<AgentUIManager>();
        if (_uiManager != null) _uiManager.UpdateResponseText(text);
        else Debug.LogWarning("[AgentCommandRouter] AgentUIManager is unavailable. Response: " + text);
    }

    private void PlayAnimation(string animName)
    {
        if (_avatar != null)
        {
            switch (animName)
            {
                case "LookUp": _avatar.TriggerLookUp(); break;
                case "Greet": _avatar.TriggerGreet(); break;
                case "Idle": _avatar.TriggerIdle(); break;
            }
        }
        Debug.Log("[Avatar] Play animation: " + animName);
    }
}
