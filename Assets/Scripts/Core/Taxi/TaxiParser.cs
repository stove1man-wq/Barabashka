using System;
using System.Collections.Generic;
using System.Text;

namespace Barabashka.Core
{
    /// <summary>
    /// Разбор фраз про такси. Работает с нормализованным текстом (строчные буквы, слова через пробел).
    /// Вызов: «вызови такси до вокзала», «закажи такси домой», «такси на работу», «мне нужно такси до аэропорта»,
    /// «вызови яндекс го до тверской 1», «вызови ситимобил». Без адреса открывается приложение оператора.
    /// Адреса: «мой дом тверская 1», «запомни работу ленина 5», «адрес дома ...».
    /// Фразы «открой яндекс го» / «открой яндекс такси» сюда не попадают: это открытие приложения (PhoneActions).
    /// Результат - команда роутера «TOOL|taxi_call|to=...&amp;provider=...» или «TOOL|taxi_set_place|slot=home&amp;address=...».
    /// </summary>
    public static class TaxiParser
    {
        private static readonly string[] CallVerbs = { "вызови", "вызовите", "вызвать", "вызывай", "закажи", "закажите", "заказать", "заказывай" };
        private static readonly string[] OpenVerbs = { "открой", "открыть", "запусти", "запустить", "включи" };
        private static readonly string[] Prepositions = { "до", "в", "во", "на", "к", "ко" };
        private static readonly string[] Fillers = { "пожалуйста", "мне", "нам", "срочно", "сейчас", "быстро", "скорее" };
        private static readonly string[] HomeWords = { "домой", "дом", "дома", "дому" };
        private static readonly string[] WorkWords = { "работа", "работу", "работы", "работе", "офис", "офису", "офиса" };
        private static readonly string[] Skip = { "это", "на", "по", "адресу", "в", "во" };

        // Фразы с этими словами - не про такси сейчас (задача, заметка, напоминание, будильник).
        private static readonly string[] ExcludedPrefixes =
        {
            "задач", "напомни", "заметк", "будильник", "разбуди",
            // Звонки и сообщения людям: слово «такси» там часть текста, а не просьба («напиши маме, что еду на такси»).
            "позвон", "звони", "набер", "набра", "напиш", "отправ", "сообщен", "смс", "скажи", "передай"
        };

        private static readonly string[][] HomeKeys =
        {
            new[] { "мой", "дом" }, new[] { "запомни", "дом" }, new[] { "запомни", "адрес", "дома" },
            new[] { "мой", "адрес", "дома" }, new[] { "адрес", "дома" }, new[] { "дом", "это" }
        };

        private static readonly string[][] WorkKeys =
        {
            new[] { "моя", "работа" }, new[] { "запомни", "работу" }, new[] { "запомни", "адрес", "работы" },
            new[] { "мой", "адрес", "работы" }, new[] { "адрес", "работы" }, new[] { "работа", "это" }
        };

        // Время суток в «адресе»: «такси в 8 утра» - заказ на время мы не делаем, адреса тут нет.
        private static readonly string[] TimeWords = { "утра", "вечера", "ночи", "дня", "час", "часа", "часов", "минут", "минуты", "минуту" };

        public static bool TryHandle(string lower, out string command)
        {
            command = null;
            if (string.IsNullOrWhiteSpace(lower)) return false;
            string[] words = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return false;

            foreach (string w in words)
                foreach (string prefix in ExcludedPrefixes)
                    if (w.StartsWith(prefix, StringComparison.Ordinal)) return false;

            return TrySetPlace(words, out command) || TryCall(words, out command);
        }

        // ---- адреса дома и работы ----

        private static bool TrySetPlace(string[] words, out string command)
        {
            command = null;
            string slot = null;
            int used = 0;
            foreach (string[] key in HomeKeys) if (StartsWith(words, key) && key.Length > used) { slot = "home"; used = key.Length; }
            foreach (string[] key in WorkKeys) if (StartsWith(words, key) && key.Length > used) { slot = "work"; used = key.Length; }
            if (slot == null) return false;

            int start = used;
            while (start < words.Length && Array.IndexOf(Skip, words[start]) >= 0) start++;
            if (start >= words.Length) return false;

            List<string> rest = new List<string>(words).GetRange(start, words.Length - start);
            // «Запомни дом ленина» - явная просьба, достаточно любого адреса. «Моя работа скучная» - не адрес.
            if (words[0] != "запомни" && !LooksLikeAddress(rest)) return false;

            string address = string.Join(" ", rest.ToArray());
            command = Tool("taxi_set_place", Pair("slot", slot), Pair("address", address));
            return true;
        }

        private static bool StartsWith(string[] words, string[] key)
        {
            if (words.Length < key.Length) return false;
            for (int i = 0; i < key.Length; i++)
                if (words[i] != key[i]) return false;
            return true;
        }

