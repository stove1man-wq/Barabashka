using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class HttpFailure : Exception
{
    public HttpFailure(string message) : base(message) { }
}

/// <summary>Простой GET-запрос с отменой и таймаутом для главного потока Unity.</summary>
public static class SimpleHttp
{
    public static async Task<string> GetAsync(string url, int timeoutSec, CancellationToken ct, string userAgent = null)
    {
        // Первый запрос после долгой паузы иногда не проходит (мобильная сеть, DNS): один повтор.
        try
        {
            return await GetOnceAsync(url, timeoutSec, ct, userAgent);
        }
        catch (HttpFailure ex)
        {
            if (!ex.Message.StartsWith("нет связи", StringComparison.Ordinal)) throw;
            await Task.Delay(700, ct);
            return await GetOnceAsync(url, timeoutSec + 5, ct, userAgent);
        }
    }

    private static async Task<string> GetOnceAsync(string url, int timeoutSec, CancellationToken ct, string userAgent)
    {
        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = timeoutSec;
            if (!string.IsNullOrEmpty(userAgent)) request.SetRequestHeader("User-Agent", userAgent);

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

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogWarning("[Http] " + url + " -> " + request.error);
                throw new HttpFailure("нет связи с сервисом (" + request.error + ")");
            }
            if (request.result != UnityWebRequest.Result.Success)
                throw new HttpFailure("сервис ответил ошибкой " + request.responseCode);
            return request.downloadHandler.text;
        }
    }
}
