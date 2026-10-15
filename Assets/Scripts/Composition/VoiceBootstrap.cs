using System;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.App;
using Barabashka.Contracts;
using Barabashka.Voice.SherpaEngine;
using Barabashka.Voice.WhisperEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using Barabashka.Voice.WindowsTts;
#endif
using UnityEngine;

namespace Barabashka.Composition
{
    /// <summary>
    /// Ð•Ð´Ð¸Ð½ÑÑ‚Ð²ÐµÐ½Ð½Ð¾Ðµ Ð¼ÐµÑÑ‚Ð¾, Ð³Ð´Ðµ Ð³Ð¾Ð»Ð¾ÑÐ¾Ð²Ð¾Ð¹ Ð±Ð»Ð¾Ðº ÑÐ¾ÐµÐ´Ð¸Ð½ÑÐµÑ‚ÑÑ Ñ ÑÐ´Ñ€Ð¾Ð¼.
    /// Ð¡Ð¾Ð·Ð´Ð°Ñ‘Ñ‚ÑÑ ÑÐ°Ð¼ Ð¿Ñ€Ð¸ ÑÑ‚Ð°Ñ€Ñ‚Ðµ ÑÑ†ÐµÐ½Ñ‹; Ð¶Ð´Ñ‘Ñ‚, Ð¿Ð¾ÐºÐ° AppBootstrap ÑÐ¾Ð·Ð´Ð°ÑÑ‚ AgentManager.
    /// </summary>
    public sealed class VoiceBootstrap : MonoBehaviour
    {
        // ÐÐ°ÑÑ‚Ñ€Ð¾Ð¹ÐºÐ¸ ÑƒÑ€Ð¾Ð²Ð½Ñ (Ð¿Ð¾Ð·Ð¶Ðµ Ð¿ÐµÑ€ÐµÐµÐ´ÑƒÑ‚ Ð² CapabilityProfile).
        private const bool UseSherpa = false;      // false: Ð²ÐµÑ€Ð½ÑƒÑ‚ÑŒÑÑ Ð½Ð° Whisper (Ð¿Ð¾ÐºÐ° Ð¾Ð½ Ð½Ðµ ÑƒÐ´Ð°Ð»Ñ‘Ð½)
        private const string Language = "ru";     // "auto" Ð¸Ð»Ð¸ ÐºÐ¾Ð´ ÑÐ·Ñ‹ÐºÐ°. Ð”Ð»Ñ GigaAM v2 Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ñ€ÑƒÑÑÐºÐ¸Ð¹

        // Sherpa: Ð¼Ð¾Ð´ÐµÐ»Ð¸ Ð»ÐµÐ¶Ð°Ñ‚ Ð² StreamingAssets/sherpa-onnx/models/... (Ð² Git Ð½Ðµ Ð²Ñ…Ð¾Ð´ÑÑ‚)
        private const string SherpaAsrModelId = "sherpa-onnx-nemo-ctc-giga-am-v2-russian-2025-04-19";
        private const string SherpaVadModelId = "silero-vad";

        // Whisper (ÑÑ‚Ð°Ñ€Ñ‹Ð¹ Ð¼Ð¾Ð´ÑƒÐ»ÑŒ)
        private const string WhisperModelPath = "Whisper/ggml-small.bin";
        private const bool WhisperUseGpu = false;

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
                    Debug.LogWarning("[Voice] AgentManager Ð½Ðµ Ð½Ð°Ð¹Ð´ÐµÐ½, Ð³Ð¾Ð»Ð¾ÑÐ¾Ð²Ð¾Ð¹ Ð²Ð²Ð¾Ð´ Ð½Ðµ Ð·Ð°Ð¿ÑƒÑ‰ÐµÐ½.");
                    return;
                }

                _tts = CreateTts();

                _recognizer = CreateRecognizer();
                _recognizer.OnError += message => Debug.LogError("[Voice] " + message);

                Debug.Log("[Voice] Ð—Ð°Ð³Ñ€ÑƒÐ¶Ð°ÑŽ Ð¼Ð¾Ð´ÐµÐ»ÑŒ Ñ€Ð°ÑÐ¿Ð¾Ð·Ð½Ð°Ð²Ð°Ð½Ð¸Ñ (" + _recognizer.Id + ")...");
                await _recognizer.InitializeAsync(
                    new RecognizerOptions { LanguageHint = Language, EmitPartialResults = true },
                    _cts.Token);

                _speech = new SpeechCoordinator(_recognizer, _tts);

                _channel = new VoiceInputChannel(_recognizer);
                _channel.OnUtterance += HandleUtterance;
                _channel.Start();
                Debug.Log("[Voice] Ð Ð°ÑÐ¿Ð¾Ð·Ð½Ð°Ð²Ð°Ð½Ð¸Ðµ (" + _recognizer.Id + ") Ð³Ð¾Ñ‚Ð¾Ð²Ð¾, ÑÐ»ÑƒÑˆÐ°ÑŽ Ð¼Ð¸ÐºÑ€Ð¾Ñ„Ð¾Ð½.");

                // Ð’Ñ€ÐµÐ¼ÐµÐ½Ð½Ð°Ñ Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ° Ð¾Ð·Ð²ÑƒÑ‡ÐºÐ¸; ÑƒÐ±ÐµÑ€Ñ‘Ð¼, ÐºÐ¾Ð³Ð´Ð° Ð¾Ñ‚Ð²ÐµÑ‚Ñ‹ Ð°Ð³ÐµÐ½Ñ‚Ð° Ð½Ð°Ñ‡Ð½ÑƒÑ‚ Ð¾Ð·Ð²ÑƒÑ‡Ð¸Ð²Ð°Ñ‚ÑŒÑÑ ÑÐ°Ð¼Ð¸.
                _speech.Say("Ð“Ð¾Ð»Ð¾ÑÐ¾Ð²Ð¾Ð¹ Ð²Ð²Ð¾Ð´ Ð³Ð¾Ñ‚Ð¾Ð².", "ru");
            }
            catch (OperationCanceledException)
            {
                // Ð¿Ñ€Ð¸Ð»Ð¾Ð¶ÐµÐ½Ð¸Ðµ Ð·Ð°ÐºÑ€Ñ‹Ð²Ð°ÐµÑ‚ÑÑ
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static ISpeechRecognizer CreateRecognizer()
        {
            if (UseSherpa) return new SherpaRecognizer(SherpaAsrModelId, SherpaVadModelId);
            return new WhisperRecognizer(WhisperModelPath, true, WhisperUseGpu);
        }

        private static ITextToSpeech CreateTts()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            var windows = new WindowsSystemTts();
            windows.WarmUp();
            return windows;
#else
            return new NullTextToSpeech(); // Android: Ð²ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ð¹ ÑÐ¸Ð½Ñ‚ÐµÐ·Ð°Ñ‚Ð¾Ñ€ Ð±ÑƒÐ´ÐµÑ‚ Ð¾Ñ‚Ð´ÐµÐ»ÑŒÐ½Ð¾Ð¹ Ñ€ÐµÐ°Ð»Ð¸Ð·Ð°Ñ†Ð¸ÐµÐ¹
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

