using System.Collections.Generic;
using System;
using System.Text;
using Barabashka.MarketplaceProviders;
using Barabashka.Contracts;

namespace Barabashka.Core
{
    /// <summary>
    /// Разбор голосовых команд для маркетплейсов:
    /// «найди на Озоне ...», «найди на Вайлдберриз ...», «поищи на маркетплейсе ...»,
    /// «открой ссылку на товар», «открой товар ...»
    /// </summary>
    public static class MarketplaceParser
    {
        private static readonly string[] SearchVerbs = { "найди", "найти", "поищи", "поискать", "покажи", "показать" };
        private static readonly string[] OpenVerbs = { "открой", "открыть", "перейди", "перейти", "запусти", "запустить" };
        private static readonly string[] MarketplaceWords = { "маркетплейс", "маркет", "магазин" };

        /// <summary>Пытается разобрать команду поиска товара на маркетплейсе.</summary>
        public static bool TrySearch(string lower, out string command)
        {
            command = null;
            if (string.IsNullOrWhiteSpace(lower)) return false;

            string providerId = DetectProvider(lower);

            foreach (string verb in SearchVerbs)
            {
                int idx = lower.IndexOf(verb, StringComparison.Ordinal);
                if (idx < 0) continue;

                string afterVerb = lower.Substring(idx + verb.Length).Trim();
                string query = CleanQuery(afterVerb, providerId);
                if (string.IsNullOrWhiteSpace(query)) continue;

                string provider = providerId == "ask" ? "auto" : (providerId ?? "auto");
                command = Tool("marketplace_search",
                    new KeyValuePair<string, string>("query", query),
                    new KeyValuePair<string, string>("provider", provider));
                return true;
            }
            return false;
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

        private static string DetectProvider(string text)
        {
            if (ContainsAny(text, "озон", "ozon", "на озоне", "в озоне")) return "ozon";
            if (ContainsAny(text, "вайлдберриз", "wildberries", "вб", "wb",
                "на вайлдберриз", "в вайлдберриз", "на wildberries", "в wildberries")) return "wildberries";
            if (ContainsAny(text, MarketplaceWords)) return "ask";
            return null;
        }

        private static string CleanQuery(string query, string providerId)
        {
            string[] toRemove = { "на озоне", "в озоне", "на вайлдберриз", "в вайлдберриз",
                "на wildberries", "в wildberries", "на маркетплейсе", "в маркетплейсе",
                "на маркете", "в маркете" };
            foreach (string remove in toRemove)
                query = query.Replace(remove, string.Empty);

            string[] prefixes = { "на ", "в ", "по ", "для ", "у " };
            foreach (string prefix in prefixes)
            {
                if (query.StartsWith(prefix, StringComparison.Ordinal))
                {
                    query = query.Substring(prefix.Length);
                    break;
                }
            }
            return query.Trim();
        }

        private static bool ContainsAny(string text, params string[] fragments)
        {
            foreach (string fragment in fragments)
                if (text.Contains(fragment)) return true;
            return false;
        }

        private static string Tool(string name, params KeyValuePair<string, string>[] pairs)
        {
            return "TOOL|" + name + "|" + AgentToolRegistry.BuildQuery(pairs);
        }
    }
}