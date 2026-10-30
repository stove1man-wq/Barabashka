using System;
using System.Linq;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Core.Settings
{
    /// <summary>
    /// Обработчик голосовых команд для страниц настроек.
    /// </summary>
    public static class SettingsVoice
    {
        /// <summary>
        /// Попытаться обработать голосовую команду для настроек.
        /// </summary>
        /// <param name="message">Сообщение пользователя (нормализованное)</param>
        /// <param name="settingsOpen">Открыты ли настройки</param>
        /// <param name="ui">UI менеджер</param>
        /// <returns>true, если команда распознана и обработана</returns>
        public static bool TryHandle(string message, bool settingsOpen, AgentUIManager ui)
        {
            if (string.IsNullOrEmpty(message) || ui == null)
                return false;

            // Нормализация сообщения: нижний регистр, замена ё на е, удаление знаков
            string normalized = NormalizeMessage(message);
            
            // Проверяем команду "открой/покажи настройки <страница>"
            var match = System.Text.RegularExpressions.Regex.Match(normalized, @"^(открой|покажи|покажи настройки)\s+(.+)");
            if (match.Success)
            {
                string pageName = match.Groups[2].Value.Trim();
                ISettingsPage page = SettingsRegistry.Find(pageName);
                
                if (page != null)
                {
                    // Показываем указанную страницу
                    ui.ShowSettings(page.Id);
                    return true;
                }
                else
                {
                    // Если страница не найдена, работаем как с WantsSettings
                    return WantsSettings(normalized);
                }
            }

            // Если настройки уже открыты и есть страница с именем, ищем в ней команду
            if (settingsOpen)
            {
                // Перебираем страницы, начиная с текущей
                var pages = SettingsRegistry.Pages;
                ISettingsPage currentPage = null;
                if (!string.IsNullOrEmpty(ui.CurrentSettingsPageId))
                {
                    currentPage = SettingsRegistry.Find(ui.CurrentSettingsPageId);
                }
                
                // Сначала проверяем текущую страницу, затем остальные
                if (currentPage != null)
                {
                    return TryHandleOnPage(currentPage, normalized, out string reply) && 
                           ui.ShowSettings(ui.CurrentSettingsPageId, reply);
                }
                
                foreach (var page in pages)
                {
                    if (TryHandleOnPage(page, normalized, out string reply))
                    {
                        return ui.ShowSettings(page.Id, reply);
                    }
                }
            }

            // Если не в режиме настроек и не получилось обработать, работаем как с WantsSettings
            return WantsSettings(normalized);
        }

        /// <summary>
        /// Проверяет команды, которые работают вне режима настроек
        /// </summary>
        private static bool WantsSettings(string normalized)
        {
            // Команды открытия настроек
            return normalized.Contains("настройки") || 
                   normalized.Contains("показать настройки") ||
                   normalized.Contains("открыть настройки");
        }

        /// <summary>
        /// Попытаться обработать команду на конкретной странице
        /// </summary>
        private static bool TryHandleOnPage(ISettingsPage page, string normalized, out string reply)
        {
            reply = null;
            if (page == null || string.IsNullOrEmpty(normalized))
                return false;

            return page.TryVoice(normalized, out reply);
        }

        /// <summary>
        /// Нормализация сообщения: приведение к нижнему регистру, замена ё на е, удаление знаков
        /// </summary>
        private static string NormalizeMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;
                
            // Приводим к нижнему регистру
            string normalized = message.ToLowerInvariant();
            
            // Заменяем ё на е
            normalized = normalized.Replace('ё', 'е');
            
            // Убираем знаки препинания и лишние пробелы
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\w\s]", string.Empty);
            
            // Убираем лишние пробелы
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
            
            return normalized;
        }
    }
}