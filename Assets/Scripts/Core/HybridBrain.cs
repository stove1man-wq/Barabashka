using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using Debug = UnityEngine.Debug;

/// <summary>
/// Двухступенчатый мозг: сначала быстрые правила (IsolatedBrain), и только если они фразу не поняли,
/// вопрос уходит языковой модели (Ollama), которая либо вызывает инструмент из AgentToolCatalog, либо отвечает текстом.
/// Если модель выключена или недоступна, работает как раньше: правила отвечают сами.
/// Команды для настройки модели вводятся в чате: «/llm».
/// </summary>
public sealed class HybridBrain : IBrain
{
    private const int MaxHistoryMessages = 8;
    private const int MaxReplyChars = 600;

    private readonly IBrain _rules;
    private readonly LlmClient _llm;
    private readonly List<KeyValuePair<string, string>> _history = new List<KeyValuePair<string, string>>();
    private bool _warnedUnavailable;

    public HybridBrain(IBrain rules, LlmClient llm)
    {
        if (rules == null) throw new ArgumentNullException("rules");
        if (llm == null) throw new ArgumentNullException("llm");
        _rules = rules;
        _llm = llm;
    }

    public string Id { get { return "hybrid"; } }
    public bool RequiresNetwork { get { return false; } }

    public async Task<AgentResponse> RespondAsync(Utterance input, CancellationToken ct)
    {
        string text = input != null && input.Text != null ? input.Text.Trim() : string.Empty;

        if (text.StartsWith("/llm", StringComparison.OrdinalIgnoreCase))
            return await HandleSettingsCommandAsync(text, input, ct);

        if (text.StartsWith("/действия", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/actions", StringComparison.OrdinalIgnoreCase))
        {
            string[] p = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string arg = p.Length > 1 ? p[1].ToLowerInvariant() : string.Empty;
            string answer;
            if (arg.StartsWith("ручн", StringComparison.Ordinal) || arg == "manual") { AppSettings.Actions = ActionLevel.Manual; answer = "Режим действий: " + AppSettings.ActionsTitle(ActionLevel.Manual) + "."; }
            else if (arg.StartsWith("голос", StringComparison.Ordinal) || arg.StartsWith("подтвер", StringComparison.Ordinal) || arg == "confirm") { AppSettings.Actions = ActionLevel.Confirm; answer = "Режим действий: " + AppSettings.ActionsTitle(ActionLevel.Confirm) + "."; }
            else if (arg.StartsWith("авто", StringComparison.Ordinal) || arg.StartsWith("полн", StringComparison.Ordinal) || arg == "auto") { AppSettings.Actions = ActionLevel.Auto; answer = "Режим действий: " + AppSettings.ActionsTitle(ActionLevel.Auto) + "."; }
            else answer = "Режим действий: " + AppSettings.ActionsTitle(AppSettings.Actions) +
                          ".\nСменить: /действия ручной | голос | авто";
            return new AgentResponse { Text = answer, LanguageCode = input != null ? input.LanguageCode : null };
        }

        if (text.StartsWith("/возможности", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/capabilities", StringComparison.OrdinalIgnoreCase))
        {
            string[] p = text.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            string verb = p.Length > 1 ? p[1].ToLowerInvariant() : string.Empty;
            string rest = p.Length > 2 ? p[2] : string.Empty;
            string answer;
            if (verb.StartsWith("выкл", StringComparison.Ordinal)) answer = CapabilityRegistry.Toggle(rest, false);
            else if (verb.StartsWith("вкл", StringComparison.Ordinal)) answer = CapabilityRegistry.Toggle(rest, true);
            else answer = CapabilityRegistry.Describe();
            return new AgentResponse { Text = answer, LanguageCode = input != null ? input.LanguageCode : null };
        }

        if (text.StartsWith("/телеграм", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/telegram", StringComparison.OrdinalIgnoreCase))
        {
            int sp = text.IndexOf(' ');
            string tgAnswer = await TelegramCommands.HandleAsync(sp < 0 ? string.Empty : text.Substring(sp + 1));
            return new AgentResponse { Text = tgAnswer, LanguageCode = input != null ? input.LanguageCode : null };
        }

        if (text.StartsWith("/подключения", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/connections", StringComparison.OrdinalIgnoreCase))
            return new AgentResponse { Text = ConnectorRegistry.Describe(), LanguageCode = input != null ? input.LanguageCode : null };

        if (text.StartsWith("/политика", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/policy", StringComparison.OrdinalIgnoreCase))
        {
            int space = text.IndexOf(' ');
            return new AgentResponse { Text = ActionPolicy.Handle(space < 0 ? string.Empty : text.Substring(space + 1)), LanguageCode = input != null ? input.LanguageCode : null };
        }

        if (text.StartsWith("/журнал", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/log", StringComparison.OrdinalIgnoreCase))
        {
            bool clearLog = text.IndexOf("очист", StringComparison.OrdinalIgnoreCase) >= 0;
            if (clearLog) ActionLog.Clear();
            return new AgentResponse { Text = clearLog ? "Журнал действий очищен." : ActionLog.Describe(20), LanguageCode = input != null ? input.LanguageCode : null };
        }

        if (text.StartsWith("/непонятое", StringComparison.OrdinalIgnoreCase))
        {
            bool clear = text.IndexOf("очист", StringComparison.OrdinalIgnoreCase) >= 0;
            if (clear) UnknownLog.Clear();
            return new AgentResponse
            {
                Text = clear ? "Журнал непонятых фраз очищен." : UnknownLog.Describe(30),
                LanguageCode = input != null ? input.LanguageCode : null
            };
        }

        AgentResponse ruleResponse = await _rules.RespondAsync(input, ct);
        if (!IsUnknown(ruleResponse))
        {
            Remember(text, string.IsNullOrEmpty(ruleResponse.Text) ? "Готово." : ruleResponse.Text);
            return ruleResponse;
        }

        // Для разбора: запоминаем фразу, которую правила не поняли (кроме коротких обрывков речи).
        if (CountLetters(text) >= 3) UnknownLog.Add(text);

        // Правила не поняли. Слишком короткие обрывки (шум распознавания) модели не отдаём.
        if (!LlmSettings.Enabled || CountLetters(text) < 3) return ruleResponse;

        try
        {
            string content = await _llm.ChatAsync(AgentToolCatalog.BuildSystemPrompt(DateTime.Now), _history, text, true, ct,
                AgentToolCatalog.BuildCompactPrompt(DateTime.Now));
            _warnedUnavailable = false;
            Debug.Log("[LLM] Ответ модели: " + content);

            bool implausible;
            AgentResponse answer = Interpret(content, input, text, out implausible);
            if (implausible)
            {
                // Модель вызвала инструмент, о котором в просьбе нет ни слова (маленькие модели так ошибаются).
                // Повторяем вопрос с пометкой «инструменты не нужны»; начало запроса то же, поэтому кэш модели не теряется.
                Debug.LogWarning("[LLM] Вызов инструмента не подходит к просьбе, переспрашиваю как обычный вопрос.");
                string again = await _llm.ChatAsync(AgentToolCatalog.BuildSystemPrompt(DateTime.Now), _history,
                    text + "\n[Инструменты не нужны: ответь только {\"reply\":\"ответ по-русски\"}]", true, ct,
                    AgentToolCatalog.BuildCompactPrompt(DateTime.Now));
                Debug.Log("[LLM] Повторный ответ модели: " + again);
                Dictionary<string, string> retry = MiniJson.ParseFlatObject(again);
                string retryText = Clean(retry != null ? MiniJson.Get(retry, "reply") : again);
                answer = retryText.Length == 0 ? null
                    : new AgentResponse { Text = retryText, LanguageCode = input != null ? input.LanguageCode : null };
            }
            if (answer == null) return ruleResponse;

            Remember(text, answer.Command != null ? "Выполнено." : answer.Text);
            return answer;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LlmException ex)
        {
            Debug.LogWarning("[LLM] " + ex.Message);
            // Подробность показываем один раз подряд, дальше - коротко, чтобы не повторять одно и то же.
            string note = _warnedUnavailable
                ? "Не понял запрос, а языковая модель сейчас недоступна."
                : "Не понял запрос, а языковая модель недоступна. " + ex.Message;
            _warnedUnavailable = true;
            return new AgentResponse { Text = note, LanguageCode = input != null ? input.LanguageCode : null };
        }
    }

    // Правила сами сообщают, что не поняли фразу, стандартным ответом с этим началом.
    private static bool IsUnknown(AgentResponse response)
    {
        return response != null && string.IsNullOrEmpty(response.Command) && response.Text != null &&
               response.Text.StartsWith(IsolatedBrain.UnknownReplyPrefix, StringComparison.Ordinal);
    }

    private static AgentResponse Interpret(string content, Utterance input, string userText, out bool implausible)
    {
        implausible = false;
        string language = input != null ? input.LanguageCode : null;
        Dictionary<string, string> fields = MiniJson.ParseFlatObject(content);

        if (fields == null)
        {
            // Модель ответила обычным текстом, а не JSON: принимаем как реплику.
            string plain = Clean(content);
            return plain.Length == 0 ? null : new AgentResponse { Text = plain, LanguageCode = language };
        }

        string tool = MiniJson.Get(fields, "tool");
        if (tool.Length > 0 && !tool.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            if (!ToolMatchesRequest(tool, userText))
            {
                string own = Clean(MiniJson.Get(fields, "reply"));
                if (own.Length > 0) return new AgentResponse { Text = own, LanguageCode = language };
                implausible = true;
                return null;
            }

            string command, error;
            if (AgentToolCatalog.TryBuildCommand(fields, out command, out error))
                return new AgentResponse { Command = command, LanguageCode = language };

            Debug.LogWarning("[LLM] Некорректный вызов инструмента: " + error);
            string fallbackReply = Clean(MiniJson.Get(fields, "reply"));
            if (fallbackReply.Length > 0) return new AgentResponse { Text = fallbackReply, LanguageCode = language };
            return new AgentResponse { Text = "Не получилось выполнить: " + error + ". Попробуйте сказать иначе.", LanguageCode = language };
        }

        string reply = Clean(MiniJson.Get(fields, "reply"));
        if (reply.Length == 0) reply = Clean(MiniJson.Get(fields, "text"));
        return reply.Length == 0 ? null : new AgentResponse { Text = reply, LanguageCode = language };
    }

    // Страховка от выдуманных вызовов: инструмент допускается, только если в просьбе есть слово из его области.
    private static readonly Dictionary<string, string[]> ToolWords = new Dictionary<string, string[]>
    {
        { "time", new[] { "врем", "час", "котор" } },
        { "date", new[] { "дат", "числ", "день", "какой", "сегодня", "завтра" } },
        { "task_add", new[] { "задач", "добав", "запиш", "запис", "внес", "занес", "поставь", "надо", "нужно", "забуд", "купи", "сделат", "дела" } },
        { "task_list", new[] { "задач", "дела", "список", "покаж", "выведи", "что " } },
        { "task_done", new[] { "задач", "готов", "выполн", "сдела", "отмет", "заверш" } },
        { "task_delete", new[] { "задач", "удал", "убер", "сотр", "очист" } },
        { "task_rename", new[] { "задач", "переимен", "назов", "измен", "поменя" } },
        { "open_url", new[] { "откр", "запус", "зайд", "зайт", "перейд", "включ", "загруз", "сайт", "страниц", "ссылк", "браузер", ".ru", ".com" } },
        { "note_add", new[] { "запомн", "запиш", "запис", "заметк" } },
        { "note_list", new[] { "заметк", "запис", "запомн", "найд", "покаж" } },
        { "note_delete", new[] { "заметк", "удал", "убер", "сотр" } },
        { "reminder_add", new[] { "напомн", "таймер", "через", "разбуд", "будильник", "в " } },
        { "reminder_list", new[] { "напомин", "таймер", "покаж", "список", "активн" } },
        { "reminder_delete", new[] { "напомин", "таймер", "отмен", "удал", "убер", "сотр" } },
        { "weather", new[] { "погод", "температур", "дожд", "холод", "тепл", "снег", "ветер", "градус", "зонт", "куртк", "жарко" } },
        { "set_city", new[] { "город", "живу", "по умолчанию", "запомн" } },
        { "flashlight", new[] { "фонар", "свет", "посвет" } },
        { "volume", new[] { "громк", "звук", "тише", "громче" } },
        { "battery", new[] { "заряд", "батаре", "аккумул" } },
        { "web_search", new[] { "найд", "поищ", "загугл", "погугл", "интернет", "поиск" } },
        { "open_site", new[] { "откр", "запус", "зайд", "перейд", "включ", "ютуб", "сайт" } },
        { "route", new[] { "маршрут", "карт", "дорог", "проложи", "навигац", "доехат", "добрат" } },
        { "dial", new[] { "позвон", "набер", "номер", "звонок", "вызов" } },
        { "phone_settings", new[] { "настройк", "вайфай", "wi", "блютуз", "bluetooth" } },
        { "open_app", new[] { "откр", "запус", "включ", "зайд", "приложен" } },
        { "alarm_set", new[] { "будильн", "разбуд" } },
        { "alarm_show", new[] { "будильн" } },
        { "telegram_read", new[] { "телеграм", "telegram", "переписк", "сообщен", "непрочит", "нов" } },
        { "call", new[] { "позвон", "звони", "набер", "вызов", "дозвон" } },
        { "message", new[] { "напиш", "отправ", "смс", "сообщен", "телеграм", "ватсап", "вацап", "вотсап", "пошл", "скин" } }
    };

    private static bool ToolMatchesRequest(string tool, string userText)
    {
        string[] words;
        if (!ToolWords.TryGetValue(tool.ToLowerInvariant(), out words)) return true;
        string lower = (userText ?? string.Empty).ToLowerInvariant();
        foreach (string w in words)
            if (lower.IndexOf(w, StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    // Убираем разметку, которую модели любят добавлять, и ограничиваем длину (ответ могут озвучивать).
    private static string Clean(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string text = value.Replace("**", string.Empty).Replace("`", string.Empty).Trim();
        if (text.Length > MaxReplyChars) text = text.Substring(0, MaxReplyChars).TrimEnd() + "…";
        return text;
    }

    private void Remember(string user, string assistant)
    {
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(assistant)) return;
        _history.Add(new KeyValuePair<string, string>("user", user));
        _history.Add(new KeyValuePair<string, string>("assistant", assistant));
        while (_history.Count > MaxHistoryMessages) _history.RemoveAt(0);
    }

    private static int CountLetters(string text)
    {
        int n = 0;
        foreach (char c in text) if (char.IsLetter(c)) n++;
        return n;
    }

    // ---- Настройка в чате: /llm, /llm вкл, /llm выкл, /llm адрес …, /llm модель …, /llm проверка ----

    private async Task<AgentResponse> HandleSettingsCommandAsync(string text, Utterance input, CancellationToken ct)
    {
        string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string action = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;
        string reply;

        switch (action)
        {
            case "вкл":
            case "on":
                LlmSettings.Enabled = true;
                reply = "Языковая модель включена.";
                break;
            case "выкл":
            case "off":
                LlmSettings.Enabled = false;
                reply = "Языковая модель выключена, отвечают только правила.";
                break;
            case "телефон":
            case "device":
                reply = HandleDeviceCommand(parts);
                break;
            case "адрес":
            case "url":
                reply = SetOllamaUrl(parts.Length > 2 ? parts[2] : string.Empty);
                break;
            case "модель":
            case "model":
                if (parts.Length < 3) { reply = "Укажите имя модели Ollama: /llm модель llama3.1:8b"; break; }
                LlmSettings.Model = parts[2];
                reply = "Модель Ollama: " + LlmSettings.Model;
                break;
            case "профили":
            case "список":
                reply = DescribeProfiles();
                break;
            case "добавить":
                reply = AddProfile(parts);
                break;
            case "ключ":
                if (parts.Length < 4) { reply = "Формат: /llm ключ имя_профиля КЛЮЧ"; break; }
                reply = LlmProfiles.SetKey(parts[2], parts[3])
                    ? "Ключ для «" + parts[2] + "» сохранён."
                    : "Профиль «" + parts[2] + "» не найден. Список: /llm профили";
                break;
            case "удалить":
                if (parts.Length < 3) { reply = "Формат: /llm удалить имя_профиля"; break; }
                reply = LlmProfiles.Remove(parts[2])
                    ? "Профиль «" + parts[2] + "» удалён."
                    : "Профиль «" + parts[2] + "» не найден. Список: /llm профили";
                break;
            case "проверка":
            case "test":
                reply = await TestAsync(ct);
                break;
            default:
                reply = "Языковая модель: " + (LlmSettings.Enabled ? "включена" : "выключена") + "\n" + DescribeProfiles() +
                        "\nКоманды: /llm вкл | выкл | адрес … | модель … | профили | добавить … | ключ … | удалить … | проверка" +
                        "\nОблако: /llm добавить openrouter КЛЮЧ (или groq КЛЮЧ)" +
                        "\nБез сети и компьютера: /llm телефон (скачает модель около 1 ГБ в память телефона)";
                break;
        }

        // Настройки изменились: прежние пометки «не отвечает» больше не нужны.
        if (action != "проверка" && action != "test") _llm.ResetBlocks();
        return new AgentResponse { Text = reply, LanguageCode = input != null ? input.LanguageCode : null };
    }

    private static string SetOllamaUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Укажите адрес: /llm адрес http://192.168.0.5:11434";
        if (value.IndexOf("://", StringComparison.Ordinal) < 0) value = "http://" + value;
        Uri uri;
        if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return "Не похоже на адрес. Пример: /llm адрес http://192.168.0.5:11434";
        LlmSettings.Url = value;
        return "Адрес Ollama: " + LlmSettings.Url;
    }

    // /llm телефон [ссылка на .gguf]   |   /llm телефон удалить
    private static string HandleDeviceCommand(string[] parts)
    {
        string arg = parts.Length > 2 ? parts[2] : string.Empty;
        if (arg.Equals("удалить", StringComparison.OrdinalIgnoreCase) || arg.Equals("delete", StringComparison.OrdinalIgnoreCase))
            return LocalModel.Remove();

        if (LocalLlmHost.Current == null)
            return "Для модели в телефоне нужен пакет LLMUnity: в Unity Window - Package Manager - Add package from git URL - https://github.com/undreamai/LLMUnity.git, затем пересоберите приложение.";

        if (LocalModel.IsDownloading) return "Модель уже скачивается: " + LocalModel.DescribeDownload();

        if (arg.Length == 0 && LocalModel.Exists(LocalModel.DefaultPath))
        {
            LlmProfiles.AddOrReplace(new LlmProfile { name = LocalModel.ProfileName, kind = "device", url = string.Empty, model = LocalModel.DefaultPath, apiKey = string.Empty });
            return "Модель в телефоне подключена (файл уже скачан). Проверка: /llm проверка";
        }

        if (arg.Length > 0 && !arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "Ссылка на файл модели должна начинаться с https:// и вести на .gguf. Без ссылки скачивается модель по умолчанию.";

        LocalModel.StartDownload(arg);
        return "Начинаю скачивать модель (около 1 ГБ), лучше по Wi-Fi и пока приложение открыто. Когда закончу, скажу. Ход: /llm профили";
    }

    private string DescribeProfiles()
    {
        var sb = new StringBuilder("Порядок обращения к моделям:");
        int n = 0;
        if (LlmSettings.OllamaUsable)
        {
            n++;
            sb.Append('\n').Append(n).Append(". ollama (Ollama): ").Append(LlmSettings.Url).Append(", ").Append(LlmSettings.Model)
              .Append(_llm.IsBlocked("ollama") ? " [не отвечает]" : string.Empty);
        }
        else
        {
            sb.Append("\n- ollama не используется: адрес не задан (/llm адрес …)");
        }
        foreach (LlmProfile p in LlmProfiles.Chain())
        {
            if (p.name == LlmProfiles.OllamaName) continue;
            n++;
            if (p.IsDevice)
            {
                sb.Append('\n').Append(n).Append(". device (модель в телефоне, работает без сети): ").Append(LocalModel.SizeBytes(p.model) / (1024 * 1024)).Append(" МБ");
                if (LocalLlmHost.Current == null) sb.Append(" [нет пакета LLMUnity]");
                else sb.Append(", ").Append(LocalLlmHost.Current.Status);
                if (_llm.IsBlocked(p.name)) sb.Append(" [не отвечает]");
                continue;
            }
            sb.Append('\n').Append(n).Append(". ").Append(p.name).Append(p.IsOpenAi ? " (OpenAI-совместимый): " : " (Ollama): ")
              .Append(p.url).Append(", ").Append(p.model);
            if (p.IsOpenAi) sb.Append(", ").Append(LlmProfiles.MaskKey(p.apiKey));
            if (_llm.IsBlocked(p.name)) sb.Append(" [не отвечает]");
        }
        if (LocalModel.IsDownloading) sb.Append("\n- модель для телефона ").Append(LocalModel.DescribeDownload());
        if (n == 0) sb.Append("\n(пусто)");
        return sb.ToString();
    }

    // /llm добавить openrouter КЛЮЧ [модель]   или   /llm добавить имя тип адрес модель [КЛЮЧ]
    private static string AddProfile(string[] parts)
    {
        const string usage = "Быстро: /llm добавить openrouter КЛЮЧ (или groq КЛЮЧ). Полностью: /llm добавить имя ollama|openai адрес модель [КЛЮЧ]";
        if (parts.Length < 4) return usage;

        string name = parts[2];
        if (!LlmProfiles.IsValidName(name) || name.Equals(LlmProfiles.OllamaName, StringComparison.OrdinalIgnoreCase) ||
            name.Equals(LocalModel.ProfileName, StringComparison.OrdinalIgnoreCase))
            return "Имя профиля: 2-20 букв, цифр, - или _ (имя «ollama» занято).";

        var profile = new LlmProfile { name = name, apiKey = string.Empty };
        LlmProfiles.Preset preset;
        string second = parts[3].ToLowerInvariant();
        if (LlmProfiles.TryGetPreset(name, out preset) && second != "ollama" && second != "openai")
        {
            profile.kind = preset.Kind;
            profile.url = preset.Url;
            profile.model = parts.Length > 4 ? parts[4] : preset.Model;
            profile.apiKey = parts[3];
        }
        else
        {
            if (parts.Length < 6) return usage;
            if (second != "ollama" && second != "openai") return "Тип профиля: ollama или openai.";
            string url = parts[4];
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return "Не похоже на адрес. Пример: https://openrouter.ai/api/v1";
            profile.kind = second;
            profile.url = url.TrimEnd('/');
            profile.model = parts[5];
            profile.apiKey = parts.Length > 6 ? parts[6] : string.Empty;
        }

        LlmProfiles.AddOrReplace(profile);
        return "Профиль «" + profile.name + "» сохранён (" + profile.model + ", " + LlmProfiles.MaskKey(profile.apiKey) +
               "). Проверка: /llm проверка";
    }

    private async Task<string> TestAsync(CancellationToken ct)
    {
        _llm.ResetBlocks();
        var timer = Stopwatch.StartNew();
        try
        {
            string answer = await _llm.ChatAsync("Отвечай одним словом.", null, "Скажи: работает", false, ct);
            return "Связь есть через «" + _llm.LastProfileName + "» (" + (timer.ElapsedMilliseconds / 1000f).ToString("0.0") +
                   " с). Ответ модели: " + Clean(answer);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LlmException ex)
        {
            return ex.Message;
        }
    }
}
