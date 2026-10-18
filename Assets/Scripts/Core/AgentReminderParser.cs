using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Разбор русских фраз про напоминания и таймеры правилами (без языковой модели):
/// «напомни через 10 минут позвонить маме», «напомни завтра в 9 утра про встречу»,
/// «поставь таймер на полчаса», «покажи напоминания», «отмени напоминание 2», «отмени таймер».
/// Работает с нормализованным текстом (строчные буквы, слова через пробел, без знаков). Результат - команда роутера «TOOL|…».
/// Если время в фразе не найдено, возвращает false: фраза пойдёт дальше (например, как обычная задача или к языковой модели).
/// </summary>
public static class AgentReminderParser
{
    private static readonly HashSet<string> RemindWords = new HashSet<string> { "напомни", "напомнить" };
    private static readonly HashSet<string> AddVerbs = new HashSet<string> { "поставь", "поставить", "создай", "создать", "добавь", "добавить", "сделай", "запиши", "установи", "заведи" };
    private static readonly HashSet<string> CancelVerbs = new HashSet<string> { "отмени", "отменить", "удали", "удалить", "убери", "убрать", "сбрось", "выключи", "останови", "сотри" };
    private static readonly HashSet<string> ListWords = new HashSet<string> { "покажи", "показать", "список", "какие", "какой", "мои", "мой", "что", "выведи", "перечисли", "отобрази", "есть" };
    private static readonly HashSet<string> Filler = new HashSet<string> { "мне", "нам", "пожалуйста", "пожалуйста,", "будь", "добр" };
    private static readonly HashSet<string> LeadingNoise = new HashSet<string> { "что", "чтобы", "то", "том" };

    public static bool TryHandle(string lower, DateTime now, out string command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(lower)) return false;
        string[] w = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        bool remind = false, noun = false, timer = false, cancel = false, add = false, list = false;
        foreach (string word in w)
        {
            if (RemindWords.Contains(word)) remind = true;
            if (word.StartsWith("напоминани", StringComparison.Ordinal)) noun = true;
            if (word.StartsWith("таймер", StringComparison.Ordinal)) timer = true;
            if (CancelVerbs.Contains(word)) cancel = true;
            if (AddVerbs.Contains(word)) add = true;
            if (ListWords.Contains(word)) list = true;
        }

        if (!remind && !noun && !timer) return false;

        // Отмена: «отмени напоминание 2», «отмени все напоминания», «отмени таймер».
        if (cancel && (noun || timer))
        {
            string spec = ExtractNumbers(RuNumbers.Replace(lower));
            var cancelPairs = new List<KeyValuePair<string, string>>();
            if (spec != null) cancelPairs.Add(new KeyValuePair<string, string>("numbers", spec));
            else if (timer && !noun) cancelPairs.Add(new KeyValuePair<string, string>("kind", "timer"));
            command = "TOOL|reminder_delete|" + AgentToolRegistry.BuildQuery(cancelPairs);
            return true;
        }

        // Список: «покажи напоминания», «какие у меня таймеры».
        if (list && (noun || timer) && !remind && !add)
        {
            command = "TOOL|reminder_list|";
            return true;
        }

        var used = new bool[w.Length];
        for (int i = 0; i < w.Length; i++)
        {
            if (RemindWords.Contains(w[i]) || AddVerbs.Contains(w[i]) || Filler.Contains(w[i]) ||
                w[i].StartsWith("напоминани", StringComparison.Ordinal) || w[i].StartsWith("таймер", StringComparison.Ordinal))
                used[i] = true;
        }

        // Таймер: «поставь таймер на 10 минут», «таймер полчаса».
        if (timer)
        {
            TimeSpan span;
            int end;
            int at = IndexOfTimer(w);
            int start = at + 1;
            if (start < w.Length && (w[start] == "на" || w[start] == "через")) { used[start] = true; start++; }
            if (!TryDuration(w, start, out span, out end)) return false;
            MarkUsed(used, start, end);

            string label = JoinRemaining(w, used);
            command = BuildAdd(label.Length > 0 ? "Таймер: " + label : "Таймер", null, span, true);
            return true;
        }

        if (!remind && !(noun && add)) return false;

        // Напоминание через промежуток: «напомни через 10 минут …».
        for (int i = 0; i < w.Length; i++)
        {
            if (w[i] != "через") continue;
            TimeSpan span;
            int end;
            if (!TryDuration(w, i + 1, out span, out end)) continue;
            used[i] = true;
            MarkUsed(used, i + 1, end);
            command = BuildAdd(Capitalize(JoinRemaining(w, used)), null, span, false);
            return true;
        }

