using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// A04/A07: даты без зависимости от культуры устройства (на Android русская культура может быть недоступна).
/// Сегодняшняя дата словами, разбор срока задачи («на завтра», «в пятницу») и его описание.
/// </summary>
public static class AgentDates
{
    private static readonly string[] MonthsGenitive =
    {
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря"
    };

    // Индекс = (int)DayOfWeek: воскресенье = 0.
    private static readonly string[] Weekdays =
    {
        "воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота"
    };

    private static readonly Dictionary<string, DayOfWeek> WeekdayForms = new Dictionary<string, DayOfWeek>
    {
        { "понедельник", DayOfWeek.Monday },
        { "вторник", DayOfWeek.Tuesday },
        { "среду", DayOfWeek.Wednesday }, { "среда", DayOfWeek.Wednesday },
        { "четверг", DayOfWeek.Thursday },
        { "пятницу", DayOfWeek.Friday }, { "пятница", DayOfWeek.Friday },
        { "субботу", DayOfWeek.Saturday }, { "суббота", DayOfWeek.Saturday },
        { "воскресенье", DayOfWeek.Sunday }
    };

    /// <summary>«Сегодня пятница, 16 октября 2026 года.»</summary>
    public static string Today(DateTime now)
    {
        return "Сегодня " + Weekdays[(int)now.DayOfWeek] + ", " + now.Day + " " +
               MonthsGenitive[now.Month - 1] + " " + now.Year + " года.";
    }

    /// <summary>
    /// Если название задачи заканчивается сроком («… на завтра», «… в пятницу»), отделяет его.
    /// Возвращает название без срока; due — дата (локальная) или null.
    /// </summary>
    public static string SplitDue(string title, DateTime today, out DateTime? due)
    {
        due = null;
        if (string.IsNullOrWhiteSpace(title)) return title;

        string[] words = title.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2) return title;

        string last = words[words.Length - 1].Trim('.', ',', '!', '?').ToLowerInvariant();
        int cut = 1; // сколько слов с конца относится к сроку
        DateTime? date = null;

        if (last == "сегодня") date = today.Date;
        else if (last == "завтра") date = today.Date.AddDays(1);
        else if (last == "послезавтра") date = today.Date.AddDays(2);
        else
        {
            DayOfWeek day;
            if (WeekdayForms.TryGetValue(last, out day))
            {
                int diff = ((int)day - (int)today.DayOfWeek + 7) % 7;
                if (diff == 0) diff = 7; // «в пятницу» в пятницу = через неделю
                date = today.Date.AddDays(diff);
            }
        }

        if (date == null) return title;

        // Предлог перед сроком: «на завтра», «в пятницу», «во вторник».
        if (words.Length - 1 >= 2)
        {
            string prep = words[words.Length - 2].ToLowerInvariant();
            if (prep == "на" || prep == "в" || prep == "во" || prep == "к") cut = 2;
        }

        int keep = words.Length - cut;
        if (keep < 1) return title;

        due = new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, 23, 59, 0, DateTimeKind.Local);
        return string.Join(" ", words, 0, keep);
    }

    /// <summary>Описание срока для списка: «сегодня», «завтра», «просрочено», «17 октября».</summary>
    public static string DescribeDue(string dueAtUtcIso, DateTime today)
    {
        DateTime due;
        if (string.IsNullOrWhiteSpace(dueAtUtcIso) ||
            !DateTime.TryParse(dueAtUtcIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out due))
            return null;

        DateTime local = due.ToLocalTime().Date;
        int days = (int)(local - today.Date).TotalDays;

        if (days < 0) return "просрочено";
        if (days == 0) return "сегодня";
        if (days == 1) return "завтра";
        if (days == 2) return "послезавтра";
        return local.Day + " " + MonthsGenitive[local.Month - 1];
    }
}
