// Только Android-устройство (не редактор). В остальных сборках файл пуст,
// а вместо него подключается другая реализация ITextToSpeech.
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Voice.AndroidTts
{
    /// <summary>
    /// Озвучка системным синтезатором Android (android.speech.tts.TextToSpeech).
    /// Язык фразы выбирается через setLanguage; если русского голоса на устройстве нет, фраза не озвучивается
    /// (чтобы не читать русский текст английским голосом) и в журнал пишется причина.
    /// Окончание речи определяется опросом isSpeaking: так не нужен Java-класс слушателя.
    /// Вызывать из главного потока Unity.
    /// </summary>
    public sealed class AndroidSystemTts : ITextToSpeech
    {
        private sealed class InitListener : AndroidJavaProxy
        {
            private readonly Action<int> _callback;

            public InitListener(Action<int> callback)
                : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                _callback = callback;
            }

            // Вызывается Android из своего потока.
            public void onInit(int status)
            {
                _callback(status);
            }
        }

        private readonly TaskCompletionSource<bool> _ready =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private AndroidJavaObject _tts;
        private int _counter;
        private bool _disposed;

        public AndroidSystemTts()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new InitListener(OnInit));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tts] Не удалось создать TextToSpeech: " + e.Message);
                _ready.TrySetResult(false);
            }
        }

        public string Id { get { return "android-tts"; } }

        public event Action OnStarted;
        public event Action OnFinished;

        private void OnInit(int status)
        {
            // TextToSpeech.SUCCESS = 0
            if (status != 0)
                Debug.LogWarning("[Tts] Android TextToSpeech не запустился (код " + status + "). Проверьте, что на устройстве установлен синтезатор речи.");
            _ready.TrySetResult(status == 0);
        }

        public bool SupportsLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode) || languageCode == "auto") return true;
            if (_tts == null || !_ready.Task.IsCompleted || !_ready.Task.Result) return true;

            try
            {
                using (var locale = new AndroidJavaObject("java.util.Locale", languageCode.ToLowerInvariant()))
                    return _tts.Call<int>("isLanguageAvailable", locale) >= 0;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public async Task SpeakAsync(string text, string languageCode, CancellationToken ct)
        {
            if (_disposed) throw new ObjectDisposedException("AndroidSystemTts");
            if (string.IsNullOrWhiteSpace(text)) return;

            bool ready = await _ready.Task;
            if (!ready || _tts == null) return;
            ct.ThrowIfCancellationRequested();

            string lang = (string.IsNullOrEmpty(languageCode) || languageCode == "auto")
                ? string.Empty
                : languageCode.ToLowerInvariant();

            try
            {
                if (lang.Length > 0)
                {
                    using (var locale = new AndroidJavaObject("java.util.Locale", lang))
                    {
                        // LANG_MISSING_DATA = -1, LANG_NOT_SUPPORTED = -2
                        int available = _tts.Call<int>("setLanguage", locale);
                        if (available < 0)
                        {
                            Debug.LogWarning("[Tts] Голос для языка '" + lang + "' недоступен на устройстве (код " + available +
                                "). Установите русский голос в настройках Android: Язык и ввод - Синтез речи.");
                            return;
                        }
                    }
                }

                // Устаревшая, но доступная на всех версиях сигнатура speak(String, int, HashMap): её надёжно находит JNI.
                int result;
                using (var parameters = new AndroidJavaObject("java.util.HashMap"))
                    result = _tts.Call<int>("speak", text, 0 /* QUEUE_FLUSH */, parameters);

                if (result != 0)
                {
                    Debug.LogWarning("[Tts] speak вернул ошибку (код " + result + ").");
                    return;
                }

                RaiseStarted();

                // Даём движку начать, затем ждём конца речи.
                await Task.Delay(200, ct);
                while (_tts != null && !_disposed && _tts.Call<bool>("isSpeaking"))
                    await Task.Delay(100, ct);
            }
            catch (OperationCanceledException)
            {
                StopInternal();
                throw;
            }
            finally
            {
                RaiseFinished();
            }
        }

        public void Stop()
        {
            StopInternal();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_tts != null)
                {
                    _tts.Call<int>("stop");
                    _tts.Call("shutdown");
                    _tts.Dispose();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tts] Dispose: " + e.Message);
            }
            _tts = null;
        }

        private void StopInternal()
        {
            try
            {
                if (_tts != null && !_disposed) _tts.Call<int>("stop");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tts] Stop: " + e.Message);
            }
        }

        private void RaiseStarted()
        {
            Action handler = OnStarted;
            if (handler != null) handler();
        }

        private void RaiseFinished()
        {
            Action handler = OnFinished;
            if (handler != null) handler();
        }
    }
}
#endif
