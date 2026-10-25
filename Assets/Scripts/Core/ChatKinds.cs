using System;
using UnityEngine;

/// <summary>Разделы общего чата: у каждого свой цвет подложки и своя цветная кнопка вверху чата.</summary>
public enum ChatKind
{
    Dialog = 0,
    Phone = 1,
    Notes = 2,
    Telegram = 3,
    System = 4
}

/// <summary>
/// К какому разделу относится сообщение. Раздел «текущего хода» ставит тот, кто понял, о чём речь
/// (роутер по возможности, команда «/…»): уже показанные сообщения этого хода перекрашиваются.
/// </summary>
public static class ChatKinds
{
    public const int Count = 5;

    public static readonly ChatKind[] All =
    {
        ChatKind.Dialog, ChatKind.Phone, ChatKind.Notes, ChatKind.Telegram, ChatKind.System
    };

    /// <summary>Раздел текущего хода (начинается с каждым новым сообщением человека).</summary>
    public static ChatKind Current { get; private set; }

    /// <summary>Раздел текущего хода уточнён: сообщения этого хода надо перекрасить.</summary>
    public static event Action<ChatKind> Changed;

    /// <summary>Новый ход: раздел определён по тексту, событие не нужно.</summary>
    public static void Begin(ChatKind kind) { Current = kind; }

    public static void Set(ChatKind kind)
    {
        if (kind == Current) return;
        Current = kind;
        Action<ChatKind> handler = Changed;
        if (handler != null) handler(kind);
    }

    public static string Title(ChatKind kind)
    {
        switch (kind)
        {
            case ChatKind.Phone: return "Телефон";
            case ChatKind.Notes: return "Заметки";
            case ChatKind.Telegram: return "Telegram";
            case ChatKind.System: return "Система";
            default: return "Диалог";
        }
    }

    /// <summary>Яркий цвет кнопки выбранного раздела.</summary>
    public static Color Tab(ChatKind kind)
    {
        switch (kind)
        {
            case ChatKind.Phone: return new Color(0.15f, 0.60f, 0.35f, 1f);
            case ChatKind.Notes: return new Color(0.80f, 0.60f, 0.15f, 1f);
            case ChatKind.Telegram: return new Color(0.15f, 0.65f, 0.85f, 1f);
            case ChatKind.System: return new Color(0.55f, 0.38f, 0.80f, 1f);
            default: return new Color(0.20f, 0.45f, 0.75f, 1f);
        }
    }

    /// <summary>Подложка сообщения (тёмная, чтобы текст читался).</summary>
    public static Color Bubble(ChatKind kind, bool isUser)
    {
        switch (kind)
        {
            case ChatKind.Phone:
                return isUser ? new Color(0.09f, 0.26f, 0.17f, 1f) : new Color(0.07f, 0.18f, 0.12f, 1f);
            case ChatKind.Notes:
                return isUser ? new Color(0.28f, 0.22f, 0.07f, 1f) : new Color(0.20f, 0.16f, 0.06f, 1f);
            case ChatKind.Telegram:
                return isUser ? new Color(0.08f, 0.25f, 0.32f, 1f) : new Color(0.06f, 0.17f, 0.22f, 1f);
            case ChatKind.System:
                return isUser ? new Color(0.21f, 0.14f, 0.30f, 1f) : new Color(0.15f, 0.10f, 0.22f, 1f);
            default:
                return isUser ? new Color(0.10f, 0.23f, 0.36f, 1f) : new Color(0.085f, 0.115f, 0.17f, 1f);
        }
    }

    public static ChatKind ForText(string text)
    {
        string t = (text ?? string.Empty).TrimStart();
        if (t.StartsWith("/телеграм", StringComparison.OrdinalIgnoreCase) || t.StartsWith("/telegram", StringComparison.OrdinalIgnoreCase))
            return ChatKind.Telegram;
        if (t.StartsWith("/", StringComparison.Ordinal)) return ChatKind.System;
        return ChatKind.Dialog;
    }

    public static ChatKind ForCapability(string id)
    {
        if (string.IsNullOrEmpty(id)) return ChatKind.Dialog;
        if (id == "telegram_read") return ChatKind.Telegram;
        if (id.StartsWith("task_", StringComparison.Ordinal) || id.StartsWith("note_", StringComparison.Ordinal) ||
            id.StartsWith("reminder_", StringComparison.Ordinal) || id.StartsWith("alarm_", StringComparison.Ordinal))
            return ChatKind.Notes;
        switch (id)
        {
            case "call":
            case "dial":
            case "message":
            case "flashlight":
            case "volume":
            case "phone_settings":
            case "open_app":
            case "battery":
                return ChatKind.Phone;
            default:
                return ChatKind.Dialog;
        }
    }

    /// <summary>Раздел по тексту команды роутера: «TASK_ADD|…», «TOOL|имя|…», «TIME».</summary>
    public static ChatKind ForCommand(string command)
    {
        if (string.IsNullOrEmpty(command)) return ChatKind.Dialog;
        if (command.StartsWith("TASK_", StringComparison.Ordinal)) return ChatKind.Notes;
        if (command.StartsWith("TOOL|", StringComparison.Ordinal))
        {
            string rest = command.Substring("TOOL|".Length);
            int bar = rest.IndexOf('|');
            return ForCapability(bar >= 0 ? rest.Substring(0, bar) : rest);
        }
        return ChatKind.Dialog;
    }
}
