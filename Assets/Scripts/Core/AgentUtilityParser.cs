using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Разбор правилами фраз про погоду и заметки:
/// «какая погода», «погода в Казани завтра», «мой город Москва»,
/// «запомни, что пароль в ящике», «покажи заметки», «найди в заметках пароль», «удали заметку 2».
/// Работает с нормализованным текстом (строчные буквы, слова через пробел). Результат - команда роутера «TOOL|…».
/// </summary>
public static class AgentUtilityParser
{
    private static readonly HashSet<string> CancelVerbs = new HashSet<string> { "удали", "удалить", "убери", "убрать", "сотри", "очисти", "очистить" };
    private static readonly HashSet<string> ListWords = new HashSet<string> { "покажи", "показать", "список", "какие", "мои", "что", "выведи", "перечисли", "отобрази", "прочитай" };
    private static readonly HashSet<string> CityStop = new HashSet<string> { "сегодня", "завтра", "послезавтра", "сейчас", "на", "будет", "какая", "какой", "как", "в", "неделю", "недели", "выходные", "по", "и" };

    public static bool TryHandle(string lower, out string command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(lower)) return false;
        string[] w = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // «Добавь задачу проверить погоду», «напомни …»: это задачи и напоминания, а не погода и заметки.
        if (w.Length > 0 && w[0] == "напомни") return false;
        for (int i = 0; i < w.Length && i < 3; i++)
            if (w[i] == "задачу" || w[i] == "задача" || w[i] == "задачи") return false;

        // Город по умолчанию: «мой город Москва», «я живу в Казани», «город по умолчанию Сочи».
        string home = AfterPhrase(w, "мой", "город");
        if (home == null) home = AfterPhrase(w, "город", "по", "умолчанию");
        if (home == null) home = AfterPhrase(w, "я", "живу", "в");
        if (home == null) home = AfterPhrase(w, "запомни", "город");
        if (home != null)
        {
            command = Tool("set_city", new KeyValuePair<string, string>("city", home));
            return true;
        }

        bool weather = false;
        foreach (string word in w)
        {
            if (word.StartsWith("погод", StringComparison.Ordinal)) weather = true;
            if ((word.StartsWith("дожд", StringComparison.Ordinal) || word == "снег") && Has(w, "будет")) weather = true;
        }
        if (weather) return BuildWeather(w, out command);

