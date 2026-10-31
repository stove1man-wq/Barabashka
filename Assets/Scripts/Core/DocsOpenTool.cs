using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Core
{
    /// <summary>
    /// Инструмент для открытия документа.
    /// Фразы: «открой документ X»
    /// </summary>
    public sealed class DocsOpenTool : IAgentTool
    {
        public string Name => "docs_open";
        public string Description => "открыть документ по названию. {\"tool\":\"docs_open\",\"name\":\"название файла\"}";

        public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
        {
            try
            {
                string documentName = ToolArgs.Get(args, "name").Trim();
                if (string.IsNullOrEmpty(documentName))
                {
                    return Task.FromResult(ToolResult.Fail("Какой документ открыть? Назовите название файла."));
                }

                string documentsFolder = Path.Combine(MediaPicker.InboxFolder, "documents");
                
                if (!Directory.Exists(documentsFolder))
                {
                    return Task.FromResult(ToolResult.Fail("Документы не найдены. Сначала добавьте документы."));
                }

                // Поиск документа по имени (без учета регистра)
                var dirInfo = new DirectoryInfo(documentsFolder);
                var foundFile = dirInfo.GetFiles()
                    .FirstOrDefault(f => f.Name.IndexOf(documentName, StringComparison.OrdinalIgnoreCase) >= 0 
                                       && IsSupportedDocument(f.Extension));

                if (foundFile == null)
                {
                    return Task.FromResult(ToolResult.Fail($"Документ с названием «{documentName}» не найден."));
                }

                // Открываем документ через Android Intent
                if (Application.platform.Equals(RuntimePlatform.Android))
                {
                    OpenDocumentOnAndroid(foundFile.FullName);
                    return Task.FromResult(ToolResult.Ok($"Открываю документ: {foundFile.Name}"));
                }
                else
                {
                    // На ПК просто показываем путь
                    return Task.FromResult(ToolResult.Ok($"Документ найден: {foundFile.FullName}"));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Fail("Ошибка при открытии документа: " + ex.Message));
            }
        }

        private bool IsSupportedDocument(string extension)
        {
            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf", ".doc", ".docx", ".txt", ".xls", ".xlsx"
            };
            return supportedExtensions.Contains(extension);
        }

        private void OpenDocumentOnAndroid(string filePath)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                using (var activity = GetCurrentActivity())
                {
                    // Устанавливаем действие открытия файла
                    intent.Call<AndroidJavaObject>("setAction", "android.intent.action.VIEW");
                    
                    // Создаем URI файла
                    var fileUri = AndroidJavaObject.New("android.net.Uri", "file://" + filePath);
                    intent.Call<AndroidJavaObject>("setData", fileUri);
                    
                    // Устанавливаем MIME-тип для файла
                    string mimeType = GetMimeType(Path.GetExtension(filePath));
                    intent.Call<AndroidJavaObject>("setType", mimeType);
                    
                    // Добавляем флаг для нового экземпляра
                    intent.Call<AndroidJavaObject>("addFlags", 0x00000001); // FLAG_ACTIVITY_NEW_TASK
                    
                    // Запускаем активность
                    activity.Call("startActivity", intent);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Docs] Не удалось открыть документ: " + ex.Message);
            }
#endif
        }

        private string GetMimeType(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".txt": return "text/plain";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                default: return "application/octet-stream";
            }
        }

        private AndroidJavaObject GetCurrentActivity()
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
    }
}