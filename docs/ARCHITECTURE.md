# Архитектура Barabashka

Единый технический документ. При расхождении с перепиской прав этот файл.

## Сборки (asmdef) и зависимости
```
Contracts    -> (ничего; без ссылок на Unity)   интерфейсы и типы: IBrain, IAgentTool, ISpeechRecognizer, ILocalLlm ...
Core         -> Contracts                        мозг, роутер команд, инструменты, настройки, интерфейс
Voice.*      -> Contracts                        Sherpa (распознавание), WindowsTts / AndroidTts (озвучка)
App          -> Contracts                        MainThreadDispatcher, VoiceInputChannel
Composition  -> все выше                         VoiceBootstrap: единственное место, где блоки соединяются
LocalLlm     -> Contracts                        модель на телефоне (LLMUnity), только при наличии пакета ai.undream.llm
Notifications-> Contracts                        уведомления Android (пакет com.unity.mobile.notifications)
```
Обратные зависимости и связи между соседями запрещены; обмен только через `Contracts`.

## Путь фразы
1. Голос: микрофон -> Sherpa-ONNX (GigaAM v2, офлайн) -> текст. Пока агент говорит, распознавание на паузе.
2. `IsolatedBrain` (правила): точные разборы фраз. Порядок важен: контакты (звонки/сообщения) -> будильник -> напоминания -> погода/заметки -> задачи -> телефонные действия -> общие ответы.
3. `HybridBrain`: если правила не поняли и включена модель, фраза уходит в цепочку LLM (Ollama -> OpenRouter/Groq -> модель на телефоне). Модель отвечает текстом или вызывает инструмент; `ToolMatchesRequest` отсекает выдуманные вызовы.
4. Мозг возвращает `COMMAND:...` (старые команды) или `TOOL|имя|аргументы`. `AgentCommandRouter` выполняет и показывает ответ; озвучка по настройке.
5. Непонятые правилами фразы пишутся в журнал (`/непонятое`) - из него растёт набор правил.

## Инструменты
Новый инструмент = класс `IAgentTool` (имя, описание для модели, `ExecuteAsync`) + регистрация в `AgentToolRegistry.CreateDefault` + правило в одном из парсеров + запись в `docs/COMMANDS.md` + слова в таблице `ToolWords` (`HybridBrain`).
Файлы: `ReminderTools`, `NoteTools`, `WeatherTools`, `PhoneTools` (телефонные действия), `CommTools` (звонок, сообщение).

## Действия вне приложения
Настройка `AppSettings.Actions`: `Manual` (открыть готовый экран), `Confirm` (спросить «да/нет» через `ActionGate`), `Auto` (сделать сразу). Звонок и SMS могут выполняться полностью сами (разрешения CALL_PHONE, SEND_SMS, READ_CONTACTS запрашиваются при первом использовании). Telegram/WhatsApp: Android не позволяет отправить за человека, открывается чат с готовым текстом.

## Секреты и подключения
`SecretStore` хранит ключи и токены только зашифрованными: на Android ключом из Android Keystore (`Assets/Plugins/Android/SecretVault.java`), на ПК - AES с ключом машины (базовая защита). `LlmProfiles` переносит ключи моделей туда (старые открытые ключи переносятся при первом чтении). `IConnector` и `ConnectorRegistry` описывают внешние службы; возможность может требовать коннектор (`CapabilityInfo.Connector`), тогда без подключения её статус - `NeedsSetup`.

## Telegram
`TelegramClient` работает с TDLib (`TdJson`, нативная `libtdjson.so`, собирается workflow `.github/workflows/build-tdlib-android.yml`): поток приёма, запросы с `@extra`, состояния входа, чаты, чтение истории, отправка с проверкой доставки. `TelegramConnector` показывает состояние в `/подключения`; `telegram_read` требует этого коннектора. Подробнее: `docs/TELEGRAM.md`.

## Платформы
Android-вызовы идут через `AndroidJavaObject` под `#if UNITY_ANDROID && !UNITY_EDITOR`, на ПК действия возвращают понятное сообщение. Видимость приложений и службы - `Assets/Plugins/Android/BarabashkaTtsQueries.androidlib/AndroidManifest.xml` (запросы `<queries>` и разрешения).

## Данные и настройки
`PlayerPrefs`: настройки приложения (`app.*`), профили LLM, город, журнал непонятого. Задачи, заметки, напоминания хранятся локально в `persistentDataPath`. Модель для телефона: `persistentDataPath/local-llm/model.gguf`.

## Модели
Не хранятся в Git. Распознавание речи: Sherpa-ONNX GigaAM v2 (русский) в `Assets/StreamingAssets/sherpa-onnx/models/` (папка в `.gitignore`). Языковая модель на телефоне (по желанию): Qwen2.5 1.5B Q4, скачивается командой `/llm телефон`.

## Правила разработки
- Новые файлы создаются инструментами записи, не командами терминала (PowerShell портит `$` и `\`).
- Файлы переносятся между папками внутри Unity, чтобы не терять `.meta`.
- Правила (собственный набор команд) - ядро продукта; сетевые модели необязательны и включаются в настройках.
