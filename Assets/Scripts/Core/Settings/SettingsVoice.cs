using System;
using System.Text.RegularExpressions;
using Barabashka.Contracts;

namespace Barabashka.Core.Settings
{
    /// <summary>
    /// Голосовые и текстовые команды страниц настроек.
    /// Resolve не знает про интерфейс (его просто проверять тестами): он говорит, какую страницу показать и что ответить.
    /// Вне режима настроек работает только «открой настройки &lt;страница&gt;». Команды самих страниц (TryVoice) выполняются
    /// только внутри режима настроек, чтобы обычные фразы не меняли настройки случайно.
    /// </summary>
    public static class SettingsVoice
    {
        public struct Result
        {
            /// <summary>Фраза была командой настроек и обработана.</summary>
            public bool Handled;
            /// <summary>Какую страницу показать.</summary>
            public string PageId;
            /// <summary>Подтверждение для области настроек (может быть null).</summary>
            public string Reply;
        }

        private static readonly Regex OpenPage = new Regex(
            @"^(?:открой|открыть|покажи|показать|зайди в|перейди в|перейти в)\s+(?:в\s+)?настройки\s+(.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static Result Resolve(string message, bool settingsOpen, string currentPageId)
        {
            var none = new Result();
            string text = Normalize(message);
            if (text.Length == 0) return none;

            Match open = OpenPage.Match(text);
            if (open.Success)
            {
                ISettingsPage target = FindPage(open.Groups[1].Value.Trim());
                if (target != null) return new Result { Handled = true, PageId = target.Id };
                // «открой настройки вайфай» и подобное: не наши страницы, дальше решают прежние правила.
                return none;
            }

            if (!settingsOpen) return none;

            ISettingsPage current = string.IsNullOrEmpty(currentPageId) ? null : SettingsRegistry.Find(currentPageId);
            string reply;
            if (current != null && current.TryVoice(text, out reply))
                return new Result { Handled = true, PageId = current.Id, Reply = reply };
            foreach (ISettingsPage other in SettingsRegistry.Pages)
            {
                if (other == current) continue;
                if (other.TryVoice(text, out reply))
                    return new Result { Handled = true, PageId = other.Id, Reply = reply };
            }
            return none;
        }

        /// <summary>Нижний регистр, ё как е, знаки препинания как пробелы, лишние пробелы убраны.</summary>
        public static string Normalize(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            string t = message.ToLowerInvariant().Replace('ё', 'е');
            t = Regex.Replace(t, @"[^\w\s]", " ");
            return Regex.Replace(t, @"\s+", " ").Trim();
        }

        private static ISettingsPage FindPage(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            ISettingsPage exact = SettingsRegistry.Find(name);
            if (exact != null) return exact;
            foreach (ISettingsPage page in SettingsRegistry.Pages)
            {
                if (Matches(name, Normalize(page.Id)) || Matches(name, Normalize(page.Title))) return page;
                if (page.Aliases == null) continue;
                foreach (string alias in page.Aliases)
                    if (Matches(name, Normalize(alias))) return page;
            }
            return null;
        }

        // «озвучки» ~ «озвучка», «голоса» ~ «голос»: слова совпадают, если одно начинается с другого (от 4 букв)
        // или у них общее начало в 5 и более букв.
        private static bool Matches(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == b) return true;
            if (a.Length >= 4 && b.Length >= 4 &&
                (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal))) return true;
            int common = 0;
            int max = Math.Min(a.Length, b.Length);
            while (common < max && a[common] == b[common]) common++;
            return common >= 5;
        }
    }
}
