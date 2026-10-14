using System;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.App;
using Barabashka.Contracts;
using Barabashka.Voice.WhisperEngine;
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
        private const string Language = "ru";   // "auto" или код языка, например "ru"
        private const bool UseGpu = false;        // true: Vulkan на видеокарте, если поддерживается

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private ISpeechRecognizer _recognizer;
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

                _recognizer = new WhisperRecognizer(ModelPath, true, UseGpu);
                _recognizer.OnError += message => Debug.LogError("[Voice] " + message);

                Debug.Log("[Voice] Загружаю модель Whisper...");
                await _recognizer.InitializeAsync(
                    new RecognizerOptions { LanguageHint = Language, EmitPartialResults = true },
                    _cts.Token);

                _channel = new VoiceInputChannel(_recognizer);
                _channel.OnUtterance += HandleUtterance;
                _channel.Start();
                Debug.Log("[Voice] Whisper готов, слушаю микрофон.");
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

        private void OnDestroy()
        {
            _cts.Cancel();
            if (_channel != null)
            {
                _channel.OnUtterance -= HandleUtterance;
                _channel.Stop();
            }
            if (_recognizer != null) _recognizer.Dispose();
        }
    }
}

