using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Barabashka.Contracts;
using UnityEngine;

/// <summary>
/// Напоминания и таймеры: хранение в файле приложения, выборка сработавших, описание времени словами.
/// Системное уведомление (если платформа его поддерживает) ставится через ReminderNotifierHost.
/// </summary>
public static class AgentReminders
{
    private const string FileName = "barabashka_reminders_v1.json";
    private const int MaxPending = 50;
    private const string PermissionAskedKey = "reminders.notifPermissionAsked";

    [Serializable]
    public class Record
    {
        public int id;
        public string text;
        public string dueAtUtc;
        public bool fired;
        public bool isTimer;
    }

    [Serializable]
    public class Document
    {
        public int nextId = 1;
        public List<Record> items = new List<Record>();
    }

    private static readonly string[] MonthsGenitive =
    {
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря"
    };

    private static Document _doc;

    private static string FilePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

    private static Document Doc
    {
        get
        {
            if (_doc == null) _doc = Load();
            return _doc;
        }
    }

    private static Document Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                Document loaded = JsonUtility.FromJson<Document>(File.ReadAllText(FilePath));
                if (loaded != null && loaded.items != null) return loaded;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Reminders] Не удалось прочитать хранилище: " + ex.Message);
        }
        return new Document();
    }

    private static void Save()
    {
        try
        {
            // Выполненные напоминания старше суток не нужны.
            DateTime cutoff = DateTime.UtcNow.AddDays(-1);
            Doc.items.RemoveAll(r => r.fired && ParseUtc(r.dueAtUtc) < cutoff);

            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(Doc));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(temp, FilePath);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Reminders] Не удалось сохранить: " + ex);
        }
    }

    private static DateTime ParseUtc(string iso)
    {
        DateTime parsed;
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
        return DateTime.MaxValue;
    }

    public static DateTime DueLocal(Record record)
    {
        return ParseUtc(record.dueAtUtc).ToLocalTime();
    }

    /// <summary>Активные (ещё не сработавшие) напоминания по времени; номера в списке - позиции здесь.</summary>
    public static List<Record> Pending()
    {
        var list = new List<Record>();
        foreach (Record r in Doc.items) if (!r.fired) list.Add(r);
        list.Sort((a, b) => ParseUtc(a.dueAtUtc).CompareTo(ParseUtc(b.dueAtUtc)));
        return list;
    }

    public static Record Add(string text, DateTime dueLocal, bool isTimer, out string error)
    {
        error = null;
        if (Pending().Count >= MaxPending)
        {
            error = "Слишком много напоминаний (больше " + MaxPending + "). Удалите ненужные.";
            return null;
        }

        var record = new Record
        {
            id = Doc.nextId++,
            text = text,
            dueAtUtc = dueLocal.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            fired = false,
            isTimer = isTimer
        };
        Doc.items.Add(record);
        Save();

        AskNotificationPermissionOnce();
        Schedule(record);
        return record;
    }

    private static void Schedule(Record record)
    {
        IReminderNotifier notifier = ReminderNotifierHost.Current;
        if (notifier == null) return;
        try { notifier.Schedule(record.id, record.text, DueLocal(record)); }
        catch (Exception ex) { Debug.LogWarning("[Reminders] Не удалось поставить уведомление: " + ex.Message); }
    }

    private static void CancelNotification(int id)
    {
        IReminderNotifier notifier = ReminderNotifierHost.Current;
        if (notifier == null) return;
        try { notifier.Cancel(id); }
        catch (Exception ex) { Debug.LogWarning("[Reminders] Не удалось снять уведомление: " + ex.Message); }
    }

    /// <summary>При старте приложения заново ставит системные уведомления для будущих напоминаний.</summary>
    public static void RescheduleAll()
    {
        DateTime now = DateTime.Now;
        foreach (Record r in Pending())
            if (DueLocal(r) > now) Schedule(r);
    }

    /// <summary>Сработавшие напоминания: помечает их выполненными и убирает системное уведомление (сообщит приложение само).</summary>
    public static List<Record> TakeDue(DateTime utcNow)
    {
        var due = new List<Record>();
        foreach (Record r in Doc.items)
        {
            if (r.fired || ParseUtc(r.dueAtUtc) > utcNow) continue;
            r.fired = true;
            due.Add(r);
        }
        if (due.Count == 0) return due;

        Save();
        foreach (Record r in due) CancelNotification(r.id);
        due.Sort((a, b) => ParseUtc(a.dueAtUtc).CompareTo(ParseUtc(b.dueAtUtc)));
        return due;
    }

    /// <param name="spec">Номера из списка через запятую или ALL; пусто - использовать timersOnly.</param>
    /// <returns>Сколько напоминаний удалено; -1, если номера неверные.</returns>
    public static int Delete(string spec, bool timersOnly)
    {
        List<Record> pending = Pending();
        var targets = new List<Record>();

        if (timersOnly)
        {
            foreach (Record r in pending) if (r.isTimer) targets.Add(r);
        }
        else if (string.Equals((spec ?? string.Empty).Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            targets.AddRange(pending);
        }
        else
        {
            foreach (string part in (spec ?? string.Empty).Split(','))
            {
                int n;
                if (!int.TryParse(part.Trim(), out n) || n < 1 || n > pending.Count) return -1;
                if (!targets.Contains(pending[n - 1])) targets.Add(pending[n - 1]);
            }
        }

        foreach (Record r in targets)
        {
            Doc.items.Remove(r);
            CancelNotification(r.id);
        }
        if (targets.Count > 0) Save();
        return targets.Count;
    }

    // На Android 13+ уведомления показываются только с разрешением: просим один раз, когда оно впервые понадобилось.
    private static void AskNotificationPermissionOnce()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (PlayerPrefs.GetInt(PermissionAskedKey, 0) != 0) return;
            PlayerPrefs.SetInt(PermissionAskedKey, 1);
            PlayerPrefs.Save();
            const string permission = "android.permission.POST_NOTIFICATIONS";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
                UnityEngine.Android.Permission.RequestUserPermission(permission);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Reminders] Не удалось запросить разрешение на уведомления: " + ex.Message);
        }
#endif
    }

    /// <summary>«через 5 минут», «сегодня в 21:00», «завтра в 09:00», «16 октября в 09:00».</summary>
    public static string DescribeWhen(DateTime dueLocal, DateTime now)
    {
        TimeSpan left = dueLocal - now;
        if (left.TotalSeconds < 90)
            return "через " + Math.Max(1, (int)Math.Round(left.TotalSeconds)) + " " + Plural(Math.Max(1, (int)Math.Round(left.TotalSeconds)), "секунду", "секунды", "секунд");
        if (left.TotalMinutes < 90)
        {
            int minutes = (int)Math.Round(left.TotalMinutes);
            return "через " + minutes + " " + Plural(minutes, "минуту", "минуты", "минут");
        }

        string clock = dueLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (dueLocal.Date == now.Date) return "сегодня в " + clock;
        if (dueLocal.Date == now.Date.AddDays(1)) return "завтра в " + clock;
        return dueLocal.Day + " " + MonthsGenitive[dueLocal.Month - 1] + " в " + clock;
    }

    private static string Plural(int n, string one, string few, string many)
    {
        int mod100 = n % 100;
        int mod10 = n % 10;
        if (mod100 >= 11 && mod100 <= 14) return many;
        if (mod10 == 1) return one;
        if (mod10 >= 2 && mod10 <= 4) return few;
        return many;
    }
}
