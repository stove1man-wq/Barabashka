using System;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.App;
using Barabashka.Contracts;
using Barabashka.Voice.SherpaEngine;
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
        private const string Language = "ru";     // "auto" или код языка. Для GigaAM v2 только русский

        // Sherpa: модели лежат в StreamingAssets/sherpa-onnx/models/... (в Git не входят)
        private const string SherpaAsrModelId = "sherpa-onnx-nemo-ctc-giga-am-v2-russian-2025-04-19";
        private const string SherpaVadModelId = "silero-vad";

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

                _agent.ResponseReady += HandleAgentResponse;
                _tts = CreateTts();

                _recognizer = CreateRecognizer();
                _recognizer.OnError += message => Debug.LogError("[Voice] " + message);

                Debug.Log("[Voice] Загружаю модель распознавания (" + _recognizer.Id + ")...");
                await _recognizer.InitializeAsync(
                    new RecognizerOptions { LanguageHint = Language, EmitPartialResults = true },
                    _cts.Token);

                _speech = new SpeechCoordinator(_recognizer, _tts);

                _channel = new VoiceInputChannel(_recognizer);
                _channel.OnUtterance += HandleUtterance;
                _channel.Start();
                Debug.Log("[Voice] Распознавание (" + _recognizer.Id + ") готово, слушаю микрофон.");

                Debug.Log("[Voice] Голосовой цикл готов: финальная реплика передаётся агенту, ответы озвучиваются.");
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

        private static ISpeechRecognizer CreateRecognizer()
        {
            return new SherpaRecognizer(SherpaAsrModelId, SherpaVadModelId);
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
            if (_agent != null) _agent.SendUtterance(utterance);
        }

        private void HandleAgentResponse(AgentResponse response)
        {
            if (_speech == null || response == null || string.IsNullOrWhiteSpace(response.Text)) return;
            string language = string.IsNullOrWhiteSpace(response.LanguageCode) ? Language : response.LanguageCode;
            _speech.Say(response.Text, language);
        }

        private void OnDestroy()
        {
            if (_agent != null) _agent.ResponseReady -= HandleAgentResponse;
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
