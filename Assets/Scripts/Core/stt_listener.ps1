# PowerShell Script for STT using System.Speech
# Usage: powershell.exe -File "C:\Path\to\stt_listener.ps1"
Add-Type -AssemblyName System.Speech;

try {
    # 1. Initialize Speech Recognition Engine
    $recognizer = New-Object System.Speech.Recognition.SpeechRecognitionEngine;

    # 2. Set Input Device (use default mic)
    # Note: This assumes the current system default is desired.
    $recognizer.SetInputToDefaultAudioDevice();

    # 3. Configure language (Russian culture)
    # Using 'ru-RU' for explicit Russian culture
    $recognizer.SetLanguage("ru-RU");

    Write-Host "Waiting for speech. Please speak now..."

    # 4. Synchronous Recognition
    # This call blocks until speech is recognized or timeout occurs.
    $result = $recognizer.Recognize();

    # 5. Output the result text to stdout
    Write-Output $result.Text
}
catch {
    # Output error to stderr if necessary, but for simplicity, we just exit.
    Write-Error $_.Exception.Message
}
finally {
    # Ensure the recognizer is properly disposed if it was created
    if ($recognizer) {
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
    }
}