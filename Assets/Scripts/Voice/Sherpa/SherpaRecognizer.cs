using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using Eitan.Sherpa.Onnx.Unity.Mono.Components;
using Eitan.Sherpa.Onnx.Unity.Mono.Inputs;
using Eitan.SherpaONNXUnity.Runtime;
using Eitan.SherpaONNXUnity.Runtime.Modules;
using UnityEngine;
using UnityEngine.Events;

namespace Barabashka.Voice.SherpaEngine
{
    /// <summary>
    /// Офлайн-распознавание речи на sherpa-onnx-unity: микрофон -> VAD (silero) режет речь на фразы ->
    /// каждая фраза распознаётся моделью (например, GigaAM v2 для русского).
    /// Промежуточных результатов нет: OnFinal приходит после паузы в речи.
    /// Все методы вызывать из главного потока Unity (компоненты Sherpa работают в нём).
    /// </summary>
    public sealed class SherpaRecognizer : ISpeechRecognizer
    {
        private const float MinSegmentSec = 0.3f;

        private readonly string _asrModelId;
        private readonly string _vadModelId;
        private readonly bool _verboseLog;
        private readonly TimeSpan _loadTimeout;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Queue<Segment> _queue = new Queue<Segment>();
        private readonly List<string> _capturedLogs = new List<string>();
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        private GameObject _host;
        private SherpaMicrophoneInput _mic;
        private VoiceActivityDetectionComponent _vad;
        private OfflineSpeechRecognizerComponent _asr;
        private RecognizerOptions _options = new RecognizerOptions();
        private bool _initialized;
        private bool _listening;
        private bool _pumping;
        private bool _disposed;

        private struct Segment
        {
            public float[] Samples;
            public int SampleRate;
        }

        /// <param name="asrModelId">Идентификатор офлайн-модели распознавания (папка в StreamingAssets/sherpa-onnx/models/speech-recognition).</param>
        /// <param name="vadModelId">Идентификатор модели VAD, по умолчанию "silero-vad".</param>
        public SherpaRecognizer(string asrModelId, string vadModelId = "silero-vad",
            float loadTimeoutSec = 180f, bool verboseLog = true)
        {
            _asrModelId = asrModelId;
            _vadModelId = vadModelId;
            _loadTimeout = TimeSpan.FromSeconds(loadTimeoutSec);
            _verboseLog = verboseLog;
        }

        public string Id { get { return "sherpa"; } }
        public RecognizerState State { get; private set; }
        public bool SupportsPartialResults { get { return false; } }
        public bool SupportsAutoLanguage { get { return false; } }

#pragma warning disable 0067 // OnPartial объявлен контрактом, но офлайн-движок его не вызывает
        public event Action<Utterance> OnPartial;
#pragma warning restore 0067
        public event Action<Utterance> OnFinal;
        public event Action<RecognizerState> OnStateChanged;
        public event Action<string> OnError;

