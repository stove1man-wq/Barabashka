using System;
using System.Text;
using System.Threading.Tasks;

public class IsolatedBrain : IAgentBrain
{
    public Task<string> ProcessMessageAsync(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
            return Task.FromResult("Я вас не расслышал. Повторите, пожалуйста.");

        string text = userText.Trim();
        string lower = text.ToLowerInvariant();

        // A07: детерминированные команды задач. Заголовок кодируется, чтобы символы-разделители
        // и пробелы не повреждали протокол COMMAND.
        string title = ExtractTaskTitle(text, lower);
        if (title != null)
            return Task.FromResult("COMMAND:TASK_ADD|" + Uri.EscapeDataString(title));

        if (ContainsAny(lower, "покажи задачи", "список задач", "мои задачи", "какие задачи", "что у меня по задачам", "покажи список задач"))
            return Task.FromResult("COMMAND:TASK_LIST");

        string number = ExtractNumberAfterAny(lower, "заверши задачу", "выполни задачу", "отметь задачу выполненной", "отметь задачу выполненной номер", "закончи задачу");
        if (number != null)
            return Task.FromResult("COMMAND:TASK_DONE|" + number);

        number = ExtractNumberAfterAny(lower, "удали задачу", "удалить задачу", "убери задачу");
        if (number != null)
            return Task.FromResult("COMMAND:TASK_DELETE|" + number);

        if (lower.Contains("привет") || lower.Contains("здравствуй"))
            return Task.FromResult("Приветствую! Я ваш независимый ИИ-агент. Рад помочь!");

        if (lower.Contains("погода"))
            return Task.FromResult("Сегодня отличная погода для разработки локальных систем!");

        if (lower.Contains("время") || lower.Contains("час"))
            return Task.FromResult("COMMAND:TIME");

        if (lower.Contains("браузер") || lower.Contains("гитхаб"))
            return Task.FromResult("COMMAND:OPEN_URL|https://github.com");

        return Task.FromResult("Пока я умею отвечать на базовые запросы и управлять локальным списком задач. Попробуйте: «добавь задачу купить молоко», «покажи задачи», «заверши задачу 1» или «удали задачу 1».");
    }

    private static string ExtractTaskTitle(string original, string lower)
    {
        string[] prefixes = { "добавь задачу ", "добавить задачу ", "создай задачу ", "создать задачу ", "запиши задачу ", "напомни мне сделать " };
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

    private static string ExtractNumberAfterAny(string lower, params string[] phrases)
    {
        foreach (string phrase in phrases)
        {
            int at = lower.IndexOf(phrase, StringComparison.Ordinal);
            if (at < 0) continue;
            string tail = lower.Substring(at + phrase.Length).Trim();
            if (tail.StartsWith("номер ", StringComparison.Ordinal)) tail = tail.Substring(6).Trim();
            string digits = string.Empty;
            foreach (char c in tail)
            {
                if (char.IsDigit(c)) digits += c;
                else if (digits.Length > 0) break;
            }
            if (digits.Length > 0) return digits;
        }
        return null;
    }

    private static bool ContainsAny(string text, params string[] phrases)
    {
        foreach (string phrase in phrases)
            if (text.Contains(phrase)) return true;
        return false;
    }
}
