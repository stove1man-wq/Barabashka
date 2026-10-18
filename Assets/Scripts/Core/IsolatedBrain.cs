using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class IsolatedBrain : IAgentBrain
{
    /// <summary>Начало стандартного ответа «не понял»: по нему HybridBrain отличает непонятую фразу.</summary>
    public const string UnknownReplyPrefix = "Пока я умею отвечать на базовые запросы";

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

    // «переименуй задачу 2 в купить хлеб», «измени задачу вторую на позвонить маме»
    private static readonly Regex RenamePattern = new Regex(
        @"^\s*(?:переименуй|измени|поменяй)\s+задачу\s+(?:номер\s+)?(\S+)\s+(?:в|на)\s+(.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public Task<string> ProcessMessageAsync(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return Task.FromResult("Я вас не расслышал. Повторите, пожалуйста.");

        string text = userText.Trim();
        string lower = Normalize(text);
        // Числа словами -> цифры («задачу два» -> «задачу 2»): распознавание речи часто даёт слова.
        string lowerNum = RuNumbers.Replace(lower);

        string title = ExtractTaskTitle(text, lower);
        if (title != null)
        {
            DateTime? due;
            string cleanTitle = AgentDates.SplitDue(title, DateTime.Now, out due);
            string addCommand = "COMMAND:TASK_ADD|" + Uri.EscapeDataString(cleanTitle);
            if (due.HasValue)
                addCommand += "|" + due.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return Task.FromResult(addCommand);
        }

        Match rename = RenamePattern.Match(text);
        if (rename.Success)
        {
            string renameNumber = ExtractTaskNumber(RuNumbers.Replace(Normalize(rename.Groups[1].Value)));
            string newTitle = rename.Groups[2].Value.Trim().TrimEnd('.', '!', '?');
            if (renameNumber == null || newTitle.Length == 0)
                return Task.FromResult("Не понял, какую задачу переименовать. Скажите: «переименуй задачу 2 в …».");
            return Task.FromResult("COMMAND:TASK_RENAME|" + renameNumber + "|" + Uri.EscapeDataString(newTitle));
        }

        if (IsTaskListRequest(lower))
            return Task.FromResult("COMMAND:TASK_LIST");

        if (MentionsTask(lower) && ContainsAny(lower, "удал", "убер", "убрать", "сотри", "удалить"))
        {
            string number = ExtractTaskSpec(lowerNum);
            return Task.FromResult(number == null
                ? "Не понял, какую задачу удалить. Назови её номер или попроси показать список задач."
                : "COMMAND:TASK_DELETE|" + number);
        }

        if (MentionsTask(lower) && ContainsAny(lower, "заверш", "выполн", "законч", "готов", "отметь", "сделал", "сделана", "сделано"))
        {
            string number = ExtractTaskSpec(lowerNum);
            return Task.FromResult(number == null
                ? "Не понял, какую задачу отметить выполненной. Назови её номер или попроси показать список задач."
                : "COMMAND:TASK_DONE|" + number);
        }

        string calculation;
        if (AgentCalculator.TryAnswer(text, out calculation))
            return Task.FromResult(calculation);

        if (ContainsAny(lower, "дату", "дата", "какое число", "какое сегодня число", "число сегодня",
                "день недели", "какой сегодня день", "какой день", "что за день"))
            return Task.FromResult("COMMAND:DATE");

        if (lower.Contains("привет") || lower.Contains("здравствуй"))
            return Task.FromResult("Приветствую! Я ваш независимый ИИ-агент. Рад помочь!");

        if (lower.Contains("погод"))
            return Task.FromResult("Сегодня отличная погода для разработки локальных систем!");

        if (lower.Contains("время") || lower.Contains("час"))
            return Task.FromResult("COMMAND:TIME");

        if (lower.Contains("браузер") || lower.Contains("гитхаб"))
            return Task.FromResult("COMMAND:OPEN_URL|https://github.com");

        return Task.FromResult(UnknownReplyPrefix + " и управлять локальным списком задач. Можно сказать: «добавь задачу … на завтра», «покажи мои задачи», «отметь задачу 2 выполненной», «переименуй задачу 1 в …», «удали первую и вторую задачу», «какое сегодня число» или «сколько будет 15 процентов от 2300».");
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

    // Номера задач из фразы: «первую и вторую» / «задачи 1 и 3» -> "1,2" / "1,3"; «все задачи» -> "ALL".
    private static string ExtractTaskSpec(string normalizedText)
    {
        string[] words = normalizedText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string word in words)
            if (word == "все" || word == "всё" || word == "всех") return "ALL";

        var numbers = new List<int>();
        foreach (string word in words)
        {
            int ordinal;
            if (OrdinalNumbers.TryGetValue(word, out ordinal))
            {
                if (!numbers.Contains(ordinal)) numbers.Add(ordinal);
                continue;
            }

            int number;
            if (word.Length > 0 && int.TryParse(word, out number) && number > 0 && !numbers.Contains(number))
                numbers.Add(number);
        }

        if (numbers.Count == 0) return null;
        numbers.Sort();
        return string.Join(",", numbers);
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