        public async Task InitializeAsync(RecognizerOptions options, CancellationToken ct)
        {
            if (_disposed) throw new ObjectDisposedException("SherpaRecognizer");
            if (_initialized) return;

            _options = options ?? new RecognizerOptions();
            SetState(RecognizerState.Initializing);

            try
            {
                // Как в примерах пакета: сначала подтягиваем список моделей (реестр), иначе
                // TryLoadModule может не найти модель по идентификатору.
                await PrefetchManifestAsync(SherpaONNXModuleType.SpeechRecognition, ct);
                await PrefetchManifestAsync(SherpaONNXModuleType.VoiceActivityDetection, ct);

                // Объект создаётся выключенным, чтобы все три компонента получили Awake одновременно при включении.
                _host = new GameObject("[SherpaRecognizer]");
                _host.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_host);

                _mic = _host.AddComponent<SherpaMicrophoneInput>();
                _vad = _host.AddComponent<VoiceActivityDetectionComponent>();
                _asr = _host.AddComponent<OfflineSpeechRecognizerComponent>();

                // Компоненты сначала включаем (как в сцене примера) и только потом задаём модель:
                // иначе они могли начать загрузку сами, и повторный TryLoadModule вернул бы false.
                _host.SetActive(true);

                _vad.BindInput(_mic);
                _vad.SpeechSegmentReady += HandleSegment;
                _vad.SpeakingStateChanged += HandleSpeaking;

                _asr.ModelId = _asrModelId;
                // В примере язык задаётся всегда; для одноязычной модели значение не критично.
                _asr.RecognitionLanguage = IsAuto(_options.LanguageHint) ? string.Empty : _options.LanguageHint;
                _vad.ModelId = _vadModelId;

                // Подписываемся на готовность ДО запуска загрузки, чтобы не пропустить событие.
                Task asrReady = WaitReady(
                    a => _asr.InitializationStateChangedEvent.AddListener(a),
                    a => _asr.InitializationStateChangedEvent.RemoveListener(a), ct);
                Task vadReady = WaitReady(
                    a => _vad.InitializationStateChangedEvent.AddListener(a),
                    a => _vad.InitializationStateChangedEvent.RemoveListener(a), ct);

                // Пакет пишет причину отказа TryLoadModule в консоль Unity; перехватываем эти строки,
                // чтобы они попали прямо в текст нашей ошибки.
                Application.logMessageReceived += CaptureLog;
                try
                {
                    if (!_asr.TryLoadModule())
                        throw new InvalidOperationException("Не удалось начать загрузку модели распознавания: " + _asrModelId + CapturedLogs());
                    if (!_vad.TryLoadModule())
                        throw new InvalidOperationException("Не удалось начать загрузку модели VAD: " + _vadModelId + CapturedLogs());

                    await Task.WhenAll(asrReady, vadReady);
                }
                finally
                {
                    Application.logMessageReceived -= CaptureLog;
                }

                _initialized = true;
                SetState(RecognizerState.Idle);
                Log("модели загружены: " + _asrModelId + " + " + _vadModelId);
            }
            catch (Exception e)
            {
                SetState(RecognizerState.Error);
                RaiseError(e.Message);
                throw;
            }
        }

        public void StartListening()
        {
            if (_disposed || !_initialized || _listening) return;

            if (!_mic.IsCapturing && !_mic.TryStartCapture())
            {
                RaiseError("Не удалось включить микрофон.");
                SetState(RecognizerState.Error);
                return;
            }

            _listening = true;
            SetState(RecognizerState.Listening);
        }