        // ---- вызов такси ----

        private static bool TryCall(string[] words, out string command)
        {
            command = null;
            int taxiAt = -1;
            for (int i = 0; i < words.Length; i++)
                if (words[i] == "такси") { taxiAt = i; break; }

            string provider;
            int providerEnd;
            DetectProvider(words, out provider, out providerEnd);

            if (taxiAt < 0 && provider == null) return false;

            bool callVerb = HasAny(words, CallVerbs);
            bool openVerb = HasAny(words, OpenVerbs);

            // «Открой яндекс го» и «открой яндекс такси» остаются открытием приложения; «открой такси» - открытие оператора по умолчанию.
            if (openVerb && provider != null) return false;
            // Название оператора без слова «такси» понимаем только как заказ: «вызови ситимобил», «яндекс го до вокзала».
            if (taxiAt < 0 && !callVerb && IndexOfPreposition(words, providerEnd + 1) < 0) return false;

            int anchor = Math.Max(taxiAt, providerEnd);
            string to = ExtractDestination(words, anchor);

            var pairs = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrEmpty(to)) pairs.Add(Pair("to", to));
            if (provider != null) pairs.Add(Pair("provider", provider));
            command = Tool("taxi_call", pairs.ToArray());
            return true;
        }

        private static string ExtractDestination(string[] words, int anchor)
        {
            // «такси домой», «такси на работу»: дом и работа названы без адреса.
            var after = new List<string>();
            for (int i = anchor + 1; i < words.Length; i++)
                if (Array.IndexOf(Fillers, words[i]) < 0) after.Add(words[i]);

            int prep = -1;
            for (int i = 0; i < after.Count; i++)
            {
                if (Array.IndexOf(Prepositions, after[i]) >= 0 || after[i] == "адресу" || after[i] == "адрес") { prep = i; break; }
            }

            List<string> dest;
            if (prep >= 0) dest = after.GetRange(prep + 1, after.Count - prep - 1);
            else dest = after;

            // Слово «по» перед «адресу» («такси по адресу ...»).
            while (dest.Count > 0 && (dest[0] == "по" || dest[0] == "адресу" || dest[0] == "адрес")) dest.RemoveAt(0);
            if (dest.Count == 0) return string.Empty;
            foreach (string w in dest)
                if (Array.IndexOf(TimeWords, w) >= 0) return string.Empty;

            if (dest.Count == 1 && Array.IndexOf(HomeWords, dest[0]) >= 0) return "home";
            if (dest.Count == 1 && Array.IndexOf(WorkWords, dest[0]) >= 0) return "work";
            if (prep < 0 && dest.Count > 0 && !LooksLikeAddress(dest)) return string.Empty;
            return string.Join(" ", dest.ToArray());
        }

        // Без предлога адресом считаем только то, что содержит цифру или несколько слов; одиночное слово вроде «пожалуйста» - не адрес.
        private static bool LooksLikeAddress(List<string> dest)
        {
            if (dest.Count >= 2) return true;
            foreach (char c in dest[0]) if (char.IsDigit(c)) return true;
            return false;
        }

        private static int IndexOfPreposition(string[] words, int from)
        {
            for (int i = Math.Max(0, from); i < words.Length; i++)
                if (words[i] == "до") return i;
            return -1;
        }

        private static void DetectProvider(string[] words, out string provider, out int endIndex)
        {
            provider = null;
            endIndex = -1;
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                string next = i + 1 < words.Length ? words[i + 1] : string.Empty;

                if ((w == "яндекс" || w == "yandex") && (next == "го" || next == "гоу" || next == "go" || next == "такси"))
                {
                    provider = "yandex-go"; endIndex = i + 1; return;
                }
                if (w == "яндекс" && next.Length == 0) continue;
                if (w.StartsWith("ситимоб", StringComparison.Ordinal) || w == "citymobil") { provider = "citymobil"; endIndex = i; return; }
                if (w == "максим" || w == "maxim") { provider = "maxim"; endIndex = i; return; }
                if (w == "индрайв" || w == "индрайвер" || w == "indrive" || w == "indriver") { provider = "indrive"; endIndex = i; return; }
            }
        }

        private static bool HasAny(string[] words, string[] set)
        {
            foreach (string w in words)
                if (Array.IndexOf(set, w) >= 0) return true;
            return false;
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }

        // Тот же формат, что у AgentToolRegistry.BuildQuery: «TOOL|имя|ключ=значение&ключ=значение», значения закодированы.
        private static string Tool(string name, params KeyValuePair<string, string>[] pairs)
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in pairs)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(pair.Key)).Append('=').Append(Uri.EscapeDataString(pair.Value ?? string.Empty));
            }
            return "TOOL|" + name + "|" + sb;
        }
    }
}
