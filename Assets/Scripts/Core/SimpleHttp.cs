using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

public sealed class HttpFailure : Exception
{
    public HttpFailure(string message) : base(message) { }
}

/// <summary>Простой GET-запрос с отменой и таймаутом для главного потока Unity.</summary>
public static class SimpleHttp
{
    public static async Task<string> GetAsync(string url, int timeoutSec, CancellationToken ct)
    {
        using (var request = UnityWebRequest.Get(url))
        {
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

            if (request.result == UnityWebRequest.Result.ConnectionError)
                throw new HttpFailure("нет связи с интернетом");
            if (request.result != UnityWebRequest.Result.Success)
                throw new HttpFailure("сервис ответил ошибкой " + request.responseCode);
            return request.downloadHandler.text;
        }
    }
}
