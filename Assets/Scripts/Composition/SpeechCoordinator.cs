using System;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Composition
{
    /// <summary>
    /// Связывает озвучку и распознавание: пока агент говорит, микрофон выключен,
    /// чтобы агент не слышал сам себя. Вызывать из главного потока.
    /// Новая фраза обрывает предыдущую.
    /// </summary>
    public sealed class SpeechCoordinator
    {
        private const int EchoTailMs = 300;

        private readonly ISpeechRecognizer _recognizer;
        private readonly ITextToSpeech _tts;
        private CancellationTokenSource _cts;

        public SpeechCoordinator(ISpeechRecognizer recognizer, ITextToSpeech tts)
        {
            _recognizer = recognizer;
            _tts = tts;
        }

        public async void Say(string text, string languageCode)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (_cts != null) _cts.Cancel();
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            _recognizer.StopListening();
            AgentActivityBus.Publish(AgentActivity.Speaking);
            try
            {
                await _tts.SpeakAsync(text, languageCode, token);
                // Небольшая пауза: хвост звука из колонок не должен попасть в микрофон.
                await Task.Delay(EchoTailMs);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            // Если за это время началась другая фраза, микрофон включит она.
            if (!token.IsCancellationRequested)
            {
                if (AgentVoiceControl.ListeningAllowed) _recognizer.StartListening();
                AgentActivityBus.PublishResting();
            }
        }

        public void Cancel()
        {
            if (_cts != null) _cts.Cancel();
            _tts.Stop();
        }
    }
}
