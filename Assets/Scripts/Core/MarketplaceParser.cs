using System;
using System.Collections.Generic;
using System.Text;
using Barabashka.Contracts;

namespace Barabashka.Core
{
    /// <summary>
    /// Разбор голосовых команд для маркетплейсов:
    /// «найди на Озоне ...», «найди стул на Вайлдберриз», «поищи на маркетплейсе ...»,
    /// «открой ссылку на товар», «открой товар ...».
    /// Поиск срабатывает ТОЛЬКО если в фразе назван магазин (или слово «маркетплейс»):
    /// иначе «найди все приложения на телефоне» уходило бы в поиск товаров.
    /// «Открой Вайлдберриз / Озон» сюда не попадает: это открытие приложения (см. алиасы в PhoneActions).
    /// </summary>
    public static class MarketplaceParser
    {
        private static readonly string[] SearchVerbs =
        {
            "найди", "найдите", "найти", "поищи", "поищите", "поискать", "ищи", "искать", "покажи", "показать"
        };
        private static readonly string[] OpenVerbs = { "открой", "открыть", "перейди", "перейти", "запусти", "запустить" };
        private static readonly string[] Prepositions = { "на", "в", "во", "по", "у", "для", "с", "из" };
        private static readonly string[] Fillers = { "мне", "пожалуйста" };
        private static readonly string[] AppWords = { "приложение", "приложения", "приложению", "программа", "программу" };
        private static readonly string[] OzonWords = { "озон", "озоне", "озона", "озону", "озоном", "ozon" };
        private static readonly string[] WildberriesPrefixes =
        {
            "вайлдбер", "вайлбер", "валдбер", "валбер", "вилдбер", "вайлдбир", "вайлбир", "вайлдбр", "wildber", "вайлд"
        };

        /// <summary>Пытается разобрать команду поиска товара на маркетплейсе.</summary>
        public static bool TrySearch(string lower, out string command)
        {
            command = null;
            if (string.IsNullOrWhiteSpace(lower)) return false;

            string[] words = Split(lower);
            int verbAt = -1;
            for (int i = 0; i < words.Length; i++)
            {
                if (Array.IndexOf(SearchVerbs, words[i]) >= 0) { verbAt = i; break; }
            }
            if (verbAt < 0) return false;

            // Название магазина ищем во всей фразе («на озоне найди стул» тоже работает), запрос - слова после глагола.
            string providerId = null;
            var query = new List<string>();
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];

                // «вайлд берис», сказанное с паузой: вторую половину названия пропускаем.
                if (i > 0 && words[i - 1].StartsWith("вайлд", StringComparison.Ordinal) && word.StartsWith("бер", StringComparison.Ordinal))
                    continue;

                string id = ProviderOfWord(word);
                if (id != null)
                {
                    // Конкретный магазин важнее слова «маркетплейс».
                    if (providerId == null || providerId == "ask") providerId = id;
                    // Предлог перед названием («на озоне») в запрос не берём.
                    if (i > verbAt && query.Count > 0 && Array.IndexOf(Prepositions, query[query.Count - 1]) >= 0)
                        query.RemoveAt(query.Count - 1);
                    continue;
                }

                if (i > verbAt && Array.IndexOf(Fillers, word) < 0) query.Add(word);
            }

            if (providerId == null) return false;

            // «Найди приложение Озон» - это про приложение, а не про товар.
            bool onlyAppWords = true;
            foreach (string word in query)
                if (Array.IndexOf(AppWords, word) < 0) { onlyAppWords = false; break; }
            if (query.Count == 0 || onlyAppWords) return false;

            string provider = providerId == "ask" ? "auto" : providerId;
            command = Tool("marketplace_search",
                new KeyValuePair<string, string>("query", string.Join(" ", query.ToArray())),
                new KeyValuePair<string, string>("provider", provider));
            return true;
        }

        /// <summary>Пытается разобрать команду открытия ссылки на товар.</summary>
        public static bool TryOpen(string lower, out string command)
        {
            command = null;
            if (string.IsNullOrWhiteSpace(lower)) return false;

            foreach (string verb in OpenVerbs)
            {
                int idx = lower.IndexOf(verb, StringComparison.Ordinal);
                if (idx < 0) continue;

                string afterVerb = lower.Substring(idx + verb.Length).Trim();

                if (afterVerb.StartsWith("ссылк", StringComparison.Ordinal) ||
                    afterVerb.StartsWith("товар", StringComparison.Ordinal) ||
                    afterVerb.StartsWith("карточк", StringComparison.Ordinal))
                {
                    command = Tool("marketplace_open");
                    return true;
                }

                int httpIdx = afterVerb.IndexOf("http", StringComparison.OrdinalIgnoreCase);
                if (httpIdx >= 0)
                {
                    string possibleUrl = afterVerb.Substring(httpIdx).Trim();
                    int end = possibleUrl.Length;
                    for (int i = 0; i < possibleUrl.Length; i++)
                    {
                        char c = possibleUrl[i];
                        if (char.IsWhiteSpace(c) || c == '.' || c == ',' || c == ';' || c == '?' || c == '!')
                        {
                            end = i;
                            break;
                        }
                    }
                    string url = possibleUrl.Substring(0, end);
                    command = Tool("marketplace_open", new KeyValuePair<string, string>("url", url));
                    return true;
                }
            }
            return false;
        }

        /// <summary>Какому магазину принадлежит слово: ozon, wildberries, ask («маркетплейс») или null.</summary>
        private static string ProviderOfWord(string word)
        {
            if (Array.IndexOf(OzonWords, word) >= 0) return "ozon";
            if (word == "вб" || word == "wb") return "wildberries";
            foreach (string prefix in WildberriesPrefixes)
                if (word.StartsWith(prefix, StringComparison.Ordinal)) return "wildberries";
            if (word.StartsWith("маркетплейс", StringComparison.Ordinal) || word == "маркет" || word == "маркете" || word == "маркета")
                return "ask";
            return null;
        }

        private static string[] Split(string text)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            foreach (char c in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (sb.Length > 0) { list.Add(sb.ToString()); sb.Length = 0; }
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list.ToArray();
        }

        private static string Tool(string name, params KeyValuePair<string, string>[] pairs)
        {
            return "TOOL|" + name + "|" + AgentToolRegistry.BuildQuery(pairs);
        }
    }
}
