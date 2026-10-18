using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class LlmException : Exception
{
    /// <summary>true для сбоев связи и лимитов: профиль на время откладывается.</summary>
    public readonly bool Transient;

    public LlmException(string message, bool transient) : base(message) { Transient = transient; }
}

/// <summary>
/// Клиент языковых моделей. Идёт по цепочке профилей (LlmProfiles.Chain): первый ответивший побеждает,
/// профиль со сбоем связи пропускается на несколько минут, чтобы не ждать таймаут при каждой фразе.
/// Поддерживает Ollama (/api/chat) и OpenAI-совместимые сервисы (/chat/completions). Главный поток Unity.
/// </summary>
public sealed class LlmClient
{
    private const int SingleProfileTimeoutSec = 60;
    private const int ChainTimeoutSec = 25;
    private const double BlockSeconds = 180.0;

    [Serializable] public class OllamaReply { public OllamaMessage message; }
    [Serializable] public class OllamaMessage { public string role; public string content; }
    [Serializable] public class OpenAiReply { public OpenAiChoice[] choices; }
    [Serializable] public class OpenAiChoice { public OpenAiMessage message; }
    [Serializable] public class OpenAiMessage { public string role; public string content; }

    private readonly Dictionary<string, double> _blockedUntil = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Имя профиля, давшего последний успешный ответ.</summary>
    public string LastProfileName { get; private set; }

    public void ResetBlocks() { _blockedUntil.Clear(); }

    public bool IsBlocked(string profileName)
    {
        double until;
        return _blockedUntil.TryGetValue(profileName, out until) && Time.realtimeSinceStartupAsDouble < until;
    }

    public async Task<string> ChatAsync(string system, IList<KeyValuePair<string, string>> history,
        string userText, bool jsonMode, CancellationToken ct)
    {
        List<LlmProfile> chain = LlmProfiles.Chain();
        if (chain.Count == 0)
            throw new LlmException("Нет подключённых моделей. Задайте адрес Ollama (/llm адрес …) или добавьте облачную (/llm добавить …).", false);

        int timeout = chain.Count == 1 ? SingleProfileTimeoutSec : ChainTimeoutSec;
        var errors = new List<string>();
        bool attempted = false;

        // Первый проход уважает паузы после сбоев; если из-за них никого не опросили, пробуем всех подряд.
        for (int pass = 0; pass < 2 && !attempted; pass++)
        {
            foreach (LlmProfile profile in chain)
            {
                if (pass == 0 && IsBlocked(profile.name))
                {
                    errors.Add(profile.name + ": недавно не отвечал, пропущен");
                    continue;
                }

                attempted = true;
                try
                {
                    string content = await CallAsync(profile, system, history, userText, jsonMode, timeout, ct);
                    _blockedUntil.Remove(profile.name);
                    LastProfileName = profile.name;
                    return content;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (LlmException ex)
                {
                    Debug.LogWarning("[LLM] " + profile.name + ": " + ex.Message);
                    errors.Add(profile.name + ": " + ex.Message);
                    _blockedUntil[profile.name] = Time.realtimeSinceStartupAsDouble + (ex.Transient ? BlockSeconds : BlockSeconds / 3);
                }
            }
        }

        throw new LlmException(string.Join("; ", errors), true);
    }

    private async Task<string> CallAsync(LlmProfile profile, string system, IList<KeyValuePair<string, string>> history,
        string userText, bool jsonMode, int timeoutSec, CancellationToken ct)
    {
        string baseUrl = (profile.url ?? string.Empty).Trim().TrimEnd('/');
        bool openAi = profile.IsOpenAi;
        string endpoint = baseUrl + (openAi ? "/chat/completions" : "/api/chat");
        string messages = BuildMessages(system, history, userText);

        var body = new StringBuilder();
        body.Append("{\"model\":").Append(MiniJson.Quote(profile.model));
        body.Append(",\"stream\":false");
        if (openAi)
        {
            body.Append(",\"temperature\":0.2");
        }
        else
        {
            if (jsonMode) body.Append(",\"format\":\"json\"");
            body.Append(",\"keep_alive\":\"30m\"");
            body.Append(",\"options\":{\"temperature\":0.2}");
        }
        body.Append(",\"messages\":").Append(messages).Append('}');

        using (var request = new UnityWebRequest(endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (openAi && !string.IsNullOrEmpty(profile.apiKey))
                request.SetRequestHeader("Authorization", "Bearer " + profile.apiKey);
            request.timeout = timeoutSec;

            var done = new TaskCompletionSource<bool>();
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            operation.completed += _ => done.TrySetResult(true);

            using (ct.Register(() =>
            {
                try { request.Abort(); } catch (Exception) { }
                done.TrySetResult(true);
            }))
            {
                await done.Task;
            }
            ct.ThrowIfCancellationRequested();

            if (request.result != UnityWebRequest.Result.Success)
                throw MapError(profile, request);

            string content;
            try
            {
                string text = request.downloadHandler.text;
                if (openAi)
                {
                    OpenAiReply reply = JsonUtility.FromJson<OpenAiReply>(text);
                    content = reply != null && reply.choices != null && reply.choices.Length > 0 && reply.choices[0].message != null
                        ? reply.choices[0].message.content : null;
                }
                else
                {
                    OllamaReply reply = JsonUtility.FromJson<OllamaReply>(text);
                    content = reply != null && reply.message != null ? reply.message.content : null;
                }
            }
            catch (Exception ex)
            {
                throw new LlmException("не удалось разобрать ответ: " + ex.Message, false);
            }

            if (string.IsNullOrWhiteSpace(content))
                throw new LlmException("пустой ответ модели", false);
            return content;
        }
    }

    private static string BuildMessages(string system, IList<KeyValuePair<string, string>> history, string userText)
    {
        var sb = new StringBuilder();
        sb.Append("[{\"role\":\"system\",\"content\":").Append(MiniJson.Quote(system)).Append('}');
        if (history != null)
        {
            foreach (KeyValuePair<string, string> turn in history)
                sb.Append(",{\"role\":").Append(MiniJson.Quote(turn.Key))
                  .Append(",\"content\":").Append(MiniJson.Quote(turn.Value)).Append('}');
        }
        sb.Append(",{\"role\":\"user\",\"content\":").Append(MiniJson.Quote(userText)).Append("}]");
        return sb.ToString();
    }

    private static LlmException MapError(LlmProfile profile, UnityWebRequest request)
    {
        long code = request.responseCode;
        if (request.result == UnityWebRequest.Result.ConnectionError)
            return new LlmException("нет связи с " + profile.url + " (" + request.error + ")", true);

        if (code == 401 || code == 403)
            return new LlmException("ключ не принят (" + code + "), проверьте: /llm ключ " + profile.name + " …", false);
        if (code == 404)
            return new LlmException("модель «" + profile.model + "» не найдена", false);
        if (code == 429)
            return new LlmException("лимит запросов исчерпан, попробуйте позже", true);
        if (code >= 500)
            return new LlmException("сервис вернул ошибку " + code, true);

        string detail = string.Empty;
        try
        {
            string text = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (!string.IsNullOrEmpty(text)) detail = ": " + (text.Length > 120 ? text.Substring(0, 120) : text);
        }
        catch (Exception) { }
        return new LlmException("ошибка " + code + detail, false);
    }
}
