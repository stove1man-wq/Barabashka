# TASKS для Cline (по одной, новый чат на каждую)

Общее: читай только указанные файлы; после правок проверь консоль Unity; затем git add/commit/push по AGENTS.md. Файлы создавай инструментами записи, не через терминал.

## Сделано
- T1 Contracts + App (v1). T2/T3 asmdef Core, адаптер IBrain, AgentManager.SendUtterance (v2).
- T6 WhisperRecognizer, VoiceInputChannel, VoiceBootstrap (v3).

## T4. Отключить старый голосовой мост (обязательно перед T6)
В `AgentVoiceBridge.cs` закомментировать вызов `StartBackgroundSTT();` в методе `SetState`. Критерий: в Play Mode нет процессов `powershell.exe`.

## T5. IsolatedBrain на данных
Вынести фразы из `IsolatedBrain.cs` в `Assets/StreamingAssets/lang/ru/intents.json`; читать через `ILanguagePackProvider`. Исправить совпадение «час» (срабатывает на «сейчас», «частота»): искать по границам слов. Добавить английские шаблоны.

## T7. Озвучка (ITextToSpeech)
Выбор движка (Piper / системный), пауза распознавания на время речи агента (StopListening на OnStarted, StartListening на OnFinished).
