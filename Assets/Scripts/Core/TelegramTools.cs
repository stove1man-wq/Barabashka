using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;

/// <summary>Прочитать Telegram: без имени - в каких чатах есть непрочитанное, с именем - последние сообщения чата.</summary>
public sealed class TelegramReadTool : IAgentTool
{
    public string Name { get { return "telegram_read"; } }
    public string Description { get { return "прочитать Telegram: непрочитанные чаты или последние сообщения человека. {\"tool\":\"telegram_read\",\"rest\":\"Маша\"} (без имени - список непрочитанного)"; } }

    public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        TelegramClient telegram = TelegramClient.Instance;
        string notReady = await telegram.EnsureReadyAsync();
        if (notReady != null) return ToolResult.Fail(notReady);

        string[] words = ToolArgs.Get(args, "rest").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            List<KeyValuePair<string, int>> unread = telegram.UnreadChats(8);
            if (unread.Count == 0) return ToolResult.Ok("В Telegram новых сообщений нет.");
            var sb = new StringBuilder("Непрочитанное в Telegram: ");
            for (int i = 0; i < unread.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(unread[i].Key).Append(" (").Append(unread[i].Value).Append(')');
            }
            sb.Append(". Чтобы прочитать, скажите: «прочитай сообщения от …».");
            return ToolResult.Ok(sb.ToString());
        }

        ContactBook.MatchResult match = ContactBook.Find(words, telegram.ChatContacts());
        if (match.Found == null)
        {
            if (match.Ambiguous.Count > 0)
                return ToolResult.Fail("В Telegram несколько подходящих чатов: " + string.Join(", ", match.Ambiguous.ToArray()) + ". Назовите точнее.");
            return ToolResult.Fail("Не нашёл в Telegram чат «" + string.Join(" ", words) + "».");
        }

        long chatId;
        if (!long.TryParse(match.Found.Number, out chatId)) return ToolResult.Fail("Не удалось открыть чат.");
        string history = await telegram.LastMessagesAsync(chatId, 5);
        return ToolResult.Ok("Чат «" + match.Found.Name + "»:\n" + history);
    }
}

/// <summary>Отправка в Telegram через TDLib (от имени человека), когда Telegram подключён и режим действий не «вручную».</summary>
internal static class TelegramSend
{
    public sealed class Attempt
    {
        public ToolResult Result;   // null - не получилось, нужно действовать как раньше (открыть чат с готовым текстом)
        public string Note = string.Empty;
    }

    public static async Task<Attempt> TryAsync(IDictionary<string, string> args, ActionLevel level)
    {
        var attempt = new Attempt();
        TelegramClient telegram = TelegramClient.Instance;

        string notReady = await telegram.EnsureReadyAsync();
        if (notReady != null)
        {
            attempt.Note = "Telegram не подключён к агенту (" + notReady.TrimEnd('.') + "), открываю чат вручную. ";
            return attempt;
        }

        string source = ToolArgs.Get(args, "name").Length > 0 ? ToolArgs.Get(args, "name") : ToolArgs.Get(args, "rest");
        string[] words = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        ContactBook.MatchResult match = ContactBook.Find(words, telegram.ChatContacts());
        if (match.Found == null)
        {
            attempt.Note = match.Ambiguous.Count > 0
                ? "В Telegram несколько подходящих чатов (" + string.Join(", ", match.Ambiguous.ToArray()) + "), ищу по контактам телефона. "
                : "В Telegram нет чата «" + source + "», ищу по контактам телефона. ";
            return attempt;
        }

        string text = ToolArgs.Get(args, "text");
        if (text.Length == 0 && ToolArgs.Get(args, "name").Length == 0)
            text = string.Join(" ", words, match.WordsUsed, words.Length - match.WordsUsed).Trim();
        if (text.Length == 0) return attempt;   // без текста просто открываем чат
        text = char.ToUpperInvariant(text[0]) + text.Substring(1);

        long chatId;
        if (!long.TryParse(match.Found.Number, out chatId)) return attempt;
        string title = match.Found.Name;

        if (level == ActionLevel.Confirm)
        {
            ActionGate.Ask(async () => await telegram.SendTextAsync(chatId, text) ?? "Отправил в Telegram для " + title + ": «" + text + "».", "message");
            attempt.Result = ToolResult.Ok("Отправить в Telegram для " + title + ": «" + text + "»?" + ActionGate.Suffix);
            return attempt;
        }

        string error = await telegram.SendTextAsync(chatId, text);
        attempt.Result = error == null
            ? ToolResult.Ok("Отправил в Telegram для " + title + ": «" + text + "».")
            : ToolResult.Fail(error);
        return attempt;
    }
}

/// <summary>Команда «/телеграм»: настройка и вход в аккаунт.</summary>
public static class TelegramCommands
{
    public static async Task<string> HandleAsync(string args)
    {
        string[] p = (args ?? string.Empty).Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        string verb = p.Length > 0 ? p[0].ToLowerInvariant() : string.Empty;
        string rest = p.Length > 1 ? p[1].Trim() : string.Empty;
        TelegramClient t = TelegramClient.Instance;

        if (verb.StartsWith("настро", StringComparison.Ordinal))
        {
            string[] k = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            long id;
            if (k.Length != 2 || !long.TryParse(k[0], out id) || k[1].Length < 16)
                return "Формат: /телеграм настроить <api_id> <api_hash>. Ключи берутся на my.telegram.org (раздел API development tools) под вашим аккаунтом.";
            if (!SecretStore.Set(TelegramClient.KeyApiId, k[0]) || !SecretStore.Set(TelegramClient.KeyApiHash, k[1]))
                return "Не удалось безопасно сохранить ключи. Ничего не записано.";
            return "Ключи приложения сохранены (в зашифрованном виде). Дальше: /телеграм войти +79001234567. Сообщение с ключами останется в истории чата, очистите её.";
        }
        if (verb.StartsWith("вой", StringComparison.Ordinal) || verb == "login")
            return rest.Length < 6 ? "Формат: /телеграм войти +79001234567" : await t.SubmitPhoneAsync(rest);
        if (verb.StartsWith("код", StringComparison.Ordinal) || verb == "code")
            return rest.Length == 0 ? "Формат: /телеграм код 12345" : await t.SubmitCodeAsync(rest);
        if (verb.StartsWith("парол", StringComparison.Ordinal) || verb == "password")
            return rest.Length == 0 ? "Формат: /телеграм пароль ВАШ_ПАРОЛЬ" : await t.SubmitPasswordAsync(rest);
        if (verb.StartsWith("вый", StringComparison.Ordinal) || verb == "logout")
            return await t.LogOutAsync();
        if (verb.StartsWith("чат", StringComparison.Ordinal))
        {
            string notReady = await t.EnsureReadyAsync();
            if (notReady != null) return notReady;
            List<ContactBook.Contact> chats = t.ChatContacts();
            if (chats.Count == 0) return "Список чатов пока пуст, попробуйте через минуту.";
            var names = new List<string>();
            foreach (ContactBook.Contact c in chats) { names.Add(c.Name); if (names.Count >= 30) break; }
            return "Чаты (" + chats.Count + "): " + string.Join(", ", names.ToArray());
        }

        // Статус: ключи, библиотека, вход.
        if (!TelegramClient.HasCredentials)
            return "Telegram не настроен. 1) Получите api_id и api_hash на my.telegram.org. 2) /телеграм настроить <api_id> <api_hash>. 3) /телеграм войти +7… 4) /телеграм код …\nПодробно: docs/TELEGRAM.md";
        return await t.BeginAsync();
    }
}
