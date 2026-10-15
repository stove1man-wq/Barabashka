using System;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.App;
using Barabashka.Contracts;
using Barabashka.Voice.SherpaEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using Barabashka.Voice.WindowsTts;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
using Barabashka.Voice.AndroidTts;
#endif
using UnityEngine;

namespace Barabashka.Composition
{
    /// <summary>Connects speech recognition to the agent and speaks the agent response.</summary>
    public sealed class VoiceBootstrap : MonoBehaviour
    {
        private const string Language = "ru";
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
                    Debug.LogError("[Voice] AgentManager was not found; voice interaction cannot start.");
                    return;
                }

                _agent.ResponseReady += HandleAgentResponse;
                _tts = CreateTts();
                _recognizer = new SherpaRecognizer(SherpaAsrModelId, SherpaVadModelId);
                _recognizer.OnError += message => Debug.LogError("[Voice] " + message);

                Debug.Log("[Voice] Loading speech model: " + _recognizer.Id);
                await _recognizer.InitializeAsync(
                    new RecognizerOptions { LanguageHint = Language, EmitPartialResults = true },
                    _cts.Token);

                _speech = new SpeechCoordinator(_recognizer, _tts);
                _channel = new VoiceInputChannel(_recognizer);
                _channel.OnUtterance += HandleUtterance;
                _channel.Start();
                Debug.Log("[Voice] Ready. Listening for Russian speech.");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogException(e); }
        }

        private static ITextToSpeech CreateTts()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            var tts = new WindowsSystemTts();
            tts.WarmUp();
            return tts;
#elif UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidSystemTts();
#else
            return new SilentTts();
#endif
        }

        private void HandleUtterance(Utterance utterance)
        {
            IMainThread main = MainThreadDispatcher.Instance;
            if (main == null) ApplyUtterance(utterance);
            else main.Post(() => ApplyUtterance(utterance));
        }

        private void ApplyUtterance(Utterance utterance)
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
            _cts.Dispose();
        }

        private sealed class SilentTts : ITextToSpeech
        {
            public string Id => "silent";
            public event Action OnStarted;
            public event Action OnFinished;
            public bool SupportsLanguage(string languageCode) => false;
            public Task SpeakAsync(string text, string languageCode, CancellationToken ct) => Task.CompletedTask;
            public void Stop() { }
            public void Dispose() { }
        }
    }
}
