using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Фразы про документы: «документы», «мои документы», «найди документ договор», «открой документ договор», «выбери документ».
/// Разбираются раньше «открой приложение», иначе «открой документы» запускало приложение «Мои документы».
/// </summary>
public static class DocsParser
{
    private static readonly string[] AddVerbs = { "выбер", "добав", "загруз", "прикреп", "принес", "импорт" };
    private static readonly string[] OpenVerbs = { "откр", "покаж", "запуст", "прочита", "дай" };
    private static readonly string[] FindVerbs = { "найд", "найт", "поищ", "ищи", "поиск" };
    // Фразы про задачи, заметки и сообщения «про документы» - не наши.
    private static readonly string[] Blockers =
        { "задач", "напомн", "заметк", "запиш", "созда", "удал", "отправ", "напиш", "переимен", "купи", "покуп", "перевед", "сохран" };
    private static readonly HashSet<string> Filler = new HashSet<string>
    {
        "мои", "мой", "моих", "мне", "все", "всех", "пожалуйста", "список", "списке", "на", "в", "из", "телефоне", "телефон",
        "файл", "файлы", "по", "названию", "названием", "под", "с", "именем", "называется", "и", "у", "меня", "есть", "какие", "какой", "мое", "моё"
    };

    public static bool TryHandle(string lower, out string command)
    {
        command = null;
        string[] w = Words(lower);
        if (w.Length == 0 || w.Length > 9) return false;

        int di = -1;
        for (int i = 0; i < w.Length; i++)
        {
            if (w[i].StartsWith("документ", StringComparison.Ordinal) && !w[i].StartsWith("документальн", StringComparison.Ordinal)) { di = i; break; }
        }
        if (di < 0 || HasStem(w, Blockers)) return false;

        if (HasStem(w, AddVerbs))
        {
            command = Tool("docs_add", null, null);
            return true;
        }

        string suffix = w[di].Substring("документ".Length);
        bool plural = suffix == "ы" || suffix == "ов" || suffix == "ам" || suffix == "ах" || suffix == "ами";
        bool find = HasStem(w, FindVerbs);
        bool open = HasStem(w, OpenVerbs);

        var nameWords = new List<string>();
        for (int i = 0; i < w.Length; i++)
        {
            if (i == di || Filler.Contains(w[i]) || StartsWithAny(w[i], OpenVerbs) || StartsWithAny(w[i], FindVerbs)) continue;
            nameWords.Add(StemTrim(w[i]));
        }
        string name = string.Join(" ", nameWords.ToArray());

        if (name.Length > 0)
        {
            if (find || plural) command = Tool("docs_list", "query", name);
            else command = Tool("docs_open", "name", name);
            return true;
        }
        if (!plural && open) command = Tool("docs_open", "name", string.Empty);
        else command = Tool("docs_list", null, null);
        return true;
    }

    // «договора» -> «договор»: названия файлов ищутся по части слова.
    private static string StemTrim(string word)
    {
        if (word.Length < 7) return word;
        char last = word[word.Length - 1];
        return "аеуыиюя".IndexOf(last) >= 0 ? word.Substring(0, word.Length - 1) : word;
    }

    private static string Tool(string name, string key, string value)
    {
        if (key == null) return "TOOL|" + name + "|";
        return "TOOL|" + name + "|" + AgentToolRegistry.BuildQuery(new[] { new KeyValuePair<string, string>(key, value) });
    }

    private static bool StartsWithAny(string word, string[] stems)
    {
        foreach (string stem in stems)
            if (word.StartsWith(stem, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool HasStem(string[] words, string[] stems)
    {
        foreach (string word in words)
            if (StartsWithAny(word, stems)) return true;
        return false;
    }

    private static string[] Words(string text)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        foreach (char c in (text ?? string.Empty).ToLowerInvariant())
        {
            char ch = c == 'ё' ? 'е' : c;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0) { list.Add(sb.ToString()); sb.Length = 0; }
        }
        if (sb.Length > 0) list.Add(sb.ToString());
        return list.ToArray();
    }
}
