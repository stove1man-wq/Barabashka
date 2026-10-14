using System;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;

namespace Barabashka.App
{
    /// <summary>Заглушка: ничего не слушает. Используется, пока не подключён настоящий движок.</summary>
    public sealed class NullSpeechRecognizer : ISpeechRecognizer
    {
        public string Id { get { return "null"; } }
        public RecognizerState State { get; private set; }
        public bool SupportsPartialResults { get { return false; } }
        public bool SupportsAutoLanguage { get { return false; } }

        public event Action<Utterance> OnPartial;
        public event Action<Utterance> OnFinal;
        public event Action<RecognizerState> OnStateChanged;
        public event Action<string> OnError;

        public Task InitializeAsync(RecognizerOptions options, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public void StartListening() { }
        public void StopListening() { }
        public void Dispose() { }
    }

    /// <summary>Заглушка озвучки: ничего не произносит.</summary>
    public sealed class NullTextToSpeech : ITextToSpeech
    {
        public string Id { get { return "null"; } }
        public bool SupportsLanguage(string languageCode) { return true; }

        public event Action OnStarted;
        public event Action OnFinished;

        public Task SpeakAsync(string text, string languageCode, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public void Stop() { }
        public void Dispose() { }
    }
}
