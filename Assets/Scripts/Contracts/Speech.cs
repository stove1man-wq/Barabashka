using System;
using System.Threading;
using System.Threading.Tasks;

namespace Barabashka.Contracts
{
    public enum RecognizerState { Idle, Initializing, Listening, Stopping, Error }

    /// <summary>Параметры запуска распознавания.</summary>
    [Serializable]
    public sealed class RecognizerOptions
    {
        /// <summary>Подсказка языка ("ru"). null или "auto" - определять автоматически, если движок умеет.</summary>
        public string LanguageHint;
        /// <summary>Пускать промежуточные результаты (слова появляются по мере речи).</summary>
        public bool EmitPartialResults = true;
        /// <summary>Частота дискретизации входного аудио, Гц.</summary>
        public int SampleRate = 16000;
    }

    /// <summary>
    /// Распознавание речи. Потоковое: пока пользователь говорит, приходят OnPartial;
    /// когда фраза закончена, приходит один OnFinal.
    /// ВАЖНО: события могут вызываться из ЛЮБОГО потока. Подписчик, которому нужен
    /// главный поток Unity, обязан сам перейти в него через IMainThread.
    /// </summary>
    public interface ISpeechRecognizer : IDisposable
    {
        /// <summary>Уникальный идентификатор реализации ("whisper", "sherpa-ru", "null").</summary>
        string Id { get; }
        RecognizerState State { get; }
        /// <summary>true, если движок реально отдаёт промежуточные результаты.</summary>
        bool SupportsPartialResults { get; }
        /// <summary>true, если язык определяется автоматически.</summary>
        bool SupportsAutoLanguage { get; }

        event Action<Utterance> OnPartial;
        event Action<Utterance> OnFinal;
        event Action<RecognizerState> OnStateChanged;
        event Action<string> OnError;

        Task InitializeAsync(RecognizerOptions options, CancellationToken ct);
        void StartListening();
        void StopListening();
    }

    /// <summary>Озвучка. Язык задаётся при каждом вызове.</summary>
    public interface ITextToSpeech : IDisposable
    {
        string Id { get; }
        bool SupportsLanguage(string languageCode);
        event Action OnStarted;
        event Action OnFinished;

        /// <summary>Завершается, когда фраза проговорена или отменена.</summary>
        Task SpeakAsync(string text, string languageCode, CancellationToken ct);
        void Stop();
    }
}
