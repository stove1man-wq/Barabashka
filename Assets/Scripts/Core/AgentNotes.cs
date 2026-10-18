using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>Заметки («запомни, что…»): хранение в файле приложения, как у напоминаний.</summary>
public static class AgentNotes
{
    private const string FileName = "barabashka_notes_v1.json";
    private const int MaxNotes = 500;

    [Serializable]
    public class Note
    {
        public int id;
        public string text;
        public string createdUtc;
    }

    [Serializable]
    public class Document
    {
        public int nextId = 1;
        public List<Note> items = new List<Note>();
    }

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
            Debug.LogWarning("[Notes] Не удалось прочитать заметки: " + ex.Message);
        }
        return new Document();
    }

    private static bool Save()
    {
        try
        {
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(Doc));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(temp, FilePath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[Notes] Не удалось сохранить заметки: " + ex);
            return false;
        }
    }

    public static List<Note> All() { return new List<Note>(Doc.items); }

    public static Note Add(string text, out string error)
    {
        error = null;
        if (Doc.items.Count >= MaxNotes)
        {
            error = "Заметок слишком много (больше " + MaxNotes + "). Удалите ненужные.";
            return null;
        }

        var note = new Note
        {
            id = Doc.nextId++,
            text = text,
            createdUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };
        Doc.items.Add(note);
        if (!Save())
        {
            Doc.items.Remove(note);
            error = "Не удалось сохранить заметку. Проверьте свободное место.";
            return null;
        }
        return note;
    }

    /// <param name="spec">Номера через запятую или ALL.</param>
    /// <returns>Сколько заметок удалено; -1 - неверные номера.</returns>
    public static int Delete(string spec)
    {
        var targets = new List<Note>();
        if (string.Equals((spec ?? string.Empty).Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            targets.AddRange(Doc.items);
        }
        else
        {
            foreach (string part in (spec ?? string.Empty).Split(','))
            {
                int n;
                if (!int.TryParse(part.Trim(), out n) || n < 1 || n > Doc.items.Count) return -1;
                if (!targets.Contains(Doc.items[n - 1])) targets.Add(Doc.items[n - 1]);
            }
        }

        foreach (Note note in targets) Doc.items.Remove(note);
        if (targets.Count > 0) Save();
        return targets.Count;
    }
}
