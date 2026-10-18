using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class OllamaException : Exception
{
    public OllamaException(string message) : base(message) { }
}

/// <summary>
/// Клиент Ollama (POST /api/chat, без потока). Работает на главном потоке Unity.
/// Адрес и модель берутся из LlmSettings при каждом вызове, поэтому смена настроек действует сразу.
/// </summary>
public sealed class OllamaClient
{
    private const int TimeoutSeconds = 60;

    [Serializable] public class ChatReply { public ChatMessage message; }
    [Serializable] public class ChatMessage { public string role; public string content; }

    /// <param name="history">Предыдущие реплики: пары (роль "user"/"assistant", текст).</param>
    public async Task<string> ChatAsync(string system, IList<KeyValuePair<string, string>> history,
        string userText, bool jsonMode, CancellationToken ct)
    {
        string baseUrl = LlmSettings.Url;
        string model = LlmSettings.Model;

        var body = new StringBuilder();
        body.Append("{\"model\":").Append(MiniJson.Quote(model));
        body.Append(",\"stream\":false");
        if (jsonMode) body.Append(",\"format\":\"json\"");
        body.Append(",\"keep_alive\":\"30m\"");
        body.Append(",\"options\":{\"temperature\":0.2}");
        body.Append(",\"messages\":[");
        body.Append("{\"role\":\"system\",\"content\":").Append(MiniJson.Quote(system)).Append('}');
        if (history != null)
        {
            foreach (KeyValuePair<string, string> turn in history)
                body.Append(",{\"role\":").Append(MiniJson.Quote(turn.Key))
                    .Append(",\"content\":").Append(MiniJson.Quote(turn.Value)).Append('}');
        }
        body.Append(",{\"role\":\"user\",\"content\":").Append(MiniJson.Quote(userText)).Append('}');
        body.Append("]}");

        using (var request = new UnityWebRequest(baseUrl + "/api/chat", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = TimeoutSeconds;

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
            {
                if (request.responseCode == 404)
                    throw new OllamaException("Модель «" + model + "» не найдена в Ollama. Задайте другую: /llm модель имя");
                if (request.result == UnityWebRequest.Result.ConnectionError)
                    throw new OllamaException("Не удалось подключиться к Ollama по адресу " + baseUrl + " (" + request.error + ")");
                throw new OllamaException("Ollama вернула ошибку: " + request.error);
            }

            ChatReply reply;
            try { reply = JsonUtility.FromJson<ChatReply>(request.downloadHandler.text); }
            catch (Exception ex) { throw new OllamaException("Не удалось разобрать ответ Ollama: " + ex.Message); }

            if (reply == null || reply.message == null || string.IsNullOrWhiteSpace(reply.message.content))
                throw new OllamaException("Ollama вернула пустой ответ.");
            return reply.message.content;
        }
    }
}
