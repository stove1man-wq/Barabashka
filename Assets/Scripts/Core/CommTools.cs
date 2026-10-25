using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;

// Звонки и сообщения. Поведение зависит от настройки «Действия агента» (режим действий):
// вручную - открываем готовый набор или окно сообщения; с подтверждением - спрашиваем «да/нет»; полный цикл - делаем сразу.

/// <summary>Кому адресовано действие: номер, имя для показа и слова фразы, которые остались после имени.</summary>
internal sealed class Recipient
{
    public string Number;
    public string Display;
    public string Error;
    public string Rest = string.Empty;

    public static async Task<Recipient> ResolveAsync(IDictionary<string, string> args)
    {
        var r = new Recipient();

        string number = PhoneComms.CleanNumber(ToolArgs.Get(args, "number"));
        string nameArg = ToolArgs.Get(args, "name");
        string restArg = ToolArgs.Get(args, "rest");
        string textArg = ToolArgs.Get(args, "text");

        string source = nameArg.Length > 0 ? nameArg : restArg;
        string[] words = source.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // Номер продиктовали цифрами: «… 8 900 123 45 67 …».
        if (number.Length < 5 && words.Length > 0)
        {
            var digits = new StringBuilder();
            int used = 0;
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                bool numeric = w.Length > 0;
                for (int k = 0; k < w.Length; k++)
                    if (!(char.IsDigit(w[k]) || (k == 0 && w[k] == '+'))) { numeric = false; break; }
                if (!numeric) break;
                digits.Append(w);
                used++;
            }
            string candidate = PhoneComms.CleanNumber(digits.ToString());
            if (used > 0 && candidate.Length >= 5)
            {
                number = candidate;
                r.Rest = string.Join(" ", words, used, words.Length - used);
                words = new string[0];
            }
        }

        if (number.Length >= 5)
        {
            r.Number = number;
            r.Display = number;
            if (nameArg.Length > 0 && textArg.Length == 0) r.Rest = restArg;
            else if (r.Rest.Length == 0) r.Rest = restArg;
            return r;
        }

        if (words.Length == 0)
        {
            r.Error = "Не понял, кому. Скажите, например: «позвони маме».";
            return r;
        }

        if (!ContactBook.Available)
        {
            r.Error = "Контакты доступны только на телефоне. Назовите номер цифрами.";
            return r;
        }
        if (!await PhoneComms.EnsurePermissionAsync(PhoneComms.PermContacts))
        {
            r.Error = "Нужен доступ к контактам: разрешите его при запросе или в настройках приложения. Номер можно продиктовать цифрами.";
            return r;
        }

        List<ContactBook.Contact> contacts = ContactBook.LoadAll();
        if (contacts.Count == 0)
        {
            r.Error = "В телефонной книге нет контактов с номерами.";
            return r;
        }

        ContactBook.MatchResult match = ContactBook.Find(words, contacts);
        if (match.Found != null)
        {
            r.Number = match.Found.Number;
            r.Display = match.Found.Name;
            r.Rest = nameArg.Length > 0 ? restArg : string.Join(" ", words, match.WordsUsed, words.Length - match.WordsUsed);
            return r;
        }
        if (match.Ambiguous.Count > 0)
        {
            r.Error = "Нашёл несколько контактов: " + string.Join(", ", match.Ambiguous.ToArray()) + ". Назовите полное имя.";
            return r;
        }
        r.Error = "Не нашёл в контактах «" + source + "».";
        return r;
    }
}

public sealed class CallTool : IAgentTool
{
    public string Name { get { return "call"; } }
    public string Description { get { return "позвонить человеку из контактов или по номеру. {\"tool\":\"call\",\"rest\":\"маме\"} или {\"tool\":\"call\",\"number\":\"+79001234567\"}"; } }

    public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        Recipient who = await Recipient.ResolveAsync(args);
        if (who.Error != null) return ToolResult.Fail(who.Error);

        string number = who.Number;
        string display = who.Display;
        switch (ActionPolicy.LevelFor("call"))
        {
            case ActionLevel.Auto:
                return ToolResult.Ok(await PhoneComms.CallNowAsync(number, display));
            case ActionLevel.Confirm:
                ActionGate.Ask(() => PhoneComms.CallNowAsync(number, display), "call");
                return ToolResult.Ok("Позвонить: " + display + "?" + ActionGate.Suffix);
            default:
                return ToolResult.Ok(PhoneComms.OpenDialer(number, display));
        }
    }
}

public sealed class MessageTool : IAgentTool
{
    public string Name { get { return "message"; } }
    public string Description { get { return "написать сообщение человеку из контактов или по номеру. {\"tool\":\"message\",\"via\":\"sms|telegram|whatsapp\",\"name\":\"маме\",\"text\":\"текст\"}"; } }

    public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        string via = ToolArgs.Get(args, "via").ToLowerInvariant();

        // Telegram подключён к агенту (TDLib): пишем прямо из приложения по режиму действий. Иначе - прежний путь: открыть чат с готовым текстом.
        string note = string.Empty;
        if (via == "telegram" && TelegramClient.HasCredentials && ActionPolicy.LevelFor("message") != ActionLevel.Manual)
        {
            TelegramSend.Attempt attempt = await TelegramSend.TryAsync(args, ActionPolicy.LevelFor("message"));
            if (attempt.Result != null) return attempt.Result;
            note = attempt.Note;
        }

        Recipient who = await Recipient.ResolveAsync(args);
        if (who.Error != null) return ToolResult.Fail(note + who.Error);

        string text = ToolArgs.Get(args, "text");
        if (text.Length == 0) text = who.Rest.Trim();
        if (text.Length > 0) text = char.ToUpperInvariant(text[0]) + text.Substring(1);

        string number = who.Number;
        string display = who.Display;

        // Telegram и WhatsApp: в любом режиме только открываем чат с готовым текстом (отправку на Android за человека сделать нельзя).
        if (via == "telegram" || via == "whatsapp")
            return ToolResult.Ok(note + PhoneComms.OpenMessengerChat(via, number, text, display));

        ActionLevel level = ActionPolicy.LevelFor("message");
        if (text.Length == 0 || level == ActionLevel.Manual)
            return ToolResult.Ok(PhoneComms.OpenSmsComposer(number, text, display));

        if (level == ActionLevel.Confirm)
        {
            ActionGate.Ask(() => PhoneComms.SendSmsNowAsync(number, text, display), "message");
            return ToolResult.Ok("Отправить SMS для " + display + ": «" + text + "»?" + ActionGate.Suffix);
        }
        return ToolResult.Ok(await PhoneComms.SendSmsNowAsync(number, text, display));
    }
}
