using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Разбор правилами фраз про действия на телефоне: «включи фонарик», «сделай громче», «громкость на 50»,
/// «сколько заряда», «найди в интернете …», «открой ютуб», «проложи маршрут до …», «набери номер …», «открой настройки вайфай».
/// Работает с нормализованным текстом (строчные буквы); числа словами разбирает сам: громкость и номера телефонов читаются по-разному. Результат - команда роутера «TOOL|…».
/// </summary>
public static class AgentPhoneParser
{
    private static readonly string[][] SearchPrefixes =
    {
        new[] { "найди", "в", "интернете" }, new[] { "найди", "в", "гугле" }, new[] { "найди", "в", "яндексе" }, new[] { "найди", "в", "сети" },
        new[] { "поищи", "в", "интернете" }, new[] { "поищи", "в", "гугле" }, new[] { "поищи", "в", "яндексе" },
        new[] { "поиск", "в", "интернете" }, new[] { "загугли" }, new[] { "погугли" }
    };

    private static readonly string[][] RoutePrefixes =
    {
        new[] { "проложи", "маршрут" }, new[] { "построй", "маршрут" }, new[] { "как", "доехать" }, new[] { "как", "добраться" },
        new[] { "покажи", "на", "карте" }, new[] { "найди", "на", "карте" }, new[] { "навигация" }, new[] { "маршрут" }
    };

    private static readonly HashSet<string> Prepositions = new HashSet<string> { "до", "к", "в", "на", "по" };
    private static readonly HashSet<string> OpenVerbs = new HashSet<string> { "открой", "открыть", "запусти", "зайди", "перейди", "включи" };

    public static bool TryHandle(string lower, out string command)
    {
        return TryHandle(lower, lower, out command);
    }

    /// <param name="lower">Нормализованный текст, числа словами уже заменены цифрами.</param>
    /// <param name="spoken">Тот же текст, но числа остались словами (так номер телефона разбирается точнее).</param>
    public static bool TryHandle(string lower, string spoken, out string command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(lower)) return false;
        string[] w = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (w.Length == 0) return false;

        // «Добавь задачу включить фонарик», «напомни …»: это задачи и напоминания.
        if (w[0] == "напомни") return false;
        for (int i = 0; i < w.Length && i < 3; i++)
            if (w[i] == "задачу" || w[i] == "задача" || w[i] == "задачи") return false;

