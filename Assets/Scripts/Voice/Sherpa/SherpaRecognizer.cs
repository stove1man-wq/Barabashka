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
    /// Офлайн-распознавание на sherpa-onnx-unity ровно по схеме примера пакета (он у вас работает):
    /// SherpaMicrophoneInput.ChunkReady -> накопление звука -> OfflineSpeechRecognizerComponent.TranscribeClipAsync.
    /// VAD-компонент не используется. Конец фразы определяется по паузе простым порогом громкости.
    /// OnFinal приходит после паузы в речи. Все методы вызывать из главного потока Unity.
    /// </summary>
    public sealed class SherpaRecognizer : ISpeechRecognizer
    {
        // Настройки определения речи/паузы.
        private const float SpeechRms = 0.012f;      // громкость, выше которой считаем, что идёт речь
        private const float EndSilenceSec = 0.9f;    // пауза, после которой фраза считается законченной
        private const float PreRollSec = 0.35f;      // сколько звука до начала речи добавляем к фразе
        private const float MinSpeechSec = 0.10f;    // более короткие всплески игнорируем (короткое «да» ~0.2 с)
        private const float MinClipSec = 1.0f;       // очень короткие фразы дополняем тишиной: так модель распознаёт их надёжнее
        private const float MaxSegmentSec = 25f;     // принудительно режем слишком длинную фразу

        private readonly string _asrModelId;
        private readonly bool _verboseLog;
        private readonly TimeSpan _loadTimeout;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Queue<Segment> _queue = new Queue<Segment>();
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        // Накопление текущей фразы.
        private readonly List<float> _current = new List<float>();
        private readonly List<float> _preRoll = new List<float>();
        private bool _inSpeech;
        private float _speechSec;
        private float _silenceSec;

        private GameObject _host;
        private SherpaMicrophoneInput _mic;
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

        /// <param name="asrModelId">Идентификатор офлайн-модели (папка в StreamingAssets/sherpa-onnx/models/speech-recognition).</param>
        /// <param name="vadModelId">Не используется (оставлен для совместимости вызова).</param>
        public SherpaRecognizer(string asrModelId, string vadModelId = "silero-vad",
            float loadTimeoutSec = 900f, bool verboseLog = true)
        {
            _asrModelId = asrModelId;
            _loadTimeout = TimeSpan.FromSeconds(loadTimeoutSec);
            _verboseLog = verboseLog;
        }

        public string Id { get { return "sherpa"; } }
        public RecognizerState State { get; private set; }
        public bool SupportsPartialResults { get { return false; } }
        public bool SupportsAutoLanguage { get { return false; } }

#pragma warning disable 0067
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
                // На Android модель в APK не входит: пакет скачивает её при первом запуске в память приложения.
                // Скачивание включаем явно и даём на него до 15 минут (модель около 225 МБ).
                SherpaONNXUnityAPI.SetAutoDownloadModels(true);
                SherpaONNXUnityAPI.SetDownloadAttemptTimeoutSeconds(900);

                // Как в примере: список моделей получаем заранее.
                try
                {
                    await SherpaONNXModelRegistry.Instance
                        .GetManifestAsync(SherpaONNXModuleType.SpeechRecognition, ct).ConfigureAwait(true);
                    Log("список моделей получен");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    Debug.LogWarning("[Sherpa] Не удалось получить список моделей: " + e.Message);
                }

                // Объект сразу активный, а ModelId задаём ПОСЛЕ создания компонентов — как в сцене примера,
                // где компоненты уже существуют, а модель выбирается и загружается позже.
                _host = new GameObject("[SherpaRecognizer]");
                UnityEngine.Object.DontDestroyOnLoad(_host);
                _mic = _host.AddComponent<SherpaMicrophoneInput>();
                _asr = _host.AddComponent<OfflineSpeechRecognizerComponent>();

                // Даём компонентам один кадр на Awake/OnEnable/Start.
                await Task.Yield();
                await Task.Yield();

                TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();
                UnityAction<bool> listener = null;
                listener = r => { if (r) ready.TrySetResult(true); };
                _asr.InitializationStateChangedEvent.AddListener(listener);

                try
                {
                    _asr.ModelId = _asrModelId;
                    _asr.RecognitionLanguage = IsAuto(_options.LanguageHint) ? string.Empty : _options.LanguageHint;

                    bool started = _asr.TryLoadModule();
                    Log("TryLoadModule=" + started + ", IsInitialized=" + _asr.IsInitialized);

                    // false не всегда ошибка: загрузка могла уже идти или модель уже загружена.
                    if (!_asr.IsInitialized)
                    {
                        Task delay = Task.Delay(_loadTimeout, ct);
                        Task done = await Task.WhenAny(ready.Task, delay);
                        if (done != ready.Task && !_asr.IsInitialized)
                        {
                            ct.ThrowIfCancellationRequested();
                            throw new TimeoutException("Модель не загрузилась за " +
                                _loadTimeout.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture) +
                                " с (TryLoadModule=" + started + ", ModelId=" + _asr.ModelId + "). " +
                                "Если приложение запущено первый раз, нужен интернет для скачивания модели.");
                        }
                    }
                }
                finally
                {
                    _asr.InitializationStateChangedEvent.RemoveListener(listener);
                }

                _mic.ChunkReady += HandleChunk;

                _initialized = true;
                SetState(RecognizerState.Idle);
                Log("модель загружена: " + _asrModelId);
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

            ResetSegmentState();

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

            // Недосказанную фразу всё равно распознаём, как кнопка Record в примере.
            FinishSegment();
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
                if (_mic != null)
                {
                    _mic.ChunkReady -= HandleChunk;
                    if (_mic.IsCapturing) _mic.StopCapture();
                }
                if (_asr != null) _asr.DisposeModule();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = null;
        }

        // ---- Микрофон: нарезка на фразы по паузам ----

        private void HandleChunk(float[] samples, int sampleRate)
        {
            if (_disposed || samples == null || samples.Length == 0 || sampleRate <= 0) return;

            float rms = Rms(samples);

            // Диагностика микрофона: раз в 5 секунд пишем самый громкий кусок звука.
            // Нет этих строк - звук с микрофона не приходит; уровень ниже порога - речь не будет замечена.
            if (rms > _levelMax) _levelMax = rms;
            _levelChunks++;
            double nowSec = _clock.Elapsed.TotalSeconds;
            if (nowSec - _levelLogAt >= 5.0)
            {
                Log("уровень микрофона за 5 с: max rms=" + _levelMax.ToString("F4", CultureInfo.InvariantCulture) +
                    ", порог=" + SpeechRms.ToString("F4", CultureInfo.InvariantCulture) +
                    ", кусков звука=" + _levelChunks + ", частота=" + sampleRate + ", слушаем=" + _listening);
                _levelLogAt = nowSec;
                _levelMax = 0f;
                _levelChunks = 0;
            }

            if (!_listening) return;

            float dur = samples.Length / (float)sampleRate;
            bool loud = rms >= SpeechRms;

            if (!_inSpeech)
            {
                if (loud)
                {
                    _inSpeech = true;
                    _speechSec = 0f;
                    _silenceSec = 0f;
                    _current.Clear();
                    _current.AddRange(_preRoll);
                    _current.AddRange(samples);
                    _speechSec += dur;
                    _segmentRate = sampleRate;
                    Log("речь началась (rms=" + rms.ToString("F3", CultureInfo.InvariantCulture) + ")");
                }
                else
                {
                    _preRoll.AddRange(samples);
                    int keep = (int)(PreRollSec * sampleRate);
                    if (_preRoll.Count > keep) _preRoll.RemoveRange(0, _preRoll.Count - keep);
                }
                return;
            }

            _current.AddRange(samples);
            if (loud)
            {
                _speechSec += dur;
                _silenceSec = 0f;
            }
            else
            {
                _silenceSec += dur;
            }
            _segmentRate = sampleRate;

            float total = _current.Count / (float)sampleRate;
            if (_silenceSec >= EndSilenceSec || total >= MaxSegmentSec)
                FinishSegment();
        }

        private int _segmentRate = 16000;
        private float _levelMax;
        private int _levelChunks;
        private double _levelLogAt;

        private void FinishSegment()
        {
            if (_inSpeech && _speechSec >= MinSpeechSec && _current.Count > 0)
            {
                Log("речь закончилась, длина " + (_current.Count / (float)_segmentRate).ToString("F1", CultureInfo.InvariantCulture) + " с");
                // Короткие фразы («да», «нет») дополняем тишиной до минимальной длины.
                int minSamples = (int)(MinClipSec * _segmentRate);
                if (_current.Count < minSamples)
                    _current.AddRange(new float[minSamples - _current.Count]);

                _queue.Enqueue(new Segment { Samples = _current.ToArray(), SampleRate = _segmentRate });
                if (!_pumping) PumpAsync();
            }
            ResetSegmentState();
        }

        private void ResetSegmentState()
        {
            _inSpeech = false;
            _speechSec = 0f;
            _silenceSec = 0f;
            _current.Clear();
            _preRoll.Clear();
        }

        private static float Rms(float[] s)
        {
            double sum = 0;
            for (int i = 0; i < s.Length; i++) sum += s[i] * s[i];
            return (float)Math.Sqrt(sum / s.Length);
        }

        // ---- Распознавание: по очереди ----

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
                clip = AudioClip.Create("RecordedClip", segment.Samples.Length, 1, segment.SampleRate, false);
                clip.SetData(segment.Samples, 0);

                var result = await _asr.TranscribeClipAsync(clip, _cts.Token).ConfigureAwait(true);
                if (_disposed) return;

                if (result.Status != SpeechRecognition.TranscriptionStatus.Success ||
                    string.IsNullOrWhiteSpace(result.Text))
                {
                    Log("пустой результат (status=" + result.Status + ")");
                    return;
                }

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
