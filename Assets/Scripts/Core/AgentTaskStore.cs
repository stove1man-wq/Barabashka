using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Local persistent storage for the first Barabashka tasks/notes module (A07).
/// This service deliberately has no UI or language-model dependency.
/// All data stays in the app's Unity persistent data directory.
/// </summary>
public sealed class AgentTaskStore
{
    private const int CurrentSchemaVersion = 1;
    private const string FileName = "barabashka_tasks_v1.json";

    [Serializable]
    private sealed class StoreDocument
    {
        public int schemaVersion = CurrentSchemaVersion;
        public List<AgentTaskRecord> tasks = new List<AgentTaskRecord>();
    }

    [Serializable]
    public sealed class AgentTaskRecord
    {
        public string id;
        public string title;
        public string notes;
        public string createdAtUtc;
        public string dueAtUtc;
        public bool isCompleted;
        public string updatedAtUtc;
    }

    private StoreDocument _document;
    private readonly string _filePath;

    public AgentTaskStore() : this(Path.Combine(Application.persistentDataPath, FileName))
    {
    }

    // Exposed for deterministic tests using a temporary path.
    public AgentTaskStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A non-empty storage path is required.", nameof(filePath));

        _filePath = filePath;
        _document = LoadDocument();
    }

    public IReadOnlyList<AgentTaskRecord> GetAll(bool includeCompleted = true)
    {
        var result = new List<AgentTaskRecord>();
        foreach (AgentTaskRecord task in _document.tasks)
        {
            if (task != null && (includeCompleted || !task.isCompleted))
                result.Add(Clone(task));
        }

        result.Sort((a, b) => string.Compare(a.createdAtUtc, b.createdAtUtc, StringComparison.Ordinal));
        return result.AsReadOnly();
    }

    public AgentTaskRecord GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        AgentTaskRecord task = _document.tasks.Find(item =>
            item != null && string.Equals(item.id, id, StringComparison.Ordinal));
        return task == null ? null : Clone(task);
    }

    public AgentTaskRecord Create(string title, string notes = "", DateTime? dueAtUtc = null)
    {
        string normalizedTitle = (title ?? string.Empty).Trim();
        if (normalizedTitle.Length == 0)
            throw new ArgumentException("Task title cannot be empty.", nameof(title));

        DateTime now = DateTime.UtcNow;
        var task = new AgentTaskRecord
        {
            id = Guid.NewGuid().ToString("N"),
            title = normalizedTitle,
            notes = (notes ?? string.Empty).Trim(),
            createdAtUtc = now.ToString("o"),
            updatedAtUtc = now.ToString("o"),
            dueAtUtc = dueAtUtc.HasValue ? dueAtUtc.Value.ToUniversalTime().ToString("o") : string.Empty,
            isCompleted = false
        };

        _document.tasks.Add(task);
        SaveDocument();
        return Clone(task);
    }

    public bool Update(string id, string title, string notes, DateTime? dueAtUtc)
    {
        AgentTaskRecord task = FindMutable(id);
        if (task == null) return false;

        string normalizedTitle = (title ?? string.Empty).Trim();
        if (normalizedTitle.Length == 0)
            throw new ArgumentException("Task title cannot be empty.", nameof(title));

        task.title = normalizedTitle;
        task.notes = (notes ?? string.Empty).Trim();
        task.dueAtUtc = dueAtUtc.HasValue ? dueAtUtc.Value.ToUniversalTime().ToString("o") : string.Empty;
        task.updatedAtUtc = DateTime.UtcNow.ToString("o");
        SaveDocument();
        return true;
    }

    public bool SetCompleted(string id, bool completed)
    {
        AgentTaskRecord task = FindMutable(id);
        if (task == null) return false;

        task.isCompleted = completed;
        task.updatedAtUtc = DateTime.UtcNow.ToString("o");
        SaveDocument();
        return true;
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        int index = _document.tasks.FindIndex(item =>
            item != null && string.Equals(item.id, id, StringComparison.Ordinal));
        if (index < 0) return false;

        _document.tasks.RemoveAt(index);
        SaveDocument();
        return true;
    }

    private AgentTaskRecord FindMutable(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _document.tasks.Find(item =>
            item != null && string.Equals(item.id, id, StringComparison.Ordinal));
    }

    private StoreDocument LoadDocument()
    {
        if (!File.Exists(_filePath))
            return new StoreDocument();

        try
        {
            string json = File.ReadAllText(_filePath);
            StoreDocument loaded = JsonUtility.FromJson<StoreDocument>(json);
            if (loaded == null || loaded.schemaVersion != CurrentSchemaVersion || loaded.tasks == null)
            {
                Debug.LogWarning("[AgentTaskStore] Stored task data has an unsupported or invalid schema; starting with an empty in-memory store. Original file is preserved.");
                return new StoreDocument();
            }

            return loaded;
        }
        catch (Exception ex)
        {
            // Do not overwrite a file that could contain recoverable user data.
            Debug.LogError("[AgentTaskStore] Could not read task data. Original file is preserved. " + ex.Message);
            return new StoreDocument();
        }
    }

    private void SaveDocument()
    {
        string directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = _filePath + ".tmp";
        string json = JsonUtility.ToJson(_document, true);

        File.WriteAllText(temporaryPath, json);
        if (File.Exists(_filePath))
        {
            string backupPath = _filePath + ".bak";
            try
            {
                File.Replace(temporaryPath, _filePath, backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporaryPath, _filePath, true);
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // Some Android filesystems may not support File.Replace semantics.
                File.Copy(temporaryPath, _filePath, true);
                File.Delete(temporaryPath);
            }
        }
        else
        {
            File.Move(temporaryPath, _filePath);
        }
    }

    private static AgentTaskRecord Clone(AgentTaskRecord source)
    {
        return new AgentTaskRecord
        {
            id = source.id,
            title = source.title,
            notes = source.notes,
            createdAtUtc = source.createdAtUtc,
            dueAtUtc = source.dueAtUtc,
            isCompleted = source.isCompleted,
            updatedAtUtc = source.updatedAtUtc
        };
    }
}