        return TryFlashlight(w, out command)
            || TryVolume(w, out command)
            || TryBattery(w, out command)
            || TrySearch(w, out command)
            || TryRoute(w, out command)
            || TryMessage(w, out command)
            || TryDial(spoken, out command)
            || TryPhoneSettings(w, out command)
            || TryOpenApp(w, out command);
    }

    private static bool TryFlashlight(string[] w, out string command)
    {
        command = null;
        if (!HasStem(w, "фонар")) return false;
        if (HasStem(w, "выключ", "отключ", "погас", "убер", "потуш", "гаси")) { command = Tool("flashlight", "state", "off"); return true; }
        if (HasStem(w, "включ", "зажг", "посвет", "свети", "освети")) { command = Tool("flashlight", "state", "on"); return true; }
        return false;
    }

    private static bool TryVolume(string[] w, out string command)
    {
        command = null;
        bool mentions = HasStem(w, "громкост", "звук") || HasAny(w, "громче", "погромче", "тише", "потише");
        if (!mentions) return false;

        int percent = FirstNumber(RuNumbers.Replace(string.Join(" ", w)).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        if (HasAny(w, "громче", "погромче") || HasStem(w, "прибав", "увелич", "повыс", "добав"))
            { command = Tool("volume", "action", "up"); return true; }
        if (HasAny(w, "тише", "потише") || HasStem(w, "убав", "уменьш", "понизь", "снизь"))
            { command = Tool("volume", "action", "down"); return true; }
        if (HasStem(w, "максим") || (HasAny(w, "полную", "полная")))
            { command = Tool("volume", "action", "max"); return true; }
        if (percent >= 0 && HasStem(w, "постав", "установ", "сдела", "громкост", "на"))
            { command = Tool("volume", "action", "set", "percent", percent.ToString()); return true; }
        if (HasAny(w, "без") && HasStem(w, "звук")) { command = Tool("volume", "action", "mute"); return true; }
        if (HasStem(w, "выключ", "отключ", "убер") && HasStem(w, "звук")) { command = Tool("volume", "action", "mute"); return true; }
        if (HasStem(w, "включ") && HasStem(w, "звук")) { command = Tool("volume", "action", "set", "percent", "50"); return true; }
        return false;
    }

    private static bool TryBattery(string[] w, out string command)
    {
        command = null;
        if (!HasStem(w, "заряд", "батаре", "аккумул")) return false;
        if (!HasStem(w, "сколько", "какой", "каков", "уровен", "остал", "проверь", "покаж", "скаж", "процент")) return false;
        command = Tool("battery");
        return true;
    }

    private static bool TrySearch(string[] w, out string command)
    {
        command = null;
        string query = AfterPrefix(w, SearchPrefixes);
        if (string.IsNullOrWhiteSpace(query)) return false;
        command = Tool("web_search", "query", query);
        return true;
    }

    private static bool TryRoute(string[] w, out string command)
    {
        command = null;
        string rest = AfterPrefix(w, RoutePrefixes);
        if (string.IsNullOrWhiteSpace(rest)) return false;
        string[] words = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int start = 0;
        while (start < words.Length && Prepositions.Contains(words[start])) start++;
        if (start >= words.Length) return false;
        command = Tool("route", "place", string.Join(" ", words, start, words.Length - start));
        return true;
    }

    private static readonly Dictionary<string, string> Units = new Dictionary<string, string>
    {
        { "ноль", "0" }, { "нуль", "0" }, { "один", "1" }, { "одна", "1" }, { "два", "2" }, { "две", "2" }, { "три", "3" },
        { "четыре", "4" }, { "пять", "5" }, { "шесть", "6" }, { "семь", "7" }, { "восемь", "8" }, { "девять", "9" }
    };
    private static readonly Dictionary<string, string> Teens = new Dictionary<string, string>
    {
        { "десять", "10" }, { "одиннадцать", "11" }, { "двенадцать", "12" }, { "тринадцать", "13" }, { "четырнадцать", "14" },
        { "пятнадцать", "15" }, { "шестнадцать", "16" }, { "семнадцать", "17" }, { "восемнадцать", "18" }, { "девятнадцать", "19" }
    };
    private static readonly Dictionary<string, string> Tens = new Dictionary<string, string>
    {
        { "двадцать", "2" }, { "тридцать", "3" }, { "сорок", "4" }, { "пятьдесят", "5" }, { "шестьдесят", "6" },
        { "семьдесят", "7" }, { "восемьдесят", "8" }, { "девяносто", "9" }
    };
    private static readonly Dictionary<string, string> Hundreds = new Dictionary<string, string>
    {
        { "сто", "1" }, { "двести", "2" }, { "триста", "3" }, { "четыреста", "4" }, { "пятьсот", "5" },
        { "шестьсот", "6" }, { "семьсот", "7" }, { "восемьсот", "8" }, { "девятьсот", "9" }
    };

    /// <summary>
    /// Звонки и сообщения людям из контактов. Вызывается раньше разбора напоминаний, чтобы «напиши маме, что буду через 5 минут»
    /// не превратилось в таймер. Фразы с «напомни», «через», «будильник», «задачу», «заметку» сюда не попадают.
    /// </summary>
    public static bool TryContactAction(string lower, string spoken, out string command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(lower)) return false;
        string[] w = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (w.Length == 0) return false;
        foreach (string word in w)
            if (word.StartsWith("напомн", StringComparison.Ordinal) || word == "через" || word.StartsWith("таймер", StringComparison.Ordinal) ||
                word.StartsWith("будильн", StringComparison.Ordinal) || word.StartsWith("задач", StringComparison.Ordinal) ||
                word.StartsWith("заметк", StringComparison.Ordinal))
                return false;
        return TryTelegramRead(w, out command) || TryMessage(w, out command) || TryDial(spoken, out command);
    }

    // «Позвони маме», «набери номер плюс семь …», «вызови Ивана Петрова».
    private static bool TryDial(string spoken, out string command)
    {
        command = null;
        string[] w = (spoken ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        // Глагол среди первых слов: «позвони», «набери номер», «можешь набрать», «вызови».
        int verbAt = -1;
        bool dialVerb = false;
        for (int i = 0; i < w.Length && i < 4; i++)
        {
            if (w[i].StartsWith("позвон", StringComparison.Ordinal) || w[i].StartsWith("звони", StringComparison.Ordinal) ||
                w[i].StartsWith("вызов", StringComparison.Ordinal))
            { verbAt = i; break; }
            if (w[i].StartsWith("набер", StringComparison.Ordinal) || w[i].StartsWith("набра", StringComparison.Ordinal))
            { verbAt = i; dialVerb = true; break; }
        }
        if (verbAt < 0) return false;

        string number = SpokenNumber(w);
        if (number.Length >= 5)
        {
            // «Набери номер …» только открывает набор; «позвони …» звонит по выбранному в настройках режиму действий.
            command = dialVerb ? Tool("dial", "number", number) : Tool("call", "number", number);
            return true;
        }
        if (dialVerb && HasStem(w, "номер"))
        {
            command = Tool("dial", "number", number);   // без цифр инструмент ответит, что номер не разобран
            return true;
        }

        var name = new List<string>();
        for (int i = verbAt + 1; i < w.Length && name.Count < 3; i++)
        {
            string word = w[i];
            if (word == "мне" || word == "пожалуйста" || word == "абоненту" || word == "контакту" || word == "номеру" || word == "номер") continue;
            if (word == "на" || word == "по" || word == "и" || word == "что" || word == "чтобы") { if (name.Count > 0) break; continue; }
            name.Add(word);
        }
        command = Tool("call", "rest", string.Join(" ", name.ToArray()));
        return true;
    }

    private static readonly HashSet<string> MessageFillers = new HashSet<string>
    {
        "пожалуйста", "сообщение", "сообщением", "сообщения", "сообщенье", "смску", "текст", "текстом", "для", "следующее", "такое"
    };
    private static readonly HashSet<string> ViaPrepositions = new HashSet<string> { "в", "по", "через", "на" };

    private static string ViaOf(string word)
    {
        if (word.StartsWith("телегр", StringComparison.Ordinal) || word.StartsWith("телег", StringComparison.Ordinal) || word == "тг") return "telegram";
        if (word.StartsWith("ватсап", StringComparison.Ordinal) || word.StartsWith("вацап", StringComparison.Ordinal) ||
            word.StartsWith("вотсап", StringComparison.Ordinal) || word.StartsWith("whatsapp", StringComparison.Ordinal)) return "whatsapp";
        if (word.StartsWith("смс", StringComparison.Ordinal) || word.StartsWith("смск", StringComparison.Ordinal) ||
            word.StartsWith("эсэмэс", StringComparison.Ordinal) || word.StartsWith("sms", StringComparison.Ordinal)) return "sms";
        return null;
    }

    private static readonly HashSet<string> TelegramReadFiller = new HashSet<string>
    {
        "прочитай", "прочти", "покажи", "проверь", "есть", "что", "какие", "какое", "новые", "новое", "нового", "последние", "мне", "пожалуйста",
        "сообщения", "сообщение", "переписку", "переписка", "в", "во", "от", "с", "у", "чате", "чат", "пришло", "пришли", "написала", "написал",
        "непрочитанные", "непрочитанное", "ли", "там", "новых"
    };

    // «Что нового в телеграме», «прочитай сообщения от Маши в телеграм», «что написала мама в телеграм».
    private static bool TryTelegramRead(string[] w, out string command)
    {
        command = null;
        bool telegram = false;
        foreach (string word in w) if (ViaOf(word) == "telegram") telegram = true;
        if (!telegram) return false;

        bool readVerb = false;
        for (int i = 0; i < w.Length && i < 3; i++)
            if (w[i] == "прочитай" || w[i] == "прочти" || w[i] == "покажи" || w[i] == "проверь" || w[i] == "есть" || w[i] == "что" || w[i] == "какие" || w[i] == "нет")
                readVerb = true;
        if (!readVerb) return false;
        if (!HasStem(w, "сообщен", "перепис", "нов", "непрочит", "пришл", "написал", "чат")) return false;

        var name = new List<string>();
        foreach (string word in w)
        {
            if (ViaOf(word) != null || TelegramReadFiller.Contains(word)) continue;
            name.Add(word);
        }
        command = Tool("telegram_read", "rest", string.Join(" ", name.ToArray()));
        return true;
    }

    // «Напиши маме в телеграм, что я задерживаюсь», «отправь смс маме привет», «отправь Ивану сообщение в ватсап …».
    // Нужно слово «смс», название мессенджера или «сообщение»: без них «напиши мне план» - это просьба для языковой модели.
    private static bool TryMessage(string[] w, out string command)
    {
        command = null;
        int verbAt = -1;
        for (int i = 0; i < w.Length && i < 3; i++)
        {
            string word = w[i];
            if (word.StartsWith("напиш", StringComparison.Ordinal) || word.StartsWith("отправ", StringComparison.Ordinal) ||
                word.StartsWith("пошл", StringComparison.Ordinal) || word.StartsWith("послат", StringComparison.Ordinal) ||
                word.StartsWith("скин", StringComparison.Ordinal) || word == "шли" || word.StartsWith("смс", StringComparison.Ordinal) ||
                word.StartsWith("эсэмэс", StringComparison.Ordinal) || word.StartsWith("сообщен", StringComparison.Ordinal))
            { verbAt = i; break; }
        }
        if (verbAt < 0) return false;
        // Письма и стихи - не сообщения человеку из контактов.
        foreach (string word in w)
            if (word.StartsWith("письм", StringComparison.Ordinal) || word.StartsWith("почт", StringComparison.Ordinal) || word.StartsWith("замет", StringComparison.Ordinal))
                return false;

        string via = null;
        bool bareMessage = false;
        for (int i = verbAt; i < w.Length; i++)
        {
            string v = ViaOf(w[i]);
            if (v != null && via == null) via = v;
            if (w[i].StartsWith("сообщен", StringComparison.Ordinal)) bareMessage = true;
        }
        if (via == null && bareMessage) via = "sms";
        if (via == null) return false;

        // Всё после глагола, кроме названия канала (с его предлогом) и служебных слов.
        var tokens = new List<string>();
        int start = verbAt + 1;
        for (int i = start; i < w.Length; i++)
        {
            string word = w[i];
            if (ViaOf(word) != null)
            {
                if (tokens.Count > 0 && ViaPrepositions.Contains(tokens[tokens.Count - 1])) tokens.RemoveAt(tokens.Count - 1);
                continue;
            }
            if (MessageFillers.Contains(word)) continue;
            tokens.Add(word);
        }
        // Предлог в самом конце или начале («… в», «в маме») лишний.
        while (tokens.Count > 0 && ViaPrepositions.Contains(tokens[0])) tokens.RemoveAt(0);

        int sep = tokens.IndexOf("что");
        if (sep < 0) sep = tokens.IndexOf("чтобы");
        if (sep >= 1)
        {
            string name = string.Join(" ", tokens.GetRange(0, sep).ToArray());
            string text = string.Join(" ", tokens.GetRange(sep + 1, tokens.Count - sep - 1).ToArray());
            command = Tool("message", "via", via, "name", name, "text", text);
        }
        else
        {
            command = Tool("message", "via", via, "rest", string.Join(" ", tokens.ToArray()));
        }
        return true;
    }

    // Номер телефона из слов и цифр: «плюс семь девятьсот двенадцать триста сорок пять шестьдесят семь восемьдесят девять».
    private static string SpokenNumber(string[] w)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < w.Length; i++)
        {
            string word = w[i];
            string value;
            bool allDigits = word.Length > 0;
            foreach (char c in word) if (!char.IsDigit(c)) { allDigits = false; break; }

            if (allDigits) sb.Append(word);
            else if (word == "плюс" && sb.Length == 0) sb.Append('+');
            else if (Hundreds.TryGetValue(word, out value))
            {
                sb.Append(value);
                string next = i + 1 < w.Length ? w[i + 1] : null;
                string nv;
                if (next != null && Teens.TryGetValue(next, out nv)) { sb.Append(nv); i++; }
                else if (next != null && Tens.TryGetValue(next, out nv))
                {
                    sb.Append(nv);
                    i++;
                    string unit = i + 1 < w.Length ? w[i + 1] : null;
                    string uv;
                    if (unit != null && Units.TryGetValue(unit, out uv) && uv != "0") { sb.Append(uv); i++; }
                    else sb.Append('0');
                }
                else if (next != null && Units.TryGetValue(next, out nv) && nv != "0") { sb.Append('0').Append(nv); i++; }
                else sb.Append("00");
            }
            else if (Teens.TryGetValue(word, out value)) sb.Append(value);
            else if (Tens.TryGetValue(word, out value))
            {
                sb.Append(value);
                string unit = i + 1 < w.Length ? w[i + 1] : null;
                string uv;
                if (unit != null && Units.TryGetValue(unit, out uv) && uv != "0") { sb.Append(uv); i++; }
                else sb.Append('0');
            }
            else if (Units.TryGetValue(word, out value)) sb.Append(value);
        }
        return sb.ToString();
    }

    private static bool TryPhoneSettings(string[] w, out string command)
    {
        command = null;
        if (!HasStem(w, "настройк")) return false;
        if (HasStem(w, "wi", "вайфай", "вай", "wifi")) { command = Tool("phone_settings", "section", "wifi"); return true; }
        if (HasStem(w, "блютуз", "блютус", "bluetooth", "блутуз")) { command = Tool("phone_settings", "section", "bluetooth"); return true; }
        if (HasStem(w, "телефон")) { command = Tool("phone_settings", "section", "main"); return true; }
        return false;
    }

    // «Открой телеграм», «запусти камеру», «включи калькулятор»: имя приложения - всё после глагола.
    private static bool TryOpenApp(string[] w, out string command)
    {
        command = null;
        int verbAt = OpenVerbs.Contains(w[0]) ? 0 : (w.Length > 1 && OpenVerbs.Contains(w[1]) ? 1 : -1);
        if (verbAt < 0) return false;

        var rest = new List<string>();
        for (int i = verbAt + 1; i < w.Length; i++)
            if (w[i] != "приложение" && w[i] != "программу" && w[i] != "мне" && w[i] != "пожалуйста" && w[i] != "на") rest.Add(w[i]);
        if (rest.Count == 0 || rest.Count > 3) return false;

        // Это другие команды, а не приложения: списки, напоминания, погода, интерфейс агента.
        foreach (string word in rest)
            if (word.StartsWith("задач", StringComparison.Ordinal) || word.StartsWith("напомин", StringComparison.Ordinal) ||
                word.StartsWith("заметк", StringComparison.Ordinal) || word.StartsWith("список", StringComparison.Ordinal) ||
                word.StartsWith("погод", StringComparison.Ordinal) || word.StartsWith("интерфейс", StringComparison.Ordinal) ||
                word == "чат" || word == "фонарик" || word.StartsWith("звук", StringComparison.Ordinal) ||
                word.StartsWith("громк", StringComparison.Ordinal) || word.StartsWith("таймер", StringComparison.Ordinal))
                return false;

        command = Tool("open_app", "name", string.Join(" ", rest));
        return true;
    }

    // ---- помощники ----

    // Номер телефона из речи: цифры и числа словами. «девятьсот двенадцать триста сорок пять шестьдесят семь» -> 9123456 7.
    // Слова одной группы («двадцать три», «девятьсот двенадцать») складываются, соседние группы пишутся подряд.
    private static string SpokenDigits(string[] w)
    {
        var sb = new StringBuilder();
        bool plus = Array.IndexOf(w, "плюс") >= 0;
        int i = 0;
        while (i < w.Length)
        {
            string token = w[i];
            int value;
            if (IsAllDigits(token))
            {
                sb.Append(token);
                i++;
            }
            else if (RuNumbers.TryWord(token, out value))
            {
                int group = value;
                int last = value;
                i++;
                int next;
                while (i < w.Length && RuNumbers.TryWord(w[i], out next) && Follows(last, next))
                {
                    group += next;
                    last = next;
                    i++;
                }
                sb.Append(group);
            }
            else
            {
                i++;
            }
        }
        return (plus ? "+" : string.Empty) + sb;
    }

    // Входит ли следующее число в ту же группу: после сотен - любое меньше сотни, после десятков - единица.
    private static bool Follows(int previous, int next)
    {
        if (previous >= 100) return next >= 1 && next < 100;
        if (previous >= 20 && previous % 10 == 0) return next >= 1 && next <= 9;
        return false;
    }

    private static bool IsAllDigits(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        foreach (char c in token) if (!char.IsDigit(c)) return false;
        return true;
    }

    private static string AfterPrefix(string[] w, string[][] prefixes)
    {
        foreach (string[] prefix in prefixes)
        {
            if (w.Length <= prefix.Length) continue;
            bool match = true;
            for (int i = 0; i < prefix.Length; i++)
                if (w[i] != prefix[i]) { match = false; break; }
            if (match) return string.Join(" ", w, prefix.Length, w.Length - prefix.Length);
        }
        return null;
    }

    private static int FirstNumber(string[] w)
    {
        foreach (string word in w)
        {
            int n;
            if (int.TryParse(word, out n) && n >= 0 && n <= 100) return n;
        }
        return -1;
    }

    private static bool HasAny(string[] w, params string[] options)
    {
        foreach (string word in w)
            foreach (string o in options)
                if (word == o) return true;
        return false;
    }

    private static bool HasStem(string[] w, params string[] stems)
    {
        foreach (string word in w)
            foreach (string s in stems)
                if (word.StartsWith(s, StringComparison.Ordinal)) return true;
        return false;
    }

    private static string Tool(string name, params string[] keyValues)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        for (int i = 0; i + 1 < keyValues.Length; i += 2)
            pairs.Add(new KeyValuePair<string, string>(keyValues[i], keyValues[i + 1]));
        return "TOOL|" + name + "|" + AgentToolRegistry.BuildQuery(pairs);
    }
}
