# Карта кода (чтобы не исследовать проект вслепую)

Всё в `Assets/Scripts/`. Сборки (`.asmdef`) по папкам. Контракты между блоками лежат в `Contracts/`.

## Интерфейс (`Core/`, класс `AgentUIManager`, разбит на файлы)
- `AgentUIManager.cs` - каркас экрана, кнопки, ввод, голосовая группа.
- `AgentUIManager.Chat.cs` - чат, кнопки разделов, фильтры, смахивание, корзина, хранение истории.
- `AgentUIManager.Settings.cs` - окно настроек (будет заменено режимом настроек в чате).
- `AgentUIManager.Attach.cs` - меню кнопки «＋».
- `AgentUIManager.SettingsMode.cs` - режим настроек в чате.
- `ChatKinds.cs` (разделы, цвета), `ChatIcons.cs` (значки кодом), `ChatStore.cs` (история), `ChatCommands.cs` (команды чата), `ChatThumbnails.cs`.

## Ядро и команды (`Core/`)
- Разбор фраз правилами: `AgentCommandRouter.cs` (вход), `AgentPhoneParser.cs`, `AgentUtilityParser.cs` (погода, заметки), `AgentReminderParser.cs`, `AgentCalculator.cs`, `AgentDates.cs`, `MarketplaceParser.cs`, `MediaParser.cs`.
- Инструменты (`IAgentTool`): `AgentToolRegistry.cs`, `AgentToolset.cs`, `PhoneTools.cs`, `CommTools.cs`, `NoteTools.cs`, `WeatherTools.cs`, `ReminderTools.cs`, `MarketplaceTools.cs`, `MarketplaceOpenTool.cs`.
- Действия на телефоне: `PhoneActions.cs` (приложения, сайты, поиск), `PhoneComms.cs` (звонки, SMS).
- Напоминания: `AgentReminders.cs` (данные), `AgentReminderService.cs` (проверка при открытом приложении); системные уведомления: `Notifications/AndroidNotifications/`.
- Модели: `HybridBrain.cs`, `LlmClient.cs`, `LlmProfile.cs`, `LlmSettings.cs`, `LocalModel.cs`.
- Политика и возможности: `ActionPolicy.cs`, `ActionGate.cs`, `CapabilityRegistry.cs`, `AppSettings.cs`.
- Настройки: `Core/Settings/*Page.cs` (страницы), реестр в `Contracts/Settings.cs`, голосовые команды в `Core/Settings/SettingsVoice.cs`.
- Старт: `Core/AppBootstrap.cs`, `Core/AgentManager.cs`.
- Такси: `Core/Taxi/` (`TaxiParser` разбор фраз, `TaxiTools` инструменты, `AgentGeocoder` адрес -> координаты, `TaxiSettings`), операторы в `TaxiProviders/`, страница настроек `Core/Settings/TaxiPage.cs`, контракт `Contracts/Taxi.cs`.

## Остальные блоки
- `Contracts/` - интерфейсы между блоками (без Unity-зависимостей).
- `Media/` - просмотр фото и видео, выбор из галереи.
- `Voice/Sherpa/` - распознавание речи; `Voice/AndroidTts/`, `Voice/WindowsTts/` - озвучка.
- `Composition/` - сборка блоков при старте (голос, медиа).
- `MarketplaceProviders/` - Ozon, Wildberries.
- `LocalLlm/` - модель на телефоне (пакет LLMUnity, необязательно).
- Тесты: `Assets/Tests/EditMode/`.

## Документы (читай только по необходимости)
`docs/ARCHITECTURE.md` (устройство, сеть), `docs/COMMANDS.md` (фразы), `docs/MEDIA.md`, `docs/MESSENGERS.md`, `docs/ROADMAP.md`, `docs/STATE.md` (состояние, 30 строк), `docs/TESTING.md` (чек-лист для тестеров).
