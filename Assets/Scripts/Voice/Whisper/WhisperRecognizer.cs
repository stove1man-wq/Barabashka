using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;
using Whisper;
using Whisper.Utils;

// Пространство имён намеренно не "Barabashka.Voice.Whisper": иначе имя Whisper
// внутри него указывало бы на наше пространство, а не на пакет whisper.unity.
namespace Barabashka.Voice.WhisperEngine
{
    /// <summary>
    /// Потоковое распознавание речи на whisper.unity (многоязычное, офлайн).
    /// Промежуточный текст = OnSegmentUpdated, финальная реплика = OnSegmentFinished
    /// (срабатывает на паузе в речи при включённом VAD или когда заполнилось окно).
    /// InitializeAsync, StartListening и StopListening вызывать из главного потока Unity.
    /// </summary>
    public sealed class WhisperRecognizer : ISpeechRecognizer
    {
        private readonly string _modelPath;
        private readonly bool _modelInStreamingAssets;
        private readonly bool _useGpu;
        private readonly float _stepSec;
        private readonly float _lengthSec;

        private GameObject _host;
        private WhisperManager _manager;
        private MicrophoneRecord _mic;
        private WhisperStream _stream;
        private RecognizerOptions _options = new RecognizerOptions();
        private bool _listening;
        private bool _stopping;
        private bool _pendingStart;
        private bool _disposed;

        /// <param name="modelPath">Путь к модели относительно StreamingAssets.</param>
        /// <param name="stepSec">Как часто обновляется текст (меньше - быстрее, но тяжелее для ПК).</param>
        /// <param name="lengthSec">Длина окна до принудительного завершения реплики.</param>
        public WhisperRecognizer(string modelPath = "Whisper/ggml-small.bin",
            bool modelInStreamingAssets = true, bool useGpu = false,
            float stepSec = 1.5f, float lengthSec = 10f)
        {
            _modelPath = modelPath;
            _modelInStreamingAssets = modelInStreamingAssets;
            _useGpu = useGpu;
            _stepSec = stepSec;
            _lengthSec = lengthSec;
        }

        public string Id { get { return "whisper"; } }
        public RecognizerState State { get; private set; }
        public bool SupportsPartialResults { get { return true; } }
        public bool SupportsAutoLanguage { get { return true; } }

        public event Action<Utterance> OnPartial;
        public event Action<Utterance> OnFinal;
        public event Action<RecognizerState> OnStateChanged;
        public event Action<string> OnError;

        public async Task InitializeAsync(RecognizerOptions options, CancellationToken ct)
        {
            if (_disposed) throw new ObjectDisposedException("WhisperRecognizer");
            if (_stream != null) return;

            _options = options ?? new RecognizerOptions();
            SetState(RecognizerState.Initializing);

            try
            {
                // Объект создаётся выключенным: WhisperManager загружает модель в Awake,
                // а путь к модели нужно успеть задать до этого.
                _host = new GameObject("[WhisperRecognizer]");
                _host.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_host);

                _manager = _host.AddComponent<WhisperManager>();
                _manager.ModelPath = _modelPath;
                _manager.IsModelPathInStreamingAssets = _modelInStreamingAssets;
                _manager.language = IsAuto(_options.LanguageHint) ? "auto" : _options.LanguageHint;
                _manager.stepSec = _stepSec;
                _manager.lengthSec = _lengthSec;
                _manager.useVad = true;
                TrySetPrivateBool(_manager, "useGpu", _useGpu);

                _mic = _host.AddComponent<MicrophoneRecord>();
                _mic.frequency = _options.SampleRate > 0 ? _options.SampleRate : 16000;
                _mic.loop = true;     // иначе запись остановится через maxLengthSec
                _mic.echo = false;    // иначе записанная речь проигрывается обратно
                _mic.useVad = true;
                _mic.chunksLengthSec = 0.5f;

                _host.SetActive(true);

                while (_manager.IsLoading)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (!_manager.IsLoaded)
                    throw new InvalidOperationException(
                        "Модель Whisper не загрузилась. Проверьте файл StreamingAssets/" + _modelPath);

                _stream = await _manager.CreateStream(_mic);
                if (_stream == null)
                    throw new InvalidOperationException("Не удалось создать поток распознавания Whisper.");

                _stream.OnSegmentUpdated += HandleSegmentUpdated;
                _stream.OnSegmentFinished += HandleSegmentFinished;
                _stream.OnStreamFinished += HandleStreamFinished;

                SetState(RecognizerState.Idle);
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
            if (_disposed || _stream == null) return;
            if (_stopping) { _pendingStart = true; return; }
            if (_listening) return;

            _listening = true;
            _stream.StartStream();
            _mic.StartRecord();
            SetState(RecognizerState.Listening);
        }