        public void StopListening()
        {
            if (_disposed || !_listening) return;

            _listening = false;
            try { _mic.StopCapture(); }
            catch (Exception e) { Debug.LogException(e); }
            SetState(RecognizerState.Idle);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _listening = false;
            _cts.Cancel();
            _queue.Clear();

            try
            {
                if (_vad != null)
                {
                    _vad.SpeechSegmentReady -= HandleSegment;
                    _vad.SpeakingStateChanged -= HandleSpeaking;
                }
                if (_mic != null && _mic.IsCapturing) _mic.StopCapture();
                if (_vad != null) _vad.DisposeModule();
                if (_asr != null) _asr.DisposeModule();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = null;
        }

        private void HandleSpeaking(bool speaking)
        {
            Log(speaking ? "VAD: речь началась" : "VAD: речь закончилась");
        }

        private void HandleSegment(float[] samples, int sampleRate)
        {
            if (_disposed || !_listening || samples == null || samples.Length == 0 || sampleRate <= 0) return;
            if (samples.Length / (float)sampleRate < MinSegmentSec) return;

            _queue.Enqueue(new Segment { Samples = samples, SampleRate = sampleRate });
            if (!_pumping) PumpAsync();
        }

        // Фразы распознаются строго по очереди, чтобы не запускать два распознавания одновременно.
        private async void PumpAsync()
        {
            _pumping = true;
            try
            {
                while (_queue.Count > 0 && !_disposed)
                {
                    Segment segment = _queue.Dequeue();
                    await TranscribeAsync(segment);
                }
            }
            finally
            {
                _pumping = false;
            }
        }

        private async Task TranscribeAsync(Segment segment)
        {
            AudioClip clip = null;
            try
            {
                clip = AudioClip.Create("SherpaSegment", segment.Samples.Length, 1, segment.SampleRate, false);
                clip.SetData(segment.Samples, 0);

                var result = await _asr.TranscribeClipAsync(clip, _cts.Token).ConfigureAwait(true);
                if (_disposed) return;

                if (result.Status != SpeechRecognition.TranscriptionStatus.Success ||
                    string.IsNullOrWhiteSpace(result.Text))
                    return;

                string text = result.Text.Trim();
                Log("FINAL: " + text);

                string lang = IsAuto(_options.LanguageHint) ? null : _options.LanguageHint;
                Action<Utterance> handler = OnFinal;
                if (handler != null) handler(new Utterance(text, lang, true, InputSource.Voice));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                RaiseError("Ошибка распознавания: " + e.Message);
            }
            finally
            {
                if (clip != null) UnityEngine.Object.Destroy(clip);
            }
        }

        private Task WaitReady(Action<UnityAction<bool>> add, Action<UnityAction<bool>> remove, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            UnityAction<bool> listener = null;
            listener = ready =>
            {
                if (!ready) return;
                remove(listener);
                tcs.TrySetResult(true);
            };
            add(listener);

            return WaitWithTimeout(tcs.Task, remove, listener, ct);
        }

        private async Task WaitWithTimeout(Task ready, Action<UnityAction<bool>> remove,
            UnityAction<bool> listener, CancellationToken ct)
        {
            Task delay = Task.Delay(_loadTimeout, ct);
            Task finished = await Task.WhenAny(ready, delay);
            if (finished != ready)
            {
                remove(listener);
                ct.ThrowIfCancellationRequested();
                throw new TimeoutException("Модель Sherpa не загрузилась за " + _loadTimeout.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture) +
                    " с. Проверьте папку StreamingAssets/sherpa-onnx/models и интернет (VAD может скачиваться при первом запуске).");
            }
            await ready;
        }

        private void CaptureLog(string message, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(message)) return;
            bool relevant = message.StartsWith("[OfflineSpeechRecognizerComponent]", StringComparison.Ordinal) ||
                            message.StartsWith("[VoiceActivityDetectionComponent]", StringComparison.Ordinal) ||
                            message.IndexOf("Sherpa", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!relevant) return;
            lock (_capturedLogs)
            {
                if (_capturedLogs.Count < 12) _capturedLogs.Add(message);
            }
        }

        private string CapturedLogs()
        {
            lock (_capturedLogs)
            {
                if (_capturedLogs.Count == 0) return string.Empty;
                return "\nСообщения Sherpa:\n" + string.Join("\n", _capturedLogs);
            }
        }

        private async Task PrefetchManifestAsync(SherpaONNXModuleType type, CancellationToken ct)
        {
            try
            {
                await SherpaONNXModelRegistry.Instance.GetManifestAsync(type, ct).ConfigureAwait(true);
                Log("список моделей получен: " + type);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                // Без интернета список может не получиться; пробуем грузить уже скачанную модель как есть.
                Debug.LogWarning("[Sherpa] Не удалось получить список моделей (" + type + "): " + e.Message);
            }
        }

        private static bool IsAuto(string hint)
        {
            return string.IsNullOrEmpty(hint) || string.Equals(hint, "auto", StringComparison.OrdinalIgnoreCase);
        }

        private void Log(string message)
        {
            if (!_verboseLog) return;
            Debug.Log("[Sherpa t=" + _clock.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + "] " + message);
        }

        private void SetState(RecognizerState state)
        {
            if (State == state) return;
            State = state;
            Action<RecognizerState> handler = OnStateChanged;
            if (handler != null) handler(state);
        }

        private void RaiseError(string message)
        {
            Action<string> handler = OnError;
            if (handler != null) handler(message);
        }
    }
}
