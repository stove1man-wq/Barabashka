using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

public class IsolatedBrain : IAgentBrain
{
    private static readonly Dictionary<string, int> OrdinalNumbers = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        { "первый", 1 }, { "первая", 1 }, { "первую", 1 }, { "первой", 1 }, { "первого", 1 }, { "первом", 1 },
        { "второй", 2 }, { "вторую", 2 }, { "второе", 2 }, { "второго", 2 },
        { "третий", 3 }, { "третья", 3 }, { "третью", 3 }, { "третьей", 3 }, { "третьего", 3 },
        { "четвертый", 4 }, { "четвертая", 4 }, { "четвертую", 4 }, { "четвертой", 4 },
        { "пятый", 5 }, { "пятая", 5 }, { "пятую", 5 }, { "пятой", 5 },
        { "шестой", 6 }, { "шестую", 6 }, { "шестого", 6 },
        { "седьмой", 7 }, { "седьмую", 7 }, { "седьмого", 7 },
        { "восьмой", 8 }, { "восьмую", 8 }, { "восьмого", 8 },
        { "девятый", 9 }, { "девятая", 9 }, { "девятую", 9 }, { "девятой", 9 },
        { "десятый", 10 }, { "десятая", 10 }, { "десятую", 10 }, { "десятой", 10 }
    };

    public Task<string> ProcessMessageAsync(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return Task.FromResult("Я вас не расслышал. Повторите, пожалуйста.");

        string text = userText.Trim();
        string lower = Normalize(text);

        string title = ExtractTaskTitle(text, lower);
        if (title != null)
            return Task.FromResult("COMMAND:TASK_ADD|" + Uri.EscapeDataString(title));

        if (IsTaskListRequest(lower))
            return Task.FromResult("COMMAND:TASK_LIST");

        if (MentionsTask(lower) && ContainsAny(lower, "удал", "убер", "убрать", "сотри", "удалить"))
        {
            string number = ExtractTaskNumber(lower);
            return Task.FromResult(number == null
                ? "Не понял, какую задачу удалить. Назови её номер или попроси показать список задач."
                : "COMMAND:TASK_DELETE|" + number);
        }

        if (MentionsTask(lower) && ContainsAny(lower, "заверш", "выполн", "законч", "готов", "отметь", "сделал", "сделана", "сделано"))
        {
            string number = ExtractTaskNumber(lower);
            return Task.FromResult(number == null
                ? "Не понял, какую задачу отметить выполненной. Назови её номер или попроси показать список задач."
                : "COMMAND:TASK_DONE|" + number);
        }

        if (lower.Contains("привет") || lower.Contains("здравствуй"))
            return Task.FromResult("Приветствую! Я ваш независимый ИИ-агент. Рад помочь!");

        if (lower.Contains("погод"))
            return Task.FromResult("Сегодня отличная погода для разработки локальных систем!");

        if (lower.Contains("время") || lower.Contains("час"))
            return Task.FromResult("COMMAND:TIME");

        if (lower.Contains("браузер") || lower.Contains("гитхаб"))
            return Task.FromResult("COMMAND:OPEN_URL|https://github.com");

        return Task.FromResult("Пока я умею отвечать на базовые запросы и управлять локальным списком задач. Можно сказать: «добавь задачу …», «покажи мои задачи», «удали первую задачу» или «отметь задачу 2 выполненной».");
    }

    private static string Normalize(string value)
    {
        var result = new StringBuilder(value.Length);
        bool previousSpace = true;
        foreach (char c in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                result.Append(c);
                previousSpace = false;
            }
            else if (!previousSpace)
            {
                result.Append(' ');
                previousSpace = true;
            }
        }
        return result.ToString().Trim();
    }

    private static string ExtractTaskTitle(string original, string lower)
    {
        string[] prefixes =
        {
            "добавь задачу ", "добавить задачу ", "создай задачу ", "создать задачу ",
            "запиши задачу ", "поставь задачу ", "напомни мне сделать "
        };

        foreach (string prefix in prefixes)
        {
            if (lower.StartsWith(prefix, StringComparison.Ordinal))
            {
                string title = original.Substring(prefix.Length).Trim().TrimEnd('.', '!', '?');
                return title.Length == 0 ? null : title;
            }
        }
        return null;
    }

    private static bool IsTaskListRequest(string text)
    {
        bool mentionsTasks = ContainsAny(text, "задач", "дела", "список дел", "что осталось сделать");
        if (!mentionsTasks) return false;

        return ContainsAny(text,
            "покажи", "показать", "список", "какие", "какая", "мои", "что у меня",
            "что осталось", "что нужно", "что надо", "выведи", "перечисли", "посмотреть",
            "отобрази", "есть ли", "что по");
    }

    private static bool MentionsTask(string text)
    {
        return ContainsAny(text, "задач", "дело", "дела", "делу", "задачу", "задаче");
    }

    private static string ExtractTaskNumber(string normalizedText)
    {
        string[] words = normalizedText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // First look for a spoken ordinal: «первую задачу удали», «удали вторую задачу».
        foreach (string word in words)
        {
            int ordinal;
            if (OrdinalNumbers.TryGetValue(word, out ordinal))
                return ordinal.ToString();
        }

        // Then accept digits anywhere in the sentence: «задачу 1 удали», «удали задачу номер 1».
        foreach (string word in words)
        {
            string digits = string.Empty;
            foreach (char c in word)
            {
                if (char.IsDigit(c)) digits += c;
            }
            if (digits.Length > 0)
            {
                int number;
                if (int.TryParse(digits, out number) && number > 0)
                    return number.ToString();
            }
        }
        return null;
    }

    private static bool ContainsAny(string text, params string[] fragments)
    {
        foreach (string fragment in fragments)
            if (text.Contains(fragment)) return true;
        return false;
    }
}