        public void StopListening()
        {
            _pendingStart = false;
            if (_disposed || !_listening) return;

            _listening = false;
            _stopping = true;
            SetState(RecognizerState.Stopping);

            // Остановка микрофона сама вызывает StopStream у потока (он подписан на OnRecordStop).
            if (_mic.IsRecording) _mic.StopRecord();
            else _stream.StopStream();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_stream != null)
                {
                    _stream.OnSegmentUpdated -= HandleSegmentUpdated;
                    _stream.OnSegmentFinished -= HandleSegmentFinished;
                    _stream.OnStreamFinished -= HandleStreamFinished;
                }
                if (_mic != null && _mic.IsRecording) _mic.StopRecord();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = null;
            _stream = null;
        }

        private void HandleSegmentUpdated(WhisperResult segment)
        {
            if (!_options.EmitPartialResults) return;
            string text = Clean(segment);
            if (text == null) return;
            Action<Utterance> handler = OnPartial;
            if (handler != null) handler(MakeUtterance(text, false));
        }

        private void HandleSegmentFinished(WhisperResult segment)
        {
            string text = Clean(segment);
            if (text == null) return;
            Action<Utterance> handler = OnFinal;
            if (handler != null) handler(MakeUtterance(text, true));
        }

        private void HandleStreamFinished(string finalText)
        {
            _stopping = false;
            if (_pendingStart && !_disposed)
            {
                _pendingStart = false;
                StartListening();
            }
            else
            {
                SetState(RecognizerState.Idle);
            }
        }

        private Utterance MakeUtterance(string text, bool isFinal)
        {
            // Язык, определённый Whisper, здесь не читается (API результата не проверен),
            // поэтому при автоопределении код языка остаётся null.
            string lang = IsAuto(_options.LanguageHint) ? null : _options.LanguageHint;
            return new Utterance(text, lang, isFinal, InputSource.Voice);
        }

        /// <summary>Отбрасывает пустые и служебные результаты вроде "[BLANK_AUDIO]" или "(тишина)".</summary>
        private static string Clean(WhisperResult result)
        {
            if (result == null || result.Result == null) return null;
            string t = result.Result.Trim();
            if (t.Length == 0) return null;

            char first = t[0];
            char last = t[t.Length - 1];
            if ((first == '[' && last == ']') || (first == '(' && last == ')') || (first == '*' && last == '*'))
                return null;
            return t;
        }

        private static bool IsAuto(string hint)
        {
            return string.IsNullOrEmpty(hint) || string.Equals(hint, "auto", StringComparison.OrdinalIgnoreCase);
        }

        // Поле useGpu в WhisperManager приватное; без рефлексии его не задать из кода.
        // Если сборка с обрезкой кода (IL2CPP stripping) поле уберёт, останется CPU.
        private static void TrySetPrivateBool(object target, string fieldName, bool value)
        {
            try
            {
                FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(bool)) field.SetValue(target, value);
                else Debug.LogWarning("[WhisperRecognizer] Поле " + fieldName + " не найдено, используется значение по умолчанию.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WhisperRecognizer] " + e.Message);
            }
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
