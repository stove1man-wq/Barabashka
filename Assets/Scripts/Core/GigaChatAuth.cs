using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Barabashka.Core
{
    /// <summary>
    /// Обработчик авторизации для GigaChat API
    /// </summary>
    public static class GigaChatAuth
    {
        private static string _cachedToken;
        private static DateTime _tokenExpiry;
        private static readonly object _lock = new object();

        /// <summary>
        /// Получить токен для GigaChat API
        /// </summary>
        public static async Task<string> GetTokenAsync(string apiKey, CancellationToken ct)
        {
            lock (_lock)
            {
                // Проверяем, не истек ли токен (с запасом 60 секунд)
                if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry.Subtract(TimeSpan.FromSeconds(60)))
                {
                    return _cachedToken;
                }
            }

            // Получаем новый токен
            string token = await RequestTokenAsync(apiKey, ct);
            
            lock (_lock)
            {
                _cachedToken = token;
                _tokenExpiry = DateTime.UtcNow.AddMinutes(29); // Токен живёт 30 минут
            }
            
            return token;
        }

        private static async Task<string> RequestTokenAsync(string apiKey, CancellationToken ct)
        {
            var url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                // Устанавливаем заголовки
                // Ключ уже в Base64, не кодируем повторно
                request.SetRequestHeader("Authorization", $"Basic {apiKey}");
                request.SetRequestHeader("RqUID", Guid.NewGuid().ToString());
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
                // scope=GIGACHAT_API_PERS только в теле запроса, не в заголовке
                // request.SetRequestHeader("scope", "GIGACHAT_API_PERS"); // Убран из заголовка

                // Подготовка данных формы
                string body = "scope=GIGACHAT_API_PERS";
                byte[] bodyData = Encoding.UTF8.GetBytes(body);
                request.uploadHandler = new UploadHandlerRaw(bodyData);
                request.downloadHandler = new DownloadHandlerBuffer();

                // Отправляем запрос
                UnityWebRequestAsyncOperation op = request.SendWebRequest();
                
                // Ожидаем завершения
                while (!op.isDone)
                {
                    await Task.Yield();
                    ct.ThrowIfCancellationRequested();
                }

                // Обрабатываем результат
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Ошибка получения токена: {request.error}");
                }

                string responseText = request.downloadHandler.text;
                
                // Извлекаем токен из ответа JSON
                // Ожидаем JSON в формате: {"access_token":"<token>","expires_in":1787,"token_type":"Bearer"}
                try
                {
                    // Извлекаем токен (простой парсинг)
                    int startIndex = responseText.IndexOf("\"access_token\":\"");
                    if (startIndex >= 0)
                    {
                        startIndex += "\"access_token\":\"".Length;
                        int endIndex = responseText.IndexOf('"', startIndex);
                        if (endIndex > startIndex)
                        {
                            return responseText.Substring(startIndex, endIndex - startIndex);
                        }
                    }
                    
                    throw new Exception($"Не удалось извлечь токен из ответа: {responseText}");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка парсинга ответа токена: {ex.Message}. Ответ: {responseText}");
                }
            }
        }
    }
}