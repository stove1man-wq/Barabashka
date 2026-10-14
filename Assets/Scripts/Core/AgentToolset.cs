using System;
using System.Collections.Generic;
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

    public static string GetCurrentSystemTime()
    {
        return DateTime.Now.Hour.ToString("00") + ":" + DateTime.Now.Minute.ToString("00");
    }

    public static void OpenWebBrowser(string url)
    {
        Debug.Log("[Toolset] Открытие браузера по адресу: " + url);
        Application.OpenURL(url);
    }

    public static string AddTask(string encodedTitle)
    {
        string title = Uri.UnescapeDataString(encodedTitle ?? string.Empty).Trim();
        if (title.Length == 0) return "Не удалось добавить задачу: не указано название.";

        try
        {
            AgentTaskStore.AgentTaskRecord task = Tasks.Create(title);
            return "Задача добавлена: «" + task.title + "».";
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
            if (!string.IsNullOrWhiteSpace(tasks[i].dueAtUtc))
                result.Append(" (срок: ").Append(tasks[i].dueAtUtc).Append(" UTC)");
            result.Append('\n');
        }
        return result.ToString().TrimEnd();
    }

    public static string CompleteTask(string oneBasedIndex)
    {
        int index;
        if (!int.TryParse(oneBasedIndex, out index) || index < 1)
            return "Укажите номер задачи. Сначала попросите показать задачи.";

        IReadOnlyList<AgentTaskStore.AgentTaskRecord> tasks = Tasks.GetAll(false);
        if (index > tasks.Count) return "Задача с таким номером не найдена. Попросите показать задачи.";

        AgentTaskStore.AgentTaskRecord task = tasks[index - 1];
        if (!Tasks.SetCompleted(task.id, true))
            return "Не удалось отметить задачу выполненной.";
        return "Задача выполнена: «" + task.title + "».";
    }

    public static string DeleteTask(string oneBasedIndex)
    {
        int index;
        if (!int.TryParse(oneBasedIndex, out index) || index < 1)
            return "Укажите номер задачи. Сначала попросите показать задачи.";

        IReadOnlyList<AgentTaskStore.AgentTaskRecord> tasks = Tasks.GetAll(false);
        if (index > tasks.Count) return "Задача с таким номером не найдена. Попросите показать задачи.";

        AgentTaskStore.AgentTaskRecord task = tasks[index - 1];
        if (!Tasks.Delete(task.id)) return "Не удалось удалить задачу.";
        return "Задача удалена: «" + task.title + "».";
    }
}
