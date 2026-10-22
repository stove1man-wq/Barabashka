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
using UnityEngine.Android;
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

                // На телефоне агент пишет в чат, что происходит: первая загрузка модели может занять несколько минут.
                // Пока модель грузится, в строке статуса - только запись об этом; в чат пишем после готовности.
                _agent.ShowSystemNotice("Подготовка голосового ввода…", false);

                bool micAllowed = await EnsureMicrophoneAsync(_cts.Token);
                if (!micAllowed)
                {
                    _agent.ShowSystemNotice(
                        "Нет доступа к микрофону: голосовой ввод выключен. Разрешите микрофон в настройках приложения. Текстовый ввод работает.",
                        true);
                    AgentActivityBus.Publish(AgentActivity.Error);
                    return;
                }

                _tts = CreateTts();

                _recognizer = CreateRecognizer();
                _recognizer.OnError += message =>
                {
                    Debug.LogError("[Voice] " + message);
                    PostToMainThread(() => { if (_agent != null) _agent.ShowSystemNotice("Голосовой ввод: " + message, true); });
                };

                _agent.ShowSystemNotice(
                    "Загружаю голосовую модель. При первом запуске она скачивается (около 225 МБ), нужен интернет.",
                    false);

                Debug.Log("[Voice] Загружаю модель распознавания (" + _recognizer.Id + ")...");
                await _recognizer.InitializeAsync(
                    new RecognizerOptions { LanguageHint = Language, EmitPartialResults = true },
                    _cts.Token);

                _speech = new SpeechCoordinator(_recognizer, _tts);
                AgentCommandRouter.ResponseShown += HandleAgentResponse;
                AgentCommandRouter.AnnouncementShown += HandleAnnouncement;

                _channel = new VoiceInputChannel(_recognizer);
                _channel.OnUtterance += HandleUtterance;
                Debug.Log("[Voice] Распознавание (" + _recognizer.Id + ") готово, слушаю микрофон.");

                AgentVoiceControl.MutedChanged += OnMutedChanged;
                AgentVoiceControl.StopSpeakingRequested += OnStopSpeaking;
                _agent.ShowReady();
                ApplyListening();
                _speech.Say("Голосовой ввод готов.", "ru");
            }
            catch (OperationCanceledException)
            {
                // приложение закрывается
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (_agent != null)
                {
                    _agent.ShowSystemNotice("Голосовой ввод недоступен: " + e.Message + " Текстовый ввод работает.", true);
                    AgentActivityBus.VoiceActive = false;
                    AgentActivityBus.Publish(AgentActivity.Error);
                }
            }
        }

        // Микрофон работает, только пока приложение активно и не выключено пользователем.
        private void ApplyListening()
        {
            if (_channel == null) return;

            if (AgentVoiceControl.ListeningAllowed)
            {
                _channel.Start();
                AgentActivityBus.VoiceActive = true;
            }
            else
            {
                _channel.Stop();
                AgentActivityBus.VoiceActive = false;
            }
            AgentActivityBus.PublishResting();
        }

        private void OnMutedChanged(bool muted)
        {
            ApplyListening();
        }

        private void OnStopSpeaking()
        {
            if (_speech != null) _speech.Cancel();
            ApplyListening(); // микрофон возвращается после прерванной озвучки
        }

        private void OnApplicationPause(bool paused)
        {
            AgentVoiceControl.AppPaused = paused;
            ApplyListening();
        }

        // Android: микрофон нужно запросить у пользователя. На других платформах разрешение не требуется.
        private static async Task<bool> EnsureMicrophoneAsync(CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone)) return true;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => tcs.TrySetResult(true);
            callbacks.PermissionDenied += _ => tcs.TrySetResult(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => tcs.TrySetResult(false);
            Permission.RequestUserPermissions(new[] { Permission.Microphone }, callbacks);

            Task finished = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(2), ct));
            return finished == tcs.Task && tcs.Task.Result;
#else
            await Task.CompletedTask;
            return true;
#endif
        }

        private static void PostToMainThread(Action action)
        {
            IMainThread main = MainThreadDispatcher.Instance;
            if (main == null) action();
            else main.Post(action);
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
#elif UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidSystemTts();
#else
            return new NullTextToSpeech();
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

        // Ответ агента озвучивается, только если вопрос был задан голосом.
        private void HandleAgentResponse(string text)
        {
            if (AgentManager.LastInputSource != InputSource.Voice) return;
            // «Покажи / выведи» - ответ только на экране, без озвучки.
            if (AgentManager.LastRequestShowOnly) return;
            if (!AppSettings.SpeakReplies) return;

            string speech = SpeechText(text);
            IMainThread main = MainThreadDispatcher.Instance;
            if (main == null) SpeakNow(speech);
            else main.Post(() => SpeakNow(speech));
        }

        // Напоминания и таймеры озвучиваются всегда: человек их ждёт, независимо от того, как задавал вопрос.
        private void HandleAnnouncement(string text)
        {
            string speech = SpeechText(text);
            IMainThread main = MainThreadDispatcher.Instance;
            if (main == null) SpeakNow(speech);
            else main.Post(() => SpeakNow(speech));
        }

        private void SpeakNow(string speech)
        {
            if (_speech != null && !string.IsNullOrWhiteSpace(speech)) _speech.Say(speech, "ru");
        }

        // Готовит текст для озвучки: без переводов строк, скобок со сроками и служебного "UTC".
        private static string SpeechText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string result = System.Text.RegularExpressions.Regex.Replace(text, @"\s*\([^)]*\)", string.Empty);
            result = result.Replace("UTC", string.Empty)
                           .Replace("\r", " ")
                           .Replace("\n", ". ")
                           .Replace("«", string.Empty)
                           .Replace("»", string.Empty);
            result = System.Text.RegularExpressions.Regex.Replace(result, @"([:;,.])\s*\.", "$1");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s{2,}", " ").Trim();

            const int maxChars = 300;
            if (result.Length > maxChars) result = result.Substring(0, maxChars).TrimEnd() + "…";
            return result;
        }

        private void OnDestroy()
        {
            AgentCommandRouter.ResponseShown -= HandleAgentResponse;
            AgentCommandRouter.AnnouncementShown -= HandleAnnouncement;
            AgentVoiceControl.MutedChanged -= OnMutedChanged;
            AgentVoiceControl.StopSpeakingRequested -= OnStopSpeaking;
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
