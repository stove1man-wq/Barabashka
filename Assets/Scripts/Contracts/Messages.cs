using System;

namespace Barabashka.Contracts
{
    /// <summary>Откуда пришла реплика пользователя.</summary>
    public enum InputSource { Text = 0, Voice = 1, System = 2 }

    /// <summary>
    /// Реплика пользователя. Одна и та же структура для текста и голоса.
    /// IsFinal == false означает промежуточный результат потокового распознавания:
    /// текст может измениться со следующим сообщением.
    /// LanguageCode - код ISO 639-1 ("ru", "en") или null, если язык неизвестен.
    /// </summary>
    [Serializable]
    public sealed class Utterance
    {
        public string Text;
        public string LanguageCode;
        public bool IsFinal = true;
        public float Confidence = 1f;
        public InputSource Source = InputSource.Text;
        public DateTime TimestampUtc = DateTime.UtcNow;

        public Utterance() { }

        public Utterance(string text, string languageCode, bool isFinal, InputSource source)
        {
            Text = text;
            LanguageCode = languageCode;
            IsFinal = isFinal;
            Source = source;
        }
    }

    /// <summary>
    /// Ответ агента. Command - необязательная системная команда для роутера
    /// (то, что сейчас передаётся строкой с префиксом COMMAND:).
    /// Animation - имя триггера анимации аватара или null.
    /// </summary>
    [Serializable]
    public sealed class AgentResponse
    {
        public string Text;
        public string LanguageCode;
        public string Command;
        public string Animation;
    }

    /// <summary>Результат выполнения инструмента.</summary>
    [Serializable]
    public sealed class ToolResult
    {
        public bool Success;
        public string Output;
        public string Error;

        public static ToolResult Ok(string output) { return new ToolResult { Success = true, Output = output }; }
        public static ToolResult Fail(string error) { return new ToolResult { Success = false, Error = error }; }
    }
}
