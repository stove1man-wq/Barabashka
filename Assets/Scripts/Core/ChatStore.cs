using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Одно сообщение чата.</summary>
public sealed class ChatRecord
{
    public long Id;
    public ChatKind Kind;
    public bool IsUser;
    public string Speaker;
    public string Text;
    public long Ticks;
    /// <summary>0 - сообщение на месте; иначе момент, когда его смахнули в корзину.</summary>
    public long DeletedTicks;
    /// <summary>Служебное сообщение: на диск не пишется.</summary>
    public bool Transient;

    public bool Deleted { get { return DeletedTicks != 0; } }
}

/// <summary>
/// История чата на устройстве (файл chat.log): по строке на сообщение. Хранение зависит от настройки
/// (не хранить / неделю / всегда); смахнутое лежит в корзине неделю. Вызывать из главного потока.
/// </summary>
public static class ChatStore
{
    private const int MaxRecords = 200;
    private const int KeepDays = 7;
    public const int TrashDays = 7;

    private static string PathFile { get { return Path.Combine(Application.persistentDataPath, "chat.log"); } }

    public static List<ChatRecord> Load()
    {
        var list = new List<ChatRecord>();
        try
        {
            if (AppSettings.ChatHistory == ChatKeep.Never)
            {
                if (File.Exists(PathFile)) File.Delete(PathFile);
                return list;
            }
            if (!File.Exists(PathFile)) return list;

            long now = DateTime.UtcNow.Ticks;
            long keepAfter = now - TimeSpan.FromDays(KeepDays).Ticks;
            long trashAfter = now - TimeSpan.FromDays(TrashDays).Ticks;
            foreach (string line in File.ReadAllLines(PathFile, Encoding.UTF8))
            {
                ChatRecord rec = Parse(line);
                if (rec == null) continue;
                if (AppSettings.ChatHistory == ChatKeep.Week && rec.Ticks < keepAfter) continue;
                if (rec.Deleted && rec.DeletedTicks < trashAfter) continue;
                list.Add(rec);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Chat] не удалось прочитать историю: " + ex.Message);
        }
        return list;
    }

    public static void Save(List<ChatRecord> records)
    {
        try
        {
            if (AppSettings.ChatHistory == ChatKeep.Never)
            {
                if (File.Exists(PathFile)) File.Delete(PathFile);
                return;
            }

            var keep = new List<ChatRecord>();
            foreach (ChatRecord rec in records) if (!rec.Transient) keep.Add(rec);
            int skip = Math.Max(0, keep.Count - MaxRecords);

            var sb = new StringBuilder();
            for (int i = skip; i < keep.Count; i++) sb.Append(Format(keep[i])).Append('\n');

            string temp = PathFile + ".tmp";
            File.WriteAllText(temp, sb.ToString(), Encoding.UTF8);
            if (File.Exists(PathFile)) File.Delete(PathFile);
            File.Move(temp, PathFile);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Chat] не удалось сохранить историю: " + ex.Message);
        }
    }

    private static string Format(ChatRecord r)
    {
        return r.Id.ToString(CultureInfo.InvariantCulture) + "\t" + (int)r.Kind + "\t" + (r.IsUser ? "1" : "0") + "\t" +
               r.Ticks.ToString(CultureInfo.InvariantCulture) + "\t" + r.DeletedTicks.ToString(CultureInfo.InvariantCulture) + "\t" +
               Escape(r.Speaker) + "\t" + Escape(r.Text);
    }

    private static ChatRecord Parse(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        string[] f = line.Split('\t');
        if (f.Length < 7) return null;
        long id, ticks, deleted;
        int kind;
        if (!long.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) return null;
        if (!int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out kind)) return null;
        if (!long.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) return null;
        if (!long.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out deleted)) return null;
        if (kind < 0 || kind >= ChatKinds.Count) kind = 0;
        return new ChatRecord
        {
            Id = id,
            Kind = (ChatKind)kind,
            IsUser = f[2] == "1",
            Ticks = ticks,
            DeletedTicks = deleted,
            Speaker = Unescape(f[5]),
            Text = Unescape(f[6])
        };
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", string.Empty).Replace("\n", "\\n");
    }

    private static string Unescape(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0) return value ?? string.Empty;
        var sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '\\' && i + 1 < value.Length)
            {
                char n = value[++i];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
