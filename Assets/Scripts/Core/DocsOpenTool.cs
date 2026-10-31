using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    return Task.FromResult(ToolResult.Fail("Какой документ открыть? Назовите название файла."));

                string folder = DocsStore.Folder;
                if (!Directory.Exists(folder))
                    return Task.FromResult(ToolResult.Fail("Документы не найдены. Сначала добавьте документы."));

                // Поиск документа по имени (без учёта регистра)
                FileInfo foundFile = new DirectoryInfo(folder).GetFiles()
                    .FirstOrDefault(f => f.Name.IndexOf(documentName, StringComparison.OrdinalIgnoreCase) >= 0
                                         && DocsStore.IsSupported(f.Extension));

                if (foundFile == null)
                    return Task.FromResult(ToolResult.Fail("Документ с названием «" + documentName + "» не найден."));

                if (Application.platform != RuntimePlatform.Android)
                    return Task.FromResult(ToolResult.Ok("Документ найден: " + foundFile.FullName));

                if (OpenOnAndroid(foundFile.FullName))
                    return Task.FromResult(ToolResult.Ok("Открываю документ: " + foundFile.Name));
                return Task.FromResult(ToolResult.Fail("Не удалось открыть документ «" + foundFile.Name + "» на телефоне."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Fail("Ошибка при открытии документа: " + ex.Message));
            }
        }

        // Внимание: на Android 7 и новее открытие по file:// запрещено системой. Для надёжной работы нужен FileProvider
        // (отдельная задача). Пока при отказе системы честно сообщаем, что не открылось.
        private static bool OpenOnAndroid(string filePath)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var file = new AndroidJavaObject("java.io.File", filePath))
                using (var uri = uriClass.CallStatic<AndroidJavaObject>("fromFile", file))
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    intent.Call<AndroidJavaObject>("setDataAndType", uri, DocsStore.MimeType(Path.GetExtension(filePath)));
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000001); // FLAG_ACTIVITY_NEW_TASK | FLAG_GRANT_READ_URI_PERMISSION
                    activity.Call("startActivity", intent);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Docs] Не удалось открыть документ: " + ex.Message);
                return false;
            }
#else
            return false;
#endif
        }
    }
}
