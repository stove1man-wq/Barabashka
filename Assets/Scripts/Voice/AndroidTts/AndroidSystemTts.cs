#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Voice.AndroidTts
{
    public sealed class AndroidSystemTts : ITextToSpeech
    {
        private AndroidJavaObject _tts;
        private TaskCompletionSource<bool> _initialized;
        private TaskCompletionSource<bool> _speech;
        private bool _disposed;
        private readonly object _gate = new object();

        public string Id => "android-system-tts";
        public event Action OnStarted;
        public event Action OnFinished;

        public AndroidSystemTts()
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                _initialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new InitListener(this));
                _tts.Call("setOnUtteranceProgressListener", new ProgressListener(this));
            }
        }

        public bool SupportsLanguage(string languageCode) => true;

        public async Task SpeakAsync(string text, string languageCode, CancellationToken ct)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AndroidSystemTts));
            if (string.IsNullOrWhiteSpace(text)) return;
            await _initialized.Task;
            ct.ThrowIfCancellationRequested();

            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                StopCurrentLocked();
                _speech = completion;
                string lang = string.IsNullOrWhiteSpace(languageCode) || languageCode == "auto" ? "ru-RU" : languageCode;
                using (var localeClass = new AndroidJavaClass("java.util.Locale"))
                using (var locale = localeClass.CallStatic<AndroidJavaObject>("forLanguageTag", lang))
                    _tts.Call<int>("setLanguage", locale);

                using (var bundle = new AndroidJavaObject("android.os.Bundle"))
                    _tts.Call<int>("speak", text, 0, bundle, "barabashka");
            }

            using (ct.Register(Stop))
                await completion.Task;
        }

        public void Stop()
        {
            lock (_gate) StopCurrentLocked();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            if (_tts != null)
            {
                _tts.Call("shutdown");
                _tts.Dispose();
                _tts = null;
            }
        }

        private void StopCurrentLocked()
        {
            if (_speech != null)
            {
                _speech.TrySetCanceled();
                _speech = null;
            }
            if (_tts != null) _tts.Call<int>("stop");
        }

        private void CompleteSpeech(bool cancelled)
        {
            TaskCompletionSource<bool> completion;
            lock (_gate)
            {
                completion = _speech;
                _speech = null;
            }
            if (completion == null) return;
            if (cancelled) completion.TrySetCanceled();
            else completion.TrySetResult(true);
            Action handler = OnFinished;
            if (handler != null) handler();
        }

        private sealed class ProgressListener : AndroidJavaProxy
        {
            private readonly AndroidSystemTts _owner;
            public ProgressListener(AndroidSystemTts owner) : base("android.speech.tts.UtteranceProgressListener") => _owner = owner;
            public void onStart(string utteranceId)
            {
                Action handler = _owner.OnStarted;
                if (handler != null) handler();
            }
            public void onDone(string utteranceId) => _owner.CompleteSpeech(false);
            public void onError(string utteranceId) => _owner.CompleteSpeech(false);
        }

        private sealed class InitListener : AndroidJavaProxy
        {
            private readonly AndroidSystemTts _owner;
            public InitListener(AndroidSystemTts owner) : base("android.speech.tts.TextToSpeech$OnInitListener") => _owner = owner;
            public void onInit(int status)
            {
                if (status == 0) _owner._initialized.TrySetResult(true);
                else _owner._initialized.TrySetException(new InvalidOperationException("Android TextToSpeech initialization failed: " + status));
            }
        }
    }
}
#endif
