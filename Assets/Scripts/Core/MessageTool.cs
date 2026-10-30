public sealed class MessageTool : IAgentTool
    {
        public string Name { get { return "message"; } }
        public string Description { get { return "написать сообщение человеку из контактов или по номеру. {\"tool\":\"message\",\"via\":\"sms|telegram|whatsapp|max\",\"name\":\"маме\",\"text\":\"текст\"}"; } }

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

            // Добавим поддержку max в чат
            if (via == "max")
            {
                string text = ToolArgs.Get(args, "text");
                if (text.Length == 0) text = ToolArgs.Get(args, "rest").Trim();
                
                // Для MAX создаем Intent с передачей текста
                if (!string.IsNullOrEmpty(text))
                {
                    // Обработка как для share-intent в MAX
                    var result = PhoneComms.OpenMaxComposer(text);
                    return ToolResult.Ok(result);
                }
                else
                {
                    // Если текст не указан, открываем шаблон предварительно
                    return ToolResult.Ok("Для отправки в MAX откройте чат с готовым текстом.");
                }
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