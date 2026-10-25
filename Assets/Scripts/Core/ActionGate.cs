using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Ожидание ответа «да/нет» перед действием вне приложения (режим «С подтверждением»).
/// Инструмент кладёт сюда действие и спрашивает человека; роутер выполняет его после «да».
/// </summary>
public static class ActionGate
{
    private const float TimeoutSec = 30f;

    private static Func<Task<string>> _pending;
    private static float _until;
    private static string _label;

    /// <summary>Какая возможность ждёт ответа (для журнала действий).</summary>
    public static string PendingLabel { get { return _label; } }

    public static bool HasPending { get { return _pending != null && Time.unscaledTime <= _until; } }

    /// <param name="execute">Действие; возвращает текст для показа человеку.</param>
    /// <param name="label">Идентификатор возможности для журнала действий (необязательно).</param>
    /// <param name="logOnRun">Записать в журнал выполнение после «да»; false, если запись делает само действие.</param>
    public static void Ask(Func<Task<string>> execute, string label = null, bool logOnRun = true)
    {
        _label = label;
        if (label != null && logOnRun)
        {
            _pending = async () =>
            {
                string text = await execute();
                ActionLog.Add(label, "подтверждено и выполнено", true);
                return text;
            };
        }
        else
        {
            _pending = execute;
        }
        _until = Time.unscaledTime + TimeoutSec;
    }

    /// <summary>Забирает ожидающее действие (или null, если его нет либо время вышло).</summary>
    public static Func<Task<string>> Take()
    {
        Func<Task<string>> action = _pending;
        bool fresh = Time.unscaledTime <= _until;
        _pending = null;
        return fresh ? action : null;
    }

    public static void Clear()
    {
        _pending = null;
        _label = null;
    }

    public const string Suffix = " Скажите «да, сделай» или «нет, отмена».";
}
