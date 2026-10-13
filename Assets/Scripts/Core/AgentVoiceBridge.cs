using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Debug = UnityEngine.Debug;

public class AgentVoiceBridge : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AgentManager _agentManager;

    [Header("TTS Settings")]
    [SerializeField] private int _ttsRate = 0;
    [SerializeField] private int _ttsVolume = 100;
    [SerializeField] private string _ttsVoiceName = "";

    [Header("Events")]
    public UnityEvent<string> OnUserSpeechRecognized;
    public UnityEvent OnTtsStarted;
    public UnityEvent OnTtsFinished;

    private enum VoiceState { Idle, Speaking, Listening }
    private VoiceState _state = VoiceState.Idle;

    private CancellationTokenSource _ttsCts;
    private CancellationTokenSource _sttCts;
    private string _scriptPath;

    private void Awake()
    {
        if (_agentManager == null) _agentManager = FindObjectOfType<AgentManager>();
        _scriptPath = Path.Combine(Application.dataPath, "Scripts/Core/stt_listener.ps1");
    }

    private void Start()
    {
        Debug.Log("[VoiceBridge] Инициализация STT на архитектуре Unity 6 CLI Bridge.");
        SetState(VoiceState.Listening);
    }

    private void OnDestroy()
    {
        CancelOperations();
    }

    public void SpeakAndListen(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            SetState(VoiceState.Listening);
            return;
        }

        SetState(VoiceState.Speaking);
        _ = RunSpeechCycleAsync(text);
    }

    private async Task RunSpeechCycleAsync(string text)
    {
        StopSTTInternal();

        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        OnTtsStarted?.Invoke();

        await Task.Run(() => 
        {
            try
            {
                string safeText = text.Replace("'", "''").Replace("\"", "`\"");
                string psScript = "Add-Type -AssemblyName System.Speech; \$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer; \(synth.Rate = " + _ttsRate + "; \)synth.Volume = " + _ttsVolume + "; " + (string.IsNullOrEmpty(_ttsVoiceName) ? "" : "\(synth.SelectVoice('" + _ttsVoiceName + "');") + " \)synth.Speak('" + safeText + "')";

                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -Command \"" + psScript + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    process.Start();
                    process.WaitForExit();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[VoiceBridge] TTS Runtime Error: " + e.Message);
            }
        }, _ttsCts.Token);

        OnTtsFinished?.Invoke();
        SetState(VoiceState.Listening);
    }

    private void StartBackgroundSTT()
    {
        _sttCts?.Cancel();
        _sttCts = new CancellationTokenSource();
        
        _ = Task.Run(async () =>
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -File \"" + _scriptPath + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    process.Start();

                    string recognizedText = await process.StandardOutput.ReadToEndAsync();
                    process.WaitForExit();

                    recognizedText = recognizedText?.Trim();

                    if (!string.IsNullOrEmpty(recognizedText) && !_sttCts.Token.IsCancellationRequested)
                    {
                        _agentManager.SendUserMessage(recognizedText);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[VoiceBridge] STT Async Error: " + e.Message);
            }
            finally
            {
                if (_state == VoiceState.Listening && !_sttCts.Token.IsCancellationRequested)
                {
                    StartBackgroundSTT();
                }
            }
        }, _sttCts.Token);
    }

    private void StopSTTInternal()
    {
        _sttCts?.Cancel();
        _sttCts = null;
    }

    private void CancelOperations()
    {
        _ttsCts?.Cancel();
        _sttCts?.Cancel();
    }

    private void SetState(VoiceState newState)
    {
        _state = newState;
        if (_state == VoiceState.Listening)
        {
            StartBackgroundSTT();
        }
        else
        {
            StopSTTInternal();
        }
    }
}
