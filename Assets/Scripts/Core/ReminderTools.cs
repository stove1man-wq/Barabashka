using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;

internal static class ToolArgs
{
    public static string Get(IDictionary<string, string> args, string key)
    {
        string value;
        return args != null && args.TryGetValue(key, out value) && value != null ? value.Trim() : string.Empty;
    }
}

public sealed class ReminderAddTool : IAgentTool
{
    private static readonly string[] AtFormats = { "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm" };

    public string Name { get { return "reminder_add"; } }

    public string Description
    {
        get
        {
            return "поставить напоминание или таймер. {\"tool\":\"reminder_add\",\"text\":\"о чём напомнить\",\"at\":\"ГГГГ-ММ-ДДTЧЧ:ММ\"} " +
                   "или вместо at: \"in_minutes\":\"10\". Для таймера text \"Таймер\" и \"timer\":\"1\".";
        }
    }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        DateTime now = DateTime.Now;
        DateTime due;
        string at = ToolArgs.Get(args, "at");
        string inMinutes = ToolArgs.Get(args, "in_minutes");
        double minutes;

        if (at.Length > 0 && DateTime.TryParseExact(at, AtFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out due))
            due = DateTime.SpecifyKind(due, DateTimeKind.Local);
        else if (double.TryParse(inMinutes, NumberStyles.Float, CultureInfo.InvariantCulture, out minutes) && minutes > 0)
            due = now.AddMinutes(minutes);
        else
            return Task.FromResult(ToolResult.Fail("Не понял, когда напомнить. Скажите, например: «напомни через 10 минут позвонить маме»."));

        if (due < now.AddSeconds(-30)) return Task.FromResult(ToolResult.Fail("Это время уже прошло."));
        if (due > now.AddDays(366)) return Task.FromResult(ToolResult.Fail("Это слишком далеко: ставлю напоминания не дальше чем на год."));

        string text = ToolArgs.Get(args, "text");
        bool isTimer = ToolArgs.Get(args, "timer") == "1" || string.Equals(text, "Таймер", StringComparison.OrdinalIgnoreCase);
        if (text.Length == 0) text = isTimer ? "Таймер" : "Напоминание";
        if (text.Length > 200) text = text.Substring(0, 200);

        string error;
        AgentReminders.Record record = AgentReminders.Add(text, due, isTimer, out error);
        if (record == null) return Task.FromResult(ToolResult.Fail(error));

        string when = AgentReminders.DescribeWhen(due, now);
        return Task.FromResult(ToolResult.Ok(isTimer
            ? "Таймер поставлен: " + when + "."
            : "Напомню «" + text + "» " + when + "."));
    }
}

public sealed class ReminderListTool : IAgentTool
{
    public string Name { get { return "reminder_list"; } }
    public string Description { get { return "показать активные напоминания и таймеры. {\"tool\":\"reminder_list\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        List<AgentReminders.Record> pending = AgentReminders.Pending();
        if (pending.Count == 0) return Task.FromResult(ToolResult.Ok("Активных напоминаний нет."));

        DateTime now = DateTime.Now;
        var sb = new StringBuilder("Ваши напоминания:\n");
        for (int i = 0; i < pending.Count; i++)
        {
            sb.Append(i + 1).Append(". ").Append(pending[i].text).Append(", ")
              .Append(AgentReminders.DescribeWhen(AgentReminders.DueLocal(pending[i]), now)).Append('\n');
        }
        return Task.FromResult(ToolResult.Ok(sb.ToString().TrimEnd()));
    }
}

public sealed class ReminderDeleteTool : IAgentTool
{
    public string Name { get { return "reminder_delete"; } }

    public string Description
    {
        get { return "отменить напоминания. {\"tool\":\"reminder_delete\",\"numbers\":\"1,2\"}, все: \"ALL\"; все таймеры: {\"tool\":\"reminder_delete\",\"kind\":\"timer\"}"; }
    }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        string numbers = ToolArgs.Get(args, "numbers");
        bool timers = string.Equals(ToolArgs.Get(args, "kind"), "timer", StringComparison.OrdinalIgnoreCase);

        // Без номера: если напоминание одно, отменяем его.
        if (numbers.Length == 0 && !timers)
        {
            List<AgentReminders.Record> pending = AgentReminders.Pending();
            if (pending.Count == 0) return Task.FromResult(ToolResult.Ok("Активных напоминаний нет."));
            if (pending.Count > 1) return Task.FromResult(ToolResult.Fail("Какое напоминание отменить? Назовите номер или попросите показать напоминания."));
            numbers = "1";
        }

        int removed = AgentReminders.Delete(numbers, timers);
        if (removed < 0) return Task.FromResult(ToolResult.Fail("Напоминания с таким номером нет. Попросите показать напоминания."));
        if (removed == 0) return Task.FromResult(ToolResult.Ok(timers ? "Активных таймеров нет." : "Активных напоминаний нет."));
        return Task.FromResult(ToolResult.Ok(removed == 1 ? "Напоминание отменено." : "Отменено напоминаний: " + removed + "."));
    }
}
