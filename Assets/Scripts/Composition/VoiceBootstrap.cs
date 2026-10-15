using System;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.App;
using Barabashka.Contracts;
using Barabashka.Voice.WhisperEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using Barabashka.Voice.WindowsTts;
#endif
using UnityEngine;

namespace Barabashka.Composition
{
    /// <summary>
    /// Единственное место, где голосовой блок соединяется с ядром.
    /// Создаётся сам при старте сцены; ждёт, пока AppBootstrap создаст AgentManager.
    /// </summary>
    public sealed class VoiceBootstrap : MonoBehaviour
    {
        // Настройки уровня (позже переедут в CapabilityProfile).
        private const string ModelPath = "Whisper/ggml-small.bin";
        private const string Language = "ru";     // "auto" или код языка. "auto" на коротких кусках звука даёт ошибку -3
        private const bool UseGpu = false;        // true: Vulkan на видеокарте, если поддерживается

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private ISpeechRecognizer _recognizer;
        private ITextToSpeech _tts;
        private SpeechCoordinator _speech;
        private VoiceInputChannel _channel;
        private AgentManager _agent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("[VoiceBootstrap]");
            DontDestroyOnLoad(go);
            go.AddComponent<VoiceBootstrap>();
        }

        private async void Start()
        {
            try
            {
                float waited = 0f;
                while (_agent == null && waited < 10f)
                {
                    _agent = FindFirstObjectByType<AgentManager>();
                    if (_agent != null) break;
                    waited += Time.unscaledDeltaTime;
                    await Task.Yield();
                }

                if (_agent == null)
                {
                    Debug.LogWarning("[Voice] AgentManager не найден, голосовой ввод не запущен.");
                    return;
                }

                _tts = CreateTts();

                _recognizer = new WhisperRecognizer(ModelPath, true, UseGpu);
                _recognizer.OnError += message => Debug.LogError("[Voice] " + message);

                Debug.Log("[Voice] Загружаю модель Whisper...");
                await _recognizer.InitializeAsync(
                    new RecognizerOptions { LanguageHint = Language, EmitPartialResults = true },
                    _cts.Token);

                _speech = new SpeechCoordinator(_recognizer, _tts);

                _channel = new VoiceInputChannel(_recognizer);
                _channel.OnUtterance += HandleUtterance;
                _channel.Start();
                Debug.Log("[Voice] Whisper готов, слушаю микрофон.");

                // Временная проверка озвучки; уберём, когда ответы агента начнут озвучиваться сами.
                _speech.Say("Голосовой ввод готов.", "ru");
            }
            catch (OperationCanceledException)
            {
                // приложение закрывается
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static ITextToSpeech CreateTts()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            var windows = new WindowsSystemTts();
            windows.WarmUp();
            return windows;
#else
            return new NullTextToSpeech(); // Android: встроенный синтезатор будет отдельной реализацией
#endif
        }

        private void HandleUtterance(Utterance utterance)
        {
            IMainThread main = MainThreadDispatcher.Instance;
            if (main == null) Apply(utterance);
            else main.Post(() => Apply(utterance));
        }

        private void Apply(Utterance utterance)
        {
            if (_agent != null) _agent.ShowRecognizedVoiceText(utterance);
        }

        private void OnDestroy()
        {
            _cts.Cancel();
            if (_channel != null)
            {
                _channel.OnUtterance -= HandleUtterance;
                _channel.Stop();
            }
            if (_speech != null) _speech.Cancel();
            if (_tts != null) _tts.Dispose();
            if (_recognizer != null) _recognizer.Dispose();
        }
    }
}
