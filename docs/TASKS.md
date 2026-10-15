# TASKS для Cline (по одной, новый чат на каждую)

Общее: читай только указанные файлы; после правок проверь консоль Unity; затем git add/commit/push по AGENTS.md. Файлы создавай инструментами записи, не через терминал.

## Сделано
- T1-T3 Contracts, asmdef, адаптер IBrain, AgentManager.SendUtterance.
- T4 старый мост отключён. T6 Whisper: WhisperRecognizer, VoiceInputChannel, VoiceBootstrap.
- T7 (частично): WindowsSystemTts, SpeechCoordinator, пауза микрофона на время речи.

## T7б. Озвучка ответов агента  [ЖДЁТ АРХИТЕКТОРА]
Нужны AgentCommandRouter.cs и AgentUIManager.cs: в Core добавляется событие "агент сказал" (текст + язык), Composition подписывает на него SpeechCoordinator.Say. Включая ответы команд (время).

## T7в. Android TTS
Реализация ITextToSpeech через android.speech.tts (AndroidJavaObject) под #if UNITY_ANDROID.

## T5. IsolatedBrain на данных
Фразы из IsolatedBrain в StreamingAssets/lang/ru/intents.json; исправить совпадение «час»; английские шаблоны.

## Фаза 2. LocalOllamaBrain : IBrain