        return TryNotes(w, out command);
    }

    // ---- погода ----

    private static bool BuildWeather(string[] w, out string command)
    {
        string day = "today";
        if (Has(w, "завтра")) day = "tomorrow";
        else if (Has(w, "послезавтра")) day = "day_after";
        else if (Has(w, "неделю") || Has(w, "недели") || Has(w, "выходные") || Has(w, "неделе")) day = "week";

        // Город: слова после «в» / «во» до слов-ограничителей: «погода в нижнем новгороде завтра».
        var city = new List<string>();
        for (int i = 0; i < w.Length - 1; i++)
        {
            if (w[i] != "в" && w[i] != "во") continue;
            for (int j = i + 1; j < w.Length && city.Count < 3; j++)
            {
                if (CityStop.Contains(w[j])) break;
                city.Add(w[j]);
            }
            break;
        }

        // «Погода в моём городе», «здесь»: город не назван, берём город по умолчанию.
        if (city.Count > 0 && (city[0] == "моем" || city[0] == "моём" || city[0] == "нашем" || city[0] == "этом" ||
                               city[0] == "здесь" || city[0] == "тут"))
            city.Clear();

        var pairs = new List<KeyValuePair<string, string>>();
        if (city.Count > 0) pairs.Add(new KeyValuePair<string, string>("city", string.Join(" ", city)));
        pairs.Add(new KeyValuePair<string, string>("day", day));
        command = Tool("weather", pairs.ToArray());
        return true;
    }

    // ---- заметки ----

    private static bool TryNotes(string[] w, out string command)
    {
        command = null;
        bool noteWord = false;
        foreach (string word in w) if (word.StartsWith("заметк", StringComparison.Ordinal)) noteWord = true;

        // Удаление: «удали заметку 2», «удали все заметки».
        if (noteWord && AnyIn(w, CancelVerbs))
        {
            string spec = Numbers(w);
            command = spec == null
                ? Tool("note_delete")
                : Tool("note_delete", new KeyValuePair<string, string>("numbers", spec));
            return true;
        }

        // Поиск: «найди в заметках пароль», «что я записывал про дачу».
        int find = IndexOf(w, "найди");
        if (find >= 0 && noteWord)
        {
            string query = JoinAfterNoise(w, find + 1, "в", "заметках", "заметке", "среди", "заметки", "про", "о");
            command = query.Length > 0
                ? Tool("note_list", new KeyValuePair<string, string>("query", query))
                : Tool("note_list");
            return true;
        }
        int recorded = IndexOf(w, "записывал");
        if (recorded < 0) recorded = IndexOf(w, "записывала");
        if (recorded < 0) recorded = IndexOf(w, "запоминал");
        if (recorded >= 0 && Has(w, "я") && Has(w, "про"))
        {
            string query = JoinAfterNoise(w, IndexOf(w, "про") + 1);
            command = query.Length > 0
                ? Tool("note_list", new KeyValuePair<string, string>("query", query))
                : Tool("note_list");
            return true;
        }

        // Список: «покажи заметки», «мои заметки», «что я записывал».
        if ((noteWord && AnyIn(w, ListWords)) || (recorded >= 0 && Has(w, "я")))
        {
            command = Tool("note_list");
            return true;
        }

        // Запись: «запомни, что …», «запиши заметку …», «новая заметка …», «добавь заметку …».
        int start = -1;
        if (w.Length >= 2 && w[0] == "запомни") start = 1;
        else if (w.Length >= 3 && (w[0] == "запиши" || w[0] == "добавь" || w[0] == "создай" || w[0] == "сделай") && w[1].StartsWith("заметк", StringComparison.Ordinal)) start = 2;
        else if (w.Length >= 3 && w[0] == "новая" && w[1].StartsWith("заметк", StringComparison.Ordinal)) start = 2;
        else if (w.Length >= 2 && w[0].StartsWith("заметк", StringComparison.Ordinal)) start = 1;

        if (start >= 0)
        {
            string text = JoinAfterNoise(w, start, "что", "мне", "пожалуйста");
            if (text.Length == 0) return false;
            command = Tool("note_add", new KeyValuePair<string, string>("text", Capitalize(text)));
            return true;
        }

        return false;
    }

    // ---- общее ----

    private static string Tool(string name, params KeyValuePair<string, string>[] pairs)
    {
        return "TOOL|" + name + "|" + AgentToolRegistry.BuildQuery(pairs);
    }

    private static bool Has(string[] w, string word)
    {
        return Array.IndexOf(w, word) >= 0;
    }

    private static int IndexOf(string[] w, string word)
    {
        return Array.IndexOf(w, word);
    }

    private static bool AnyIn(string[] w, HashSet<string> set)
    {
        foreach (string word in w) if (set.Contains(word)) return true;
        return false;
    }

    // Слова после заданной цепочки слов («мой город …»); null, если цепочки нет или после неё пусто.
    private static string AfterPhrase(string[] w, params string[] phrase)
    {
        for (int i = 0; i + phrase.Length < w.Length; i++)
        {
            bool match = true;
            for (int k = 0; k < phrase.Length; k++)
                if (w[i + k] != phrase[k]) { match = false; break; }
            if (!match) continue;

            var rest = new List<string>();
            for (int j = i + phrase.Length; j < w.Length && rest.Count < 3; j++) rest.Add(w[j]);
            return rest.Count == 0 ? null : string.Join(" ", rest);
        }
        return null;
    }

    private static string JoinAfterNoise(string[] w, int from, params string[] noise)
    {
        var kept = new List<string>();
        for (int i = Math.Max(0, from); i < w.Length; i++) kept.Add(w[i]);
        while (kept.Count > 0 && Array.IndexOf(noise, kept[0]) >= 0) kept.RemoveAt(0);
        return string.Join(" ", kept).Trim();
    }

    // «2», «1 и 3», «вторую», «все» -> «2» / «1,3» / «ALL»; нет номеров -> null.
    private static string Numbers(string[] w)
    {
        string joined = RuNumbers.Replace(string.Join(" ", w));
        foreach (string word in joined.Split(' '))
            if (word == "все" || word == "всё" || word == "всех") return "ALL";
        return IsolatedBrain.ExtractTaskSpec(joined);
    }

    private static string Capitalize(string text)
    {
        return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
