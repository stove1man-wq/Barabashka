// Только Windows (редактор и сборка). На других платформах файл пуст,
// а вместо него подключается другая реализация ITextToSpeech.
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using Debug = UnityEngine.Debug;

namespace Barabashka.Voice.WindowsTts
{
    /// <summary>
    /// Озвучка системными голосами Windows (SAPI) через один постоянный процесс PowerShell.
    /// Текст передаётся в Base64, поэтому кавычки, $ и \ ничего не ломают.
    /// Голос выбирается по коду языка фразы; если для языка голоса нет, говорит голос по умолчанию.
    /// Отмена фразы = завершение процесса; следующий вызов запускает его заново (около секунды).
    /// События могут приходить из фонового потока.
    /// </summary>
    public sealed class WindowsSystemTts : ITextToSpeech
    {
        // Протокол: в stdin строки "SPEAK|язык|текст в Base64",
        // из stdout строки VOICES|ru,en  STARTED  DONE  ERROR|сообщение.
        // Скрипт намеренно без двойных кавычек.
        private const string Script = @"
Add-Type -AssemblyName System.Speech
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$langs = ($s.GetInstalledVoices() | Where-Object { $_.Enabled } | ForEach-Object { $_.VoiceInfo.Culture.TwoLetterISOLanguageName } | Select-Object -Unique) -join ','
[Console]::Out.WriteLine('VOICES|' + $langs)
[Console]::Out.Flush()
while (($line = [Console]::In.ReadLine()) -ne $null) {
  $p = $line.Split('|')
  if ($p[0] -ne 'SPEAK') { continue }
  try {
    $text = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p[2]))
    if ($p[1] -ne '') {
      $v = $s.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.TwoLetterISOLanguageName -eq $p[1] } | Select-Object -First 1
      if ($v) { $s.SelectVoice($v.VoiceInfo.Name) }
    }
    [Console]::Out.WriteLine('STARTED')
    [Console]::Out.Flush()
    $s.Speak($text)
  } catch {
    [Console]::Out.WriteLine('ERROR|' + ($_.Exception.Message -replace '\s+', ' '))
    [Console]::Out.Flush()
  }
  [Console]::Out.WriteLine('DONE')
  [Console]::Out.Flush()
}
";

        private readonly object _lock = new object();
        private Process _process;
        private TaskCompletionSource<bool> _current;
        private HashSet<string> _languages;
        private bool _disposed;

        public string Id { get { return "windows-sapi"; } }

        public event Action OnStarted;
        public event Action OnFinished;

        /// <summary>Заранее запускает процесс, чтобы первая фраза не ждала.</summary>
        public void WarmUp()
        {
            try { lock (_lock) { EnsureProcess(); } }
            catch (Exception e) { Debug.LogWarning("[Tts] WarmUp: " + e.Message); }
        }

        public bool SupportsLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode) || languageCode == "auto") return true;
            lock (_lock)
            {
                if (_languages == null) return true; // список голосов ещё не получен
                return _languages.Contains(languageCode.ToLowerInvariant());
            }
        }

        public Task SpeakAsync(string text, string languageCode, CancellationToken ct)
        {
            if (_disposed) throw new ObjectDisposedException("WindowsSystemTts");
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
            if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

            string lang = (string.IsNullOrEmpty(languageCode) || languageCode == "auto")
                ? ""
                : languageCode.ToLowerInvariant();
            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

            TaskCompletionSource<bool> previous = null;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_lock)
            {
                // Предыдущая фраза ещё идёт: обрываем её, иначе новая встанет в очередь.
                if (_current != null) previous = StopLocked();

                EnsureProcess();
                _current = tcs;
                _process.StandardInput.WriteLine("SPEAK|" + lang + "|" + payload);
                _process.StandardInput.Flush();
            }
            FinishAfterStop(previous);

            if (ct.CanBeCanceled)
            {
                CancellationTokenRegistration registration = ct.Register(() => StopIfCurrent(tcs));
                tcs.Task.ContinueWith(t => registration.Dispose(), TaskScheduler.Default);
            }
            return tcs.Task;
        }

        public void Stop()
        {
            TaskCompletionSource<bool> tcs;
            lock (_lock) { tcs = StopLocked(); }
            FinishAfterStop(tcs);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        private void StopIfCurrent(TaskCompletionSource<bool> tcs)
        {
            TaskCompletionSource<bool> stopped = null;
            lock (_lock)
            {
                if (_current == tcs) stopped = StopLocked();
            }
            FinishAfterStop(stopped);
        }

        // Вызывать под lock. Возвращает незавершённую фразу, чтобы завершить её вне lock.
        private TaskCompletionSource<bool> StopLocked()
        {
            Process p = _process;
            TaskCompletionSource<bool> tcs = _current;
            _process = null;
            _current = null;

            if (p != null)
            {
                try { if (!p.HasExited) p.Kill(); } catch (Exception) { }
                try { p.Dispose(); } catch (Exception) { }
            }
            return tcs;
        }

        private void FinishAfterStop(TaskCompletionSource<bool> tcs)
        {
            if (tcs == null) return;
            RaiseFinished();
            tcs.TrySetResult(true);
        }

        // Вызывать под lock.
        private void EnsureProcess()
        {
            if (_process != null && !_process.HasExited) return;

            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(Script));
            var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            p.EnableRaisingEvents = true;
            p.OutputDataReceived += (s, e) => HandleLine(p, e.Data);
            p.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning("[Tts] " + e.Data);
            };
            p.Exited += (s, e) => HandleExited(p);

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            _process = p;
        }

        private void HandleLine(Process source, string data)
        {
            if (data == null) return;
            string line = data.TrimStart('﻿').Trim();

            if (line.StartsWith("VOICES|", StringComparison.Ordinal))
            {
                var set = new HashSet<string>();
                foreach (string code in line.Substring(7).Split(','))
                {
                    if (code.Length > 0) set.Add(code.ToLowerInvariant());
                }
                lock (_lock) { if (source == _process) _languages = set; }
            }
            else if (line == "STARTED")
            {
                Action handler = OnStarted;
                if (handler != null) handler();
            }
            else if (line == "DONE")
            {
                TaskCompletionSource<bool> tcs = null;
                lock (_lock)
                {
                    if (source == _process) { tcs = _current; _current = null; }
                }
                if (tcs != null)
                {
                    RaiseFinished();
                    tcs.TrySetResult(true);
                }
            }
            else if (line.StartsWith("ERROR|", StringComparison.Ordinal))
            {
                Debug.LogWarning("[Tts] " + line.Substring(6));
            }
        }

        private void HandleExited(Process source)
        {
            TaskCompletionSource<bool> tcs = null;
            lock (_lock)
            {
                if (source != _process) return; // процесс остановлен нами через Stop
                _process = null;
                tcs = _current;
                _current = null;
            }
            Debug.LogWarning("[Tts] Процесс озвучки завершился неожиданно.");
            FinishAfterStop(tcs);
        }

        private void RaiseFinished()
        {
            Action handler = OnFinished;
            if (handler != null) handler();
        }
    }
}
#endif