        // Напоминание на время суток: «напомни завтра в 9 утра …», «в 18 30 …».
        string dayWord = null;
        for (int i = 0; i < w.Length; i++)
        {
            if (w[i] == "сегодня" || w[i] == "завтра" || w[i] == "послезавтра")
            {
                dayWord = w[i];
                used[i] = true;
            }
        }

        for (int i = 0; i < w.Length - 1; i++)
        {
            if (w[i] != "в" && w[i] != "к") continue;
            int hour, minute, end;
            string dayPart;
            if (!TryClock(w, i + 1, out hour, out minute, out dayPart, out end)) continue;

            DateTime due;
            if (!ResolveDue(now, dayWord, hour, minute, dayPart, out due)) return false;
            used[i] = true;
            MarkUsed(used, i + 1, end);
            command = BuildAdd(Capitalize(JoinRemaining(w, used)), due, TimeSpan.Zero, false);
            return true;
        }

        return false;
    }

    // ---- сборка команды ----

    private static string BuildAdd(string text, DateTime? dueLocal, TimeSpan span, bool isTimer)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        pairs.Add(new KeyValuePair<string, string>("text", string.IsNullOrWhiteSpace(text) ? (isTimer ? "Таймер" : "Напоминание") : text));
        if (dueLocal.HasValue)
            pairs.Add(new KeyValuePair<string, string>("at", dueLocal.Value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)));
        else
            pairs.Add(new KeyValuePair<string, string>("in_minutes", span.TotalMinutes.ToString("0.####", CultureInfo.InvariantCulture)));
        if (isTimer) pairs.Add(new KeyValuePair<string, string>("timer", "1"));
        return "TOOL|reminder_add|" + AgentToolRegistry.BuildQuery(pairs);
    }

    private static int IndexOfTimer(string[] w)
    {
        for (int i = 0; i < w.Length; i++)
            if (w[i].StartsWith("таймер", StringComparison.Ordinal)) return i;
        return -1;
    }

    private static void MarkUsed(bool[] used, int from, int toExclusive)
    {
        for (int i = from; i < toExclusive && i < used.Length; i++) used[i] = true;
    }

    private static string JoinRemaining(string[] w, bool[] used)
    {
        var kept = new List<string>();
        for (int i = 0; i < w.Length; i++) if (!used[i]) kept.Add(w[i]);
        while (kept.Count > 0 && LeadingNoise.Contains(kept[0])) kept.RemoveAt(0);
        return string.Join(" ", kept).Trim();
    }

    private static string Capitalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    // «1», «1 и 2», «первое»... -> «1,2»; «все» -> ALL; нет номеров -> null.
    private static string ExtractNumbers(string normalizedWithDigits)
    {
        string[] words = normalizedWithDigits.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string word in words)
            if (word == "все" || word == "всё" || word == "всех") return "ALL";
        return IsolatedBrain.ExtractTaskSpec(normalizedWithDigits);
    }

    // ---- длительность ----

    private static bool TryDuration(string[] w, int start, out TimeSpan total, out int end)
    {
        total = TimeSpan.Zero;
        end = start;
        int i = start;
        bool any = false;

        while (i < w.Length)
        {
            int j = i;
            if (any && w[j] == "и") j++;
            TimeSpan part;
            int next;
            if (!ParseComponent(w, j, out part, out next)) break;
            total += part;
            i = next;
            any = true;
        }

        end = i;
        return any && total > TimeSpan.Zero;
    }

    private static bool ParseComponent(string[] w, int i, out TimeSpan part, out int next)
    {
        part = TimeSpan.Zero;
        next = i;
        if (i >= w.Length) return false;

        string token = w[i];
        TimeSpan unit;

        if (token == "полчаса") { part = TimeSpan.FromMinutes(30); next = i + 1; return true; }

        // «час», «минуту», «секунду» без числа - это одна единица.
        if ((token == "час" || token == "минуту" || token == "минутку" || token == "секунду") && UnitOf(token, out unit))
        {
            part = unit;
            next = i + 1;
            return true;
        }

        double count;
        int j;
        if (token == "пару" || token == "пара") { count = 2; j = i + 1; }
        else if (token == "полтора" || token == "полторы") { count = 1.5; j = i + 1; }
        else
        {
            int n;
            if (!ReadNumber(w, i, out n, out j)) return false;
            count = n;
            if (j + 1 < w.Length && w[j] == "с" && w[j + 1] == "половиной") { count += 0.5; j += 2; }
        }

        if (j >= w.Length || !UnitOf(w[j], out unit)) return false;
        part = TimeSpan.FromSeconds(unit.TotalSeconds * count);
        next = j + 1;
        return true;
    }

    private static bool UnitOf(string token, out TimeSpan unit)
    {
        unit = TimeSpan.Zero;
        if (token.StartsWith("секунд", StringComparison.Ordinal) || token == "сек") unit = TimeSpan.FromSeconds(1);
        else if (token.StartsWith("минут", StringComparison.Ordinal) || token == "мин") unit = TimeSpan.FromMinutes(1);
        else if (token.StartsWith("час", StringComparison.Ordinal) || token == "ч") unit = TimeSpan.FromHours(1);
        else return false;
        return true;
    }

    // ---- время суток ----

    private static bool TryClock(string[] w, int start, out int hour, out int minute, out string dayPart, out int end)
    {
        hour = 0;
        minute = 0;
        dayPart = null;
        end = start;

        int h, i;
        if (!ReadNumber(w, start, out h, out i) || h > 24) return false;

        int j = i;
        if (j < w.Length && (w[j] == "час" || w[j] == "часа" || w[j] == "часов")) j++;

        int mm, k;
        if (j < w.Length && IsMinuteToken(w[j]) && ReadNumber(w, j, out mm, out k) && mm <= 59)
        {
            minute = mm;
            j = k;
            if (j < w.Length && w[j].StartsWith("минут", StringComparison.Ordinal)) j++;
        }

        string part;
        if (j < w.Length && DayPartOf(w[j], out part))
        {
            dayPart = part;
            j++;
        }

        hour = h;
        end = j;
        return true;
    }

    // Минуты: двузначная цифра («30», «05») или число словами; одиночная цифра - скорее начало текста («в 9 5 раз»).
    private static bool IsMinuteToken(string token)
    {
        int digits;
        if (int.TryParse(token, out digits)) return token.Length == 2;
        int word;
        return RuNumbers.TryWord(token, out word);
    }

    private static bool DayPartOf(string token, out string part)
    {
        part = null;
        switch (token)
        {
            case "утра": case "утром": part = "утра"; return true;
            case "дня": case "днем": case "днём": part = "дня"; return true;
            case "вечера": case "вечером": part = "вечера"; return true;
            case "ночи": case "ночью": part = "ночи"; return true;
            default: return false;
        }
    }

    private static bool ResolveDue(DateTime now, string dayWord, int hour, int minute, string dayPart, out DateTime due)
    {
        due = default(DateTime);
        DateTime day = now.Date;
        if (dayWord == "завтра") day = day.AddDays(1);
        else if (dayWord == "послезавтра") day = day.AddDays(2);

        if (dayPart != null)
        {
            int h24 = To24(hour, dayPart);
            if (h24 < 0 || h24 > 23) return false;
            due = day.AddHours(h24).AddMinutes(minute);
            if (dayWord == null && due <= now) due = due.AddDays(1);
            return true;
        }

        if (dayWord != null)
        {
            int h24 = (hour >= 1 && hour <= 6) ? hour + 12 : hour;
            if (h24 > 23) return false;
            due = day.AddHours(h24).AddMinutes(minute);
            return true;
        }

        // Без дня и времени суток: ближайшее будущее вхождение («в 9» - это 9 утра или 9 вечера, что наступит раньше).
        var candidates = new List<int>();
        if (hour <= 23) candidates.Add(hour);
        if (hour >= 1 && hour <= 11) candidates.Add(hour + 12);
        if (candidates.Count == 0) return false;

        DateTime best = DateTime.MaxValue;
        foreach (int h in candidates)
        {
            DateTime t = day.AddHours(h).AddMinutes(minute);
            if (t <= now) t = t.AddDays(1);
            if (t < best) best = t;
        }
        due = best;
        return true;
    }

    private static int To24(int hour, string dayPart)
    {
        switch (dayPart)
        {
            case "утра": return hour == 12 ? 0 : hour;
            case "дня": return hour < 12 ? hour + 12 : hour;
            case "вечера": return hour < 12 ? hour + 12 : hour;
            case "ночи": return hour == 12 ? 0 : (hour >= 10 && hour < 12 ? hour + 12 : hour);
            default: return hour;
        }
    }

    // ---- числа: цифрами или словами («9», «девять», «двадцать пять») ----

    private static bool ReadNumber(string[] w, int i, out int value, out int next)
    {
        value = 0;
        next = i;
        if (i >= w.Length) return false;

        int v;
        if (int.TryParse(w[i], out v)) { value = v; next = i + 1; return true; }
        if (!RuNumbers.TryWord(w[i], out v)) return false;

        int total = v;
        int j = i + 1;
        while (j < w.Length)
        {
            int u;
            if (!RuNumbers.TryWord(w[j], out u)) break;
            bool ok = (total % 100 == 0 && total > 0 && u < 100) ||
                      (total >= 20 && total < 100 && total % 10 == 0 && u >= 1 && u <= 9);
            if (!ok) break;
            total += u;
            j++;
        }
        value = total;
        next = j;
        return true;
    }
}
