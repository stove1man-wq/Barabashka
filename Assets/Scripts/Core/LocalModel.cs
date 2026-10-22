using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Файл языковой модели для работы без сети (профиль «device»): скачивание в память телефона и учёт состояния.
/// Модель лежит в persistentDataPath и переживает обновление приложения (но не удаление).
/// Скачивание идёт в фоне; о результате сообщает событие Finished.
/// </summary>
public static class LocalModel
{
    /// <summary>Qwen2.5 1.5B Instruct, Q4_K_M, около 1 ГБ: компромисс между скоростью и пониманием на телефоне.</summary>
    public const string DefaultUrl = "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf";

    public const string ProfileName = "device";
    private const long MinPlausibleBytes = 50L * 1024 * 1024;

    private static bool _downloading;
    private static float _progress;
    private static long _totalBytes;
    private static string _lastError;

    /// <summary>Сообщение о завершении скачивания (успех или ошибка); вызывается в главном потоке.</summary>
    public static event Action<string> Finished;

    public static bool IsDownloading { get { return _downloading; } }
    public static string LastError { get { return _lastError; } }

    public static string Dir { get { return Path.Combine(Application.persistentDataPath, "local-llm"); } }
    public static string DefaultPath { get { return Path.Combine(Dir, "model.gguf"); } }

    public static bool Exists(string path)
    {
        try { return !string.IsNullOrEmpty(path) && File.Exists(path) && new FileInfo(path).Length > MinPlausibleBytes; }
        catch (Exception) { return false; }
    }

    public static long SizeBytes(string path)
    {
        try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
        catch (Exception) { return 0; }
    }

    public static string DescribeDownload()
    {
        if (!_downloading) return string.Empty;
        string total = _totalBytes > 0 ? " из " + (_totalBytes / (1024 * 1024)) + " МБ" : string.Empty;
        return "скачивается " + Mathf.RoundToInt(_progress * 100f) + "%" + total;
    }

    /// <summary>Запускает скачивание, если оно ещё не идёт. Возвращает false, если уже качается.</summary>
    public static bool StartDownload(string url)
    {
        if (_downloading) return false;
        _downloading = true;
        _progress = 0f;
        _totalBytes = 0;
        _lastError = null;
        _ = RunDownloadAsync(string.IsNullOrWhiteSpace(url) ? DefaultUrl : url.Trim());
        return true;
    }

    private static async Task RunDownloadAsync(string url)
    {
        string message;
        string tempPath = DefaultPath + ".part";
        try
        {
            Directory.CreateDirectory(Dir);
            if (File.Exists(tempPath)) File.Delete(tempPath);

            using (var request = new UnityWebRequest(url, "GET"))
            {
                request.downloadHandler = new DownloadHandlerFile(tempPath) { removeFileOnAbort = true };
                request.timeout = 0;
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    _progress = operation.progress;
                    if (_totalBytes == 0)
                    {
                        string length = request.GetResponseHeader("Content-Length");
                        long parsed;
                        if (long.TryParse(length, out parsed)) _totalBytes = parsed;
                    }
                    await Task.Delay(500);
                }

                if (request.result != UnityWebRequest.Result.Success)
                    throw new Exception(request.error + (request.responseCode > 0 ? " (" + request.responseCode + ")" : string.Empty));
            }

            if (!Exists(tempPath)) throw new Exception("скачанный файл слишком мал или повреждён");
            if (Barabashka.Contracts.LocalLlmHost.Current != null) Barabashka.Contracts.LocalLlmHost.Current.Unload();
            if (File.Exists(DefaultPath)) File.Delete(DefaultPath);
            File.Move(tempPath, DefaultPath);

            LlmProfiles.AddOrReplace(new LlmProfile { name = ProfileName, kind = "device", url = string.Empty, model = DefaultPath, apiKey = string.Empty });
            message = "Модель для работы без сети скачана (" + (SizeBytes(DefaultPath) / (1024 * 1024)) + " МБ) и подключена. Проверка: /llm проверка";
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch (Exception) { }
            Debug.LogWarning("[LocalModel] " + ex.Message);
            message = "Не удалось скачать модель: " + ex.Message;
        }
        finally
        {
            _downloading = false;
        }

        try { Finished?.Invoke(message); }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    /// <summary>Убирает профиль и удаляет файл модели.</summary>
    public static string Remove()
    {
        if (_downloading) return "Скачивание ещё идёт, дождитесь окончания.";
        if (Barabashka.Contracts.LocalLlmHost.Current != null) Barabashka.Contracts.LocalLlmHost.Current.Unload();
        bool hadProfile = LlmProfiles.Remove(ProfileName);
        bool deleted = false;
        try
        {
            if (File.Exists(DefaultPath)) { File.Delete(DefaultPath); deleted = true; }
        }
        catch (Exception ex) { return "Не удалось удалить файл модели: " + ex.Message; }
        return hadProfile || deleted ? "Модель на телефоне удалена." : "Модели на телефоне нет.";
    }
}
