using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Журнал действий агента: когда, какая возможность, как решено (выполнено, спросил, запрещено) и получилось ли.
/// Тексты сообщений и получатели не записываются. Хранится на устройстве, человек может посмотреть и очистить (/журнал).
/// Вызывать из главного потока.
/// </summary>
public static class ActionLog
{
    private const int MaxLines = 300;

    private static string PathFile { get { return Path.Combine(Application.persistentDataPath, "actions.log"); } }

    public static void Add(string capabilityId, string how, bool success)
    {
        try
        {
            CapabilityInfo info = CapabilityRegistry.Get(capabilityId);
            string title = info != null ? info.Title : capabilityId;
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\t" + title + "\t" + how + "\t" + (success ? "ok" : "ошибка");
            File.AppendAllText(PathFile, line + "\n", Encoding.UTF8);
            Trim();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ActionLog] " + ex.Message);
        }
    }

    private static void Trim()
    {
        string[] lines = File.ReadAllLines(PathFile, Encoding.UTF8);
        if (lines.Length <= MaxLines + 50) return;
        var keep = new string[MaxLines];
        Array.Copy(lines, lines.Length - MaxLines, keep, 0, MaxLines);
        File.WriteAllLines(PathFile, keep, Encoding.UTF8);
    }

    public static string Describe(int count)
    {
        try
        {
            if (!File.Exists(PathFile)) return "Журнал действий пуст.";
            string[] lines = File.ReadAllLines(PathFile, Encoding.UTF8);
            if (lines.Length == 0) return "Журнал действий пуст.";
            var sb = new StringBuilder("Последние действия:");
            for (int i = Math.Max(0, lines.Length - count); i < lines.Length; i++)
                sb.Append('\n').Append(lines[i].Replace('\t', ' '));
            sb.Append("\nОчистить: /журнал очистить");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "Не удалось прочитать журнал: " + ex.Message;
        }
    }

    public static void Clear()
    {
        try { if (File.Exists(PathFile)) File.Delete(PathFile); }
        catch (Exception ex) { Debug.LogWarning("[ActionLog] " + ex.Message); }
    }
}
