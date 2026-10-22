using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Журнал фраз, которые правила не поняли. Нужен, чтобы по реальным фразам расширять правила.
/// Хранится только на устройстве (barabashka_unknown_v1.txt), последние 200 записей. Смотреть: «/непонятое».
/// </summary>
public static class UnknownLog
{
    private const int MaxLines = 200;

    private static string PathFile { get { return Path.Combine(Application.persistentDataPath, "barabashka_unknown_v1.txt"); } }

    public static void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            string clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
            string line = DateTime.Now.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture) + " | " + clean;
            List<string> lines = ReadAll();
            lines.Add(line);
            if (lines.Count > MaxLines) lines.RemoveRange(0, lines.Count - MaxLines);
            File.WriteAllLines(PathFile, lines.ToArray(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[UnknownLog] " + ex.Message);
        }
    }

    public static List<string> ReadAll()
    {
        try
        {
            if (File.Exists(PathFile)) return new List<string>(File.ReadAllLines(PathFile, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[UnknownLog] " + ex.Message);
        }
        return new List<string>();
    }

    public static void Clear()
    {
        try { if (File.Exists(PathFile)) File.Delete(PathFile); }
        catch (Exception ex) { Debug.LogWarning("[UnknownLog] " + ex.Message); }
    }

    /// <summary>Текст для показа в чате: последние записи.</summary>
    public static string Describe(int count)
    {
        List<string> lines = ReadAll();
        if (lines.Count == 0) return "Журнал пуст: непонятых фраз пока не было.";
        int start = Math.Max(0, lines.Count - count);
        var sb = new StringBuilder("Фразы, которые правила не поняли (последние " + (lines.Count - start) + " из " + lines.Count + "):");
        for (int i = start; i < lines.Count; i++) sb.Append('\n').Append(lines[i]);
        sb.Append("\nОчистить: /непонятое очистить");
        return sb.ToString();
    }
}
