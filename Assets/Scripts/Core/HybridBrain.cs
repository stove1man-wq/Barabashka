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

        AgentResponse ruleResponse = await _rules.RespondAsync(input, ct);
        if (!IsUnknown(ruleResponse))
        {
            Remember(text, string.IsNullOrEmpty(ruleResponse.Text) ? "Готово." : ruleResponse.Text);
            return ruleResponse;
        }

        // Правила не поняли. Слишком короткие обрывки (шум распознавания) модели не отдаём.
        if (!LlmSettings.Enabled || CountLetters(text) < 3) return ruleResponse;

        try
        {
            string content = await _llm.ChatAsync(AgentToolCatalog.BuildSystemPrompt(DateTime.Now), _history, text, true, ct);
            _warnedUnavailable = false;
            Debug.Log("[LLM] Ответ модели: " + content);

            AgentResponse answer = Interpret(content, input);
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

    private static AgentResponse Interpret(string content, Utterance input)
    {
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
                        "\nОблако: /llm добавить openrouter КЛЮЧ (или groq КЛЮЧ)";
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
        foreach (LlmProfile p in LlmProfiles.Extra())
        {
            n++;
            sb.Append('\n').Append(n).Append(". ").Append(p.name).Append(p.IsOpenAi ? " (OpenAI-совместимый): " : " (Ollama): ")
              .Append(p.url).Append(", ").Append(p.model);
            if (p.IsOpenAi) sb.Append(", ").Append(LlmProfiles.MaskKey(p.apiKey));
            if (_llm.IsBlocked(p.name)) sb.Append(" [не отвечает]");
        }
        if (n == 0) sb.Append("\n(пусто)");
        return sb.ToString();
    }

    // /llm добавить openrouter КЛЮЧ [модель]   или   /llm добавить имя тип адрес модель [КЛЮЧ]
    private static string AddProfile(string[] parts)
    {
        const string usage = "Быстро: /llm добавить openrouter КЛЮЧ (или groq КЛЮЧ). Полностью: /llm добавить имя ollama|openai адрес модель [КЛЮЧ]";
        if (parts.Length < 4) return usage;

        string name = parts[2];
        if (!LlmProfiles.IsValidName(name) || name.Equals(LlmProfiles.OllamaName, StringComparison.OrdinalIgnoreCase))
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
