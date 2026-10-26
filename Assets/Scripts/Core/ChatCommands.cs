using System;
using System.Collections.Generic;
using System.Text;

public enum ChatCmd
{
    None,
    Help,
    Section,
    DeleteLast,
    DeleteSection,
    Restore,
    RestoreAll,
    EmptyTrash,
    KeepNever,
    KeepWeek,
    KeepForever,
    TrashOn,
    TrashOff
}

public sealed class ChatCommand
{
    public ChatCmd Cmd;
    /// <summary>Раздел назван явно.</summary>
    public bool HasKind;
    public ChatKind Kind;
    /// <summary>«Все»: показать всё / очистить всё.</summary>
    public bool Everything;
    public int Count = 1;

    /// <summary>Команда меняет настройку или даёт справку: ответ показывается в чате.</summary>
    public bool Talks
    {
        get
        {
            return Cmd == ChatCmd.Help || Cmd == ChatCmd.KeepNever || Cmd == ChatCmd.KeepWeek || Cmd == ChatCmd.KeepForever ||
                   Cmd == ChatCmd.TrashOn || Cmd == ChatCmd.TrashOff;
        }
    }
}

/// <summary>
/// Голосовые и текстовые команды для всего, что можно сделать с чатом руками: разделы, удаление, возврат, хранение.
/// Узнаёт фразы по смыслу, а не по точному тексту; чужие команды («удали задачу», «покажи заметки») не трогает.
/// </summary>
public static class ChatCommands
{
    public static ChatCommand Parse(string text)
    {
        var none = new ChatCommand { Cmd = ChatCmd.None };
        string trimmed = (text ?? string.Empty).Trim();
        if (trimmed.StartsWith("/чат", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("/chat", StringComparison.OrdinalIgnoreCase))
            return new ChatCommand { Cmd = ChatCmd.Help };
        if (trimmed.StartsWith("/", StringComparison.Ordinal)) return none;

        string[] w = Words(trimmed);
        if (w.Length == 0 || w.Length > 9) return none;

        bool message = Has(w, "сообщен", "чат", "диалог", "переписк");
        bool basket = Has(w, "корзин");
        bool negation = Any(w, "не");
        bool remove = Has(w, "удал", "убер", "смахн", "стер", "очист", "сотр");

        // Хранение истории.
        if (Has(w, "храни", "сохраня", "запомин") && Has(w, "истори", "чат", "сообщен", "переписк"))
        {
            if (negation) return new ChatCommand { Cmd = ChatCmd.KeepNever };
            if (Has(w, "недел")) return new ChatCommand { Cmd = ChatCmd.KeepWeek };
            if (Any(w, "всегда", "навсегда", "постоянно")) return new ChatCommand { Cmd = ChatCmd.KeepForever };
        }

        // Корзина.
        if (basket)
        {
            if (Has(w, "очист", "опустош") || (remove && Has(w, "удал", "стер"))) return new ChatCommand { Cmd = ChatCmd.EmptyTrash };
            if (Has(w, "выключ", "отключ") || Any(w, "без")) return new ChatCommand { Cmd = ChatCmd.TrashOff };
            if (Has(w, "включ", "храни", "склады", "отправл", "клади")) return new ChatCommand { Cmd = ChatCmd.TrashOn };
            if (Has(w, "смахнут", "удаленн")) return new ChatCommand { Cmd = ChatCmd.TrashOn };
        }
        if (Any(w, "насовсем", "навсегда") && Any(w, "удаляй", "удалять", "удаляйте")) return new ChatCommand { Cmd = ChatCmd.TrashOff };

        // Вернуть удалённое.
        if (Has(w, "верн", "восстанов") && (Has(w, "сообщен", "удален", "смахнут") || basket))
        {
            bool all = Any(w, "все", "всё", "удаленные", "смахнутые", "сообщения") || basket;
            return new ChatCommand { Cmd = all ? ChatCmd.RestoreAll : ChatCmd.Restore };
        }

        ChatKind kind;
        bool hasKind = FindKind(w, out kind);
        bool everything = Any(w, "все", "всё", "весь", "всю", "целиком");

        // Удалить последнее сообщение (можно несколько).
        if (remove && Has(w, "сообщен") && Has(w, "последн", "это", "предыдущ", "прошл", "прежн"))
        {
            int count = Any(w, "два", "2", "двух", "последние") ? 2 : Any(w, "три", "3", "трёх", "трех") ? 3 : 1;
            return new ChatCommand { Cmd = ChatCmd.DeleteLast, Count = count };
        }

        // Очистить чат или раздел.
        if (remove && (message || Has(w, "раздел", "вкладк")))
        {
            return new ChatCommand { Cmd = ChatCmd.DeleteSection, HasKind = hasKind, Kind = kind, Everything = everything };
        }

        // Показать раздел: «раздел телефон», «покажи все сообщения».
        bool open = Has(w, "покаж", "откр", "перейд", "включ", "дай", "хочу");
        if (Has(w, "раздел", "вкладк") && (hasKind || everything || Any(w, "общий", "общую", "общее")))
            return new ChatCommand { Cmd = ChatCmd.Section, HasKind = hasKind, Kind = kind, Everything = !hasKind };
        if (open && everything && Has(w, "сообщен", "чат", "переписк"))
            return new ChatCommand { Cmd = ChatCmd.Section, Everything = true };

        return none;
    }

    private static bool FindKind(string[] w, out ChatKind kind)
    {
        kind = ChatKind.Dialog;
        if (Has(w, "телеграм", "telegram", "тг")) { kind = ChatKind.Telegram; return true; }
        if (Has(w, "телефон", "звонк", "контакт", "смс")) { kind = ChatKind.Phone; return true; }
        if (Has(w, "заметк", "задач", "напомин", "будильник") || Any(w, "дела", "дел")) { kind = ChatKind.Notes; return true; }
        if (Has(w, "покупк", "маркет", "озон", "wildberries", "вайлдберриз")) { kind = ChatKind.Purchases; return true; }
        if (Has(w, "систем", "настрой")) { kind = ChatKind.System; return true; }
        if (Has(w, "диалог", "разговор", "обычн")) { kind = ChatKind.Dialog; return true; }
        return false;
    }

    private static string[] Words(string text)
    {
        var sb = new StringBuilder();
        foreach (char c in text.ToLowerInvariant())
        {
            char ch = c == 'ё' ? 'е' : c;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool Has(string[] words, params string[] stems)
    {
        foreach (string word in words)
            foreach (string stem in stems)
                if (word.StartsWith(stem, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool Any(string[] words, params string[] exact)
    {
        foreach (string word in words)
            foreach (string e in exact)
                if (word == e) return true;
        return false;
    }
}
