using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Core
{
    /// <summary>
    /// Инструмент для чтения уведомлений MAX.
    /// Фразы: «прочитай сообщения в макс», «что нового в макс»
    /// </summary>
    public sealed class MaxReadTool : IAgentTool
    {
        public string Name => "max_read";
        public string Description => "прочитать последние уведомления от MAX. {\"tool\":\"max_read\"}";

        public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
        {
            try
            {
                // Получаем экземпляр провайдера уведомлений MAX
                var messengerHub = FindFirstObjectByType<IMessengerHub>();
                if (messengerHub == null)
                {
                    return ToolResult.Fail("Система мессенджеров не инициализирована.");
                }

                // Ищем провайдер MAX уведомлений
                IMessengerConnector maxConnector = null;
                foreach (var provider in messengerHub.Providers)
                {
                    if (provider.Id == "max-notifications")
                    {
                        maxConnector = provider;
                        break;
                    }
                }

                if (maxConnector == null)
                {
                    return ToolResult.Fail("Провайдер уведомлений MAX не найден.");
                }

                // Читаем последние уведомления (ограничиваем 5 сообщениями)
                var messages = await maxConnector.ReadRecentAsync(string.Empty, 5, ct);
                
                if (messages == null || messages.Count == 0)
                {
                    return ToolResult.Ok("Нет новых уведомлений от MAX.");
                }

                // Формируем краткий отчет о сообщениях
                var messageTexts = new List<string>();
                foreach (var message in messages)
                {
                    string sender = string.IsNullOrEmpty(message.Sender) ? "MAX" : message.Sender;
                    string text = string.IsNullOrEmpty(message.Text) ? "Без текста" : message.Text;
                    messageTexts.Add($"{sender}: {text}");
                }

                string report = string.Join(", ", messageTexts);
                
                // Используем нейтральный цвет вместо Telegram-цвета
                string spokenText = $"Новые уведомления от MAX: {report}";
                
                return ToolResult.Ok(spokenText);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MAX] Ошибка при чтении уведомлений: " + ex.Message);
                return ToolResult.Fail("Не удалось прочитать уведомления от MAX.");
            }
        }
    }
}