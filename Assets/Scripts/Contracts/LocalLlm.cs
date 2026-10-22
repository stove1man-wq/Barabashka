using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Barabashka.Contracts
{
    /// <summary>
    /// Языковая модель, которая работает прямо на устройстве (без сети и без компьютера).
    /// Реализация подключается сама, если в проекте установлен пакет LLMUnity; без неё профиль «device» просто недоступен.
    /// </summary>
    public interface ILocalLlm
    {
        /// <summary>modelPath - полный путь к файлу .gguf. Модель загружается при первом вызове и остаётся в памяти.</summary>
        Task<string> ChatAsync(string modelPath, string system, IList<KeyValuePair<string, string>> history,
            string userText, bool jsonMode, CancellationToken ct);

        /// <summary>Загрузить модель и прогреть системный запрос заранее, чтобы первый вопрос не ждал.</summary>
        Task PreloadAsync(string modelPath, string system);

        /// <summary>Что модель делает сейчас: «не загружена», «загрузка…», «готова», «думает…».</summary>
        string Status { get; }

        /// <summary>Освободить память, занятую моделью.</summary>
        void Unload();
    }

    public sealed class LocalLlmException : Exception
    {
        /// <summary>true, если стоит повторить позже (занята прошлым запросом).</summary>
        public readonly bool Transient;

        public LocalLlmException(string message, bool transient) : base(message) { Transient = transient; }
    }

    public static class LocalLlmHost
    {
        /// <summary>Устанавливается платформенной сборкой при старте; может быть null.</summary>
        public static ILocalLlm Current;
    }
}
