using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Barabashka.Contracts;

/// <summary>
/// Каталог инструментов, которые может вызвать языковая модель. Здесь же два действия:
/// описать инструменты для модели (подсказка) и превратить её вызов в команду роутера «TASK_ADD|…».
/// Модель не выполняет ничего сама: любая команда проходит ту же проверку, что и команда от правил
/// (удаление - с подтверждением, ссылки - только http/https).
/// </summary>
public static class AgentToolCatalog
{
    private static readonly string[] WeekDays =
        { "воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота" };

    private static readonly Regex DigitsPattern = new Regex(@"\d+", RegexOptions.CultureInvariant);
    private static readonly Regex IsoDatePattern = new Regex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant);

    public static string BuildSystemPrompt(DateTime now)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ты - Барабашка, голосовой помощник на телефоне. Отвечай ТОЛЬКО одним JSON-объектом, без пояснений до и после.");
        sb.AppendLine("Если просьбу можно выполнить инструментом, верни {\"tool\":\"имя\", ...поля}.");
        sb.AppendLine("Если это вопрос или обычный разговор, верни {\"reply\":\"ответ по-русски, 1-3 коротких предложения, без списков и markdown\"}.");
        sb.AppendLine("Инструменты:");
        sb.AppendLine("- time: назвать время. {\"tool\":\"time\"}");
        sb.AppendLine("- date: назвать дату и день недели. {\"tool\":\"date\"}");
        sb.AppendLine("- task_add: добавить задачу. {\"tool\":\"task_add\",\"title\":\"что сделать\",\"due\":\"ГГГГ-ММ-ДД или пустая строка\"}");
        sb.AppendLine("- task_list: показать задачи. {\"tool\":\"task_list\"}");
        sb.AppendLine("- task_done: отметить задачи выполненными. {\"tool\":\"task_done\",\"numbers\":\"1,2\"}, все задачи: \"ALL\"");
        sb.AppendLine("- task_delete: удалить задачи. {\"tool\":\"task_delete\",\"numbers\":\"1\"}");
        sb.AppendLine("- task_rename: переименовать задачу. {\"tool\":\"task_rename\",\"number\":\"2\",\"title\":\"новое название\"}");
        sb.AppendLine("- open_url: открыть сайт в браузере. {\"tool\":\"open_url\",\"url\":\"https://...\"}");
        foreach (IAgentTool registered in AgentToolRegistry.Default.Tools)
            sb.AppendLine("- " + registered.Name + ": " + registered.Description);
        sb.AppendLine("Номера задач - это номера из списка, который показывает task_list. Если пользователь не назвал номер, верни reply с уточняющим вопросом.");
        sb.AppendLine("Каждая реплика пользователя - отдельная просьба: не повторяй инструменты из прошлых реплик, если об этом не просят снова.");
        sb.AppendLine("Если пользователь отказывается («не надо», «не открывай», «отмена», «ничего не делай»), верни {\"reply\":\"Хорошо.\"} без tool.");
        sb.AppendLine("Не выдумывай факты, погоду и новости. Если не знаешь ответа или не можешь это сделать, честно скажи об этом в reply.");
        sb.AppendLine("Сегодня " + now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ", " + WeekDays[(int)now.DayOfWeek] +
                      ", время " + now.ToString("HH:mm", CultureInfo.InvariantCulture) + ".");
        sb.Append("Календарь для сроков: ");
        for (int i = 0; i < 8; i++)
        {
            DateTime d = now.Date.AddDays(i);
            string label = i == 0 ? "сегодня" : i == 1 ? "завтра" : i == 2 ? "послезавтра" : WeekDays[(int)d.DayOfWeek];
            if (i > 0) sb.Append("; ");
            sb.Append(label).Append(" = ").Append(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (i < 3) sb.Append(" (").Append(WeekDays[(int)d.DayOfWeek]).Append(')');
        }
        sb.Append('.');
        return sb.ToString();
    }

    /// <summary>
    /// Короткий и неизменный в течение суток запрос для маленькой модели в телефоне: чем он короче и стабильнее,
    /// тем меньше времени уходит на его обработку (одинаковое начало запроса модель запоминает и не считает заново).
    /// Поэтому здесь нет текущего времени и календаря.
    /// </summary>
    public static string BuildCompactPrompt(DateTime now)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ты - Барабашка, помощник. Отвечай ТОЛЬКО одним JSON-объектом.");
        sb.AppendLine("Вопрос, разговор, «расскажи», «объясни», «что такое»: {\"reply\":\"ответ по-русски, 1-3 коротких предложения\"}.");
        sb.AppendLine("Инструмент вызывай, только если пользователь прямо просит действие: {\"tool\":\"имя\", ...поля}.");
        sb.AppendLine("Примеры: «расскажи про космос» -> {\"reply\":\"Космос - это...\"}; «добавь задачу купить хлеб» -> {\"tool\":\"task_add\",\"title\":\"купить хлеб\",\"due\":\"\"}.");
        sb.AppendLine("Инструменты:");
        sb.AppendLine("- time, date: {\"tool\":\"time\"} или {\"tool\":\"date\"}");
        sb.AppendLine("- task_add: {\"tool\":\"task_add\",\"title\":\"что сделать\",\"due\":\"ГГГГ-ММ-ДД или пусто\"}");
        sb.AppendLine("- task_list: {\"tool\":\"task_list\"}");
        sb.AppendLine("- task_done, task_delete: {\"tool\":\"task_done\",\"numbers\":\"1,2\"}");
        sb.AppendLine("- task_rename: {\"tool\":\"task_rename\",\"number\":\"2\",\"title\":\"новое название\"}");
        sb.AppendLine("- open_url: {\"tool\":\"open_url\",\"url\":\"https://...\"}");
        foreach (IAgentTool registered in AgentToolRegistry.Default.Tools)
            sb.AppendLine("- " + registered.Name + ": " + registered.Description);
        sb.AppendLine("Каждая реплика - отдельная просьба. При отказе («не надо», «отмена») верни {\"reply\":\"Хорошо.\"}. Не выдумывай факты, если не знаешь - так и скажи.");
        sb.Append("Сегодня " + now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ", " + WeekDays[(int)now.DayOfWeek] + ".");
        return sb.ToString();
    }

    /// <summary>
    /// Превращает вызов модели в команду роутера (без префикса COMMAND:). Возвращает false и причину, если вызов некорректен.
    /// </summary>
    public static bool TryBuildCommand(Dictionary<string, string> args, out string command, out string error)
    {
        command = null;
        error = null;
        string tool = MiniJson.Get(args, "tool").ToLowerInvariant();

        switch (tool)
        {
            case "time":
                command = "TIME";
                return true;
            case "date":
                command = "DATE";
                return true;
            case "task_list":
                command = "TASK_LIST";
                return true;

            case "task_add":
            {
                string title = MiniJson.Get(args, "title");
                if (title.Length == 0) { error = "не указано название задачи"; return false; }
                command = "TASK_ADD|" + Uri.EscapeDataString(title);
                string due = MiniJson.Get(args, "due");
                if (IsoDatePattern.IsMatch(due))
                {
                    DateTime parsed;
                    if (DateTime.TryParseExact(due, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                        command += "|" + due;
                }
                return true;
            }

            case "task_done":
            case "task_delete":
            {
                string spec = NormalizeNumbers(MiniJson.Get(args, "numbers"), MiniJson.Get(args, "number"));
                if (spec == null) { error = "не указаны номера задач"; return false; }
                command = (tool == "task_done" ? "TASK_DONE|" : "TASK_DELETE|") + spec;
                return true;
            }

            case "task_rename":
            {
                string number = NormalizeNumbers(MiniJson.Get(args, "number"), MiniJson.Get(args, "numbers"));
                string title = MiniJson.Get(args, "title");
                if (number == null || number == "ALL" || number.Contains(",")) { error = "не указан номер одной задачи"; return false; }
                if (title.Length == 0) { error = "не указано новое название"; return false; }
                command = "TASK_RENAME|" + number + "|" + Uri.EscapeDataString(title);
                return true;
            }

            case "open_url":
            {
                string url = MiniJson.Get(args, "url");
                Uri parsed;
                if (url.Length == 0 || url.IndexOf('|') >= 0 || url.IndexOf(' ') >= 0 ||
                    !Uri.TryCreate(url, UriKind.Absolute, out parsed) ||
                    (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                {
                    error = "некорректная ссылка";
                    return false;
                }
                command = "OPEN_URL|" + parsed.AbsoluteUri;
                return true;
            }

            default:
            {
                IAgentTool registered;
                if (!AgentToolRegistry.Default.TryGet(tool, out registered))
                {
                    error = "неизвестный инструмент «" + tool + "»";
                    return false;
                }

                // Инструмент из реестра: все поля, кроме служебных, передаются ему как аргументы.
                var pairs = new List<KeyValuePair<string, string>>();
                foreach (KeyValuePair<string, string> field in args)
                {
                    if (string.Equals(field.Key, "tool", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(field.Key, "reply", StringComparison.OrdinalIgnoreCase)) continue;
                    pairs.Add(new KeyValuePair<string, string>(field.Key, field.Value));
                }
                command = "TOOL|" + registered.Name + "|" + AgentToolRegistry.BuildQuery(pairs);
                return true;
            }
        }
    }

    // «1, 2» / «1 и 2» / «все» / «ALL» -> «1,2» / «ALL»; null, если номеров нет.
    private static string NormalizeNumbers(string primary, string secondary)
    {
        foreach (string source in new[] { primary, secondary })
        {
            if (string.IsNullOrWhiteSpace(source)) continue;
            string lower = source.Trim().ToLowerInvariant();
            if (lower == "all" || lower == "все" || lower == "всё" || lower == "всех") return "ALL";

            var numbers = new List<int>();
            foreach (Match m in DigitsPattern.Matches(lower))
            {
                int n;
                if (int.TryParse(m.Value, out n) && n > 0 && !numbers.Contains(n)) numbers.Add(n);
            }
            if (numbers.Count == 0) continue;
            numbers.Sort();
            return string.Join(",", numbers);
        }
        return null;
    }
}
