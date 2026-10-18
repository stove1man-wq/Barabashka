using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private readonly OllamaClient _llm;
    private readonly List<KeyValuePair<string, string>> _history = new List<KeyValuePair<string, string>>();
    private bool _warnedUnavailable;

    public HybridBrain(IBrain rules, OllamaClient llm)
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
        catch (OllamaException ex)
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
        string[] parts = text.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        string action = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;
        string value = parts.Length > 2 ? parts[2].Trim() : string.Empty;
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
                if (value.Length == 0) { reply = "Укажите адрес: /llm адрес http://192.168.0.5:11434"; break; }
                if (value.IndexOf("://", StringComparison.Ordinal) < 0) value = "http://" + value;
                Uri uri;
                if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    reply = "Не похоже на адрес. Пример: /llm адрес http://192.168.0.5:11434";
                    break;
                }
                LlmSettings.Url = value;
                reply = "Адрес Ollama: " + LlmSettings.Url;
                break;
            case "модель":
            case "model":
                if (value.Length == 0) { reply = "Укажите имя модели: /llm модель llama3.1:8b"; break; }
                LlmSettings.Model = value;
                reply = "Модель: " + LlmSettings.Model;
                break;
            case "проверка":
            case "test":
                reply = await TestAsync(ct);
                break;
            default:
                reply = "Языковая модель: " + (LlmSettings.Enabled ? "включена" : "выключена") +
                        "\nАдрес: " + LlmSettings.Url + "\nМодель: " + LlmSettings.Model +
                        "\nКоманды: /llm вкл, /llm выкл, /llm адрес …, /llm модель …, /llm проверка";
                break;
        }

        return new AgentResponse { Text = reply, LanguageCode = input != null ? input.LanguageCode : null };
    }

    private async Task<string> TestAsync(CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            string answer = await _llm.ChatAsync("Отвечай одним словом.", null, "Скажи: работает", false, ct);
            return "Связь есть (" + (timer.ElapsedMilliseconds / 1000f).ToString("0.0") + " с). Ответ модели: " + Clean(answer);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OllamaException ex)
        {
            return ex.Message;
        }
    }
}
