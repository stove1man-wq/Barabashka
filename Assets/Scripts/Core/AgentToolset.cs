using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class AgentToolset
{
    private static AgentTaskStore _taskStore;

    private static AgentTaskStore Tasks
    {
        get
        {
            if (_taskStore == null) _taskStore = new AgentTaskStore();
            return _taskStore;
        }
    }

    // Новый экземпляр читает файл заново: так проверяется, что изменение действительно сохранено на диск.
    private static AgentTaskStore.AgentTaskRecord ReadBack(string id)
    {
        try { return new AgentTaskStore().GetById(id); }
        catch (Exception ex)
        {
            Debug.LogError("[Toolset] Не удалось перечитать хранилище задач: " + ex);
            return null;
        }
    }

    public static string GetCurrentSystemTime()
    {
        return DateTime.Now.Hour.ToString("00") + ":" + DateTime.Now.Minute.ToString("00");
    }

    public static void OpenWebBrowser(string url)
    {
        Debug.Log("[Toolset] Открытие браузера по адресу: " + url);
        Application.OpenURL(url);
    }

    /// <param name="encodedTitle">Название задачи, закодированное Uri.EscapeDataString.</param>
    /// <param name="dueLocalDate">Срок в формате yyyy-MM-dd (локальная дата) или пусто.</param>
    public static string AddTask(string encodedTitle, string dueLocalDate = null)
    {
        string title = Uri.UnescapeDataString(encodedTitle ?? string.Empty).Trim();
        if (title.Length == 0) return "Не удалось добавить задачу: не указано название.";

        DateTime? due = null;
        DateTime parsed;
        if (!string.IsNullOrEmpty(dueLocalDate) &&
            DateTime.TryParseExact(dueLocalDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            due = new DateTime(parsed.Year, parsed.Month, parsed.Day, 23, 59, 0, DateTimeKind.Local);
        }

        try
        {
            AgentTaskStore.AgentTaskRecord task = Tasks.Create(title, string.Empty, due);

            if (ReadBack(task.id) == null)
                return "Не удалось убедиться, что задача сохранена. Проверьте память приложения.";

            string when = AgentDates.DescribeDue(task.dueAtUtc, DateTime.Now);
            return "Задача добавлена: «" + task.title + "»" + (when != null ? ", срок: " + when : string.Empty) + ".";
        }
        catch (Exception ex)
        {
            Debug.LogError("[Toolset] Не удалось сохранить задачу: " + ex);
            return "Не удалось сохранить задачу. Проверьте свободное место и доступ к памяти приложения.";
        }
    }

    public static string ListTasks()
    {
        IReadOnlyList<AgentTaskStore.AgentTaskRecord> tasks = Tasks.GetAll(false);
        if (tasks.Count == 0) return "Активных задач пока нет.";

        var result = new StringBuilder("Ваши активные задачи:\n");
        for (int i = 0; i < tasks.Count; i++)
        {
            result.Append(i + 1).Append(". ").Append(tasks[i].title);
            string when = AgentDates.DescribeDue(tasks[i].dueAtUtc, DateTime.Now);
            if (when != null) result.Append(", срок: ").Append(when);
            result.Append('\n');
        }
        return result.ToString().TrimEnd();
    }

    /// <summary>
    /// Описание задач по номерам для вопроса-подтверждения: «задачу «A»», «задачи «A» и «B»», «все задачи (3)».
    /// null, если номера неверные или задач нет.
    /// </summary>
    public static string DescribeTasks(string spec)
    {
        IReadOnlyList<AgentTaskStore.AgentTaskRecord> tasks = Tasks.GetAll(false);
        List<AgentTaskStore.AgentTaskRecord> targets = ResolveTargets(spec, tasks, out string error);
        if (targets == null || targets.Count == 0) return null;

        if (spec != null && spec.Trim() == "ALL") return "все задачи (" + targets.Count + ")";

        var names = new List<string>();
        foreach (AgentTaskStore.AgentTaskRecord t in targets) names.Add("«" + t.title + "»");
        string joined = names.Count == 1
            ? names[0]
            : string.Join(", ", names.GetRange(0, names.Count - 1)) + " и " + names[names.Count - 1];
        return (targets.Count == 1 ? "задачу " : "задачи ") + joined;
    }

    /// <param name="spec">Номера из текущего списка через запятую («1,2») или ALL.</param>
    public static string CompleteTask(string spec)
    {
        IReadOnlyList<AgentTaskStore.AgentTaskRecord> tasks = Tasks.GetAll(false);
        List<AgentTaskStore.AgentTaskRecord> targets = ResolveTargets(spec, tasks, out string error);
        if (targets == null) return error;

        var done = new List<string>();
        foreach (AgentTaskStore.AgentTaskRecord task in targets)
        {
            if (!Tasks.SetCompleted(task.id, true)) continue;
            AgentTaskStore.AgentTaskRecord check = ReadBack(task.id);
            if (check != null && check.isCompleted) done.Add(task.title);
        }

        if (done.Count == 0) return "Не удалось отметить задачи выполненными.";
        string failedNote = done.Count < targets.Count ? " Часть задач отметить не удалось." : string.Empty;
        if (done.Count == 1) return "Задача выполнена: «" + done[0] + "»." + failedNote;
        return "Выполнено задач: " + done.Count + "." + failedNote;
    }

    /// <param name="spec">Номера из текущего списка через запятую («1,2») или ALL.</param>
    public static string DeleteTask(string spec)
    {
        IReadOnlyList<AgentTaskStore.AgentTaskRecord> tasks = Tasks.GetAll(false);
        List<AgentTaskStore.AgentTaskRecord> targets = ResolveTargets(spec, tasks, out string error);
        if (targets == null) return error;

        // Работаем по id, поэтому сдвиг номеров после удаления не мешает.
        var deleted = new List<string>();
        foreach (AgentTaskStore.AgentTaskRecord task in targets)
        {
            if (!Tasks.Delete(task.id)) continue;
            if (ReadBack(task.id) == null) deleted.Add(task.title);
        }

        if (deleted.Count == 0) return "Не удалось удалить задачи.";
        string failedNote = deleted.Count < targets.Count ? " Часть задач удалить не удалось." : string.Empty;
        if (deleted.Count == 1) return "Задача удалена: «" + deleted[0] + "»." + failedNote;
        return "Удалено задач: " + deleted.Count + "." + failedNote;
    }

    // Превращает «1,2» или ALL в список задач из текущего списка. При ошибке возвращает null и текст ошибки.
    private static List<AgentTaskStore.AgentTaskRecord> ResolveTargets(
        string spec, IReadOnlyList<AgentTaskStore.AgentTaskRecord> tasks, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(spec))
        {
            error = "Укажите номер задачи. Сначала попросите показать задачи.";
            return null;
        }
        if (tasks.Count == 0)
        {
            error = "Активных задач пока нет.";
            return null;
        }

        var result = new List<AgentTaskStore.AgentTaskRecord>();
        if (spec.Trim() == "ALL")
        {
            result.AddRange(tasks);
            return result;
        }

        var seen = new List<int>();
        foreach (string part in spec.Split(','))
        {
            int n;
            if (!int.TryParse(part.Trim(), out n) || n < 1)
            {
                error = "Укажите номер задачи. Сначала попросите показать задачи.";
                return null;
            }
            if (n > tasks.Count)
            {
                error = "Задачи с номером " + n + " нет. Попросите показать задачи.";
                return null;
            }
            if (!seen.Contains(n))
            {
                seen.Add(n);
                result.Add(tasks[n - 1]);
            }
        }
        return result;
    }

    public static string RenameTask(string oneBasedIndex, string encodedTitle)
    {
        string title = Uri.UnescapeDataString(encodedTitle ?? string.Empty).Trim();
        if (title.Length == 0) return "Не указано новое название задачи.";

        AgentTaskStore.AgentTaskRecord task = FindActive(oneBasedIndex);
        if (task == null) return "Задача с таким номером не найдена. Попросите показать задачи.";

        DateTime? due = null;
        DateTime parsed;
        if (!string.IsNullOrWhiteSpace(task.dueAtUtc) &&
            DateTime.TryParse(task.dueAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            due = parsed;

        try
        {
            if (!Tasks.Update(task.id, title, task.notes, due))
                return "Не удалось изменить задачу.";

            AgentTaskStore.AgentTaskRecord check = ReadBack(task.id);
            if (check == null || check.title != title)
                return "Не удалось убедиться, что название изменено.";

            return "Задача переименована: «" + title + "».";
        }
        catch (Exception ex)
        {
            Debug.LogError("[Toolset] Не удалось изменить задачу: " + ex);
            return "Не удалось изменить задачу.";
        }
    }

    private static AgentTaskStore.AgentTaskRecord FindActive(string oneBasedIndex)
    {
        int index;
        if (!int.TryParse(oneBasedIndex, out index) || index < 1) return null;

        IReadOnlyList<AgentTaskStore.AgentTaskRecord> tasks = Tasks.GetAll(false);
        return index > tasks.Count ? null : tasks[index - 1];
    }
}
