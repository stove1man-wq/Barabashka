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

    public static bool HasPending { get { return _pending != null && Time.unscaledTime <= _until; } }

    /// <param name="execute">Действие; возвращает текст для показа человеку.</param>
    public static void Ask(Func<Task<string>> execute)
    {
        _pending = execute;
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
    }

    public const string Suffix = " Скажите «да, сделай» или «нет, отмена».";
}
