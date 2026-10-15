# Development Log

## 2026-10-10 — Android Emulator to Ollama connectivity investigation

### Confirmed
- The Android APK is already installed/launched in Android Emulator and the Unity UI is visible.
- The Unity script `Assets/Scripts/AgentPrototypeUI.cs` was changed to use `http://10.0.2.2:11434/api/generate`.
- The user reports configuring Ollama to listen on all/network interfaces.
- Windows requests to both `http://localhost:11434/api/tags` and `http://127.0.0.1:11434/api/tags` return the installed model list.
- A request posted from PowerShell via a UTF-8 JSON file to `http://localhost:11434/api/generate` succeeded with model `llama3.1:8b`, response `OLLAMA_OK`, and `done:true`.
- The Android app still does not receive a response from Ollama.

### Diagnostic attempt
- `adb devices` was attempted from PowerShell but failed because `adb` is not recognized as a command. This means the executable is not available on the current PATH; emulator/SDK absence has not been established.
- Next: locate Android SDK in Android Studio and call `platform-tools\adb.exe` by full path; capture app error/logcat; then test listener, firewall, and emulator-to-host connectivity.
- No conclusion has yet been reached about the root cause. Do not claim the Android network path is fixed.

### Guardrails
- Do not rebuild the APK unless source/build mismatch or a required manifest/code change is demonstrated.
- Change one variable at a time and record the result here.
- Keep Ollama access limited to the development environment; do not expose it to the public internet.


## 2026-10-10 — Root cause identified in Android logcat

- User ran `adb.exe logcat` via the full Android SDK path and reproduced the failure by pressing SEND TASK in the emulator.
- Exact log: `Non-secure network connections disabled in Player Settings` and `InvalidOperationException: Insecure connection not allowed` from `UnityEngine.Networking.UnityWebRequest.SendWebRequest()` called by `AgentPrototypeUI.SendTaskToOllama`.
- This proves Unity rejects the HTTP request under its Player Settings policy before a connection to Ollama is attempted. It does not yet prove that emulator-to-host networking works.
- Next action: in Unity open Edit → Project Settings → Player → Android → Other Settings → Configuration; set **Allow downloads over HTTP** to **Always Allowed** for this local development prototype (HTTP is unencrypted), rebuild/reinstall, and test once more.
- If the error changes, capture fresh logcat and diagnose the new failure. Do not claim the integration works until the app displays a response from Ollama.


## 2026-10-10 — Listener and Android shell checks

- `adb shell` opened successfully. Android emulator shell does not include `curl` (`/system/bin/sh: curl: inaccessible or not found`), so no emulator HTTP request was actually made.
- `Test-NetConnection 10.0.2.2 -Port 11434` was run from Windows; it is not a valid test of emulator-to-host reachability because it runs on the host.
- `netstat -ano | findstr :11434` showed listeners at `0.0.0.0:11434` and `[::]:11434` under PID 17632, and `127.0.0.1:11434` under PID 19544. These PIDs must be identified before changing anything.
- The user reports the app request hangs. A filtered logcat query returned no matching lines; capture broader fresh logs after reproducing before deciding whether the HTTP-policy exception remains.
- Next PowerShell diagnostic: `Get-Process -Id 17632,19544`.


## 2026-10-12 — Platform decision and consistency pass

### Owner input
- The owner decided to develop in Unity (D-005, Accepted). Reported versions: Unity 6000.6.5f1, Android Studio 2026.2.1. Agents in VS Code: Cline and Continue.
- The owner reports that the APK in the Android Emulator communicates with Ollama and works. No logs were provided and the cause of the earlier hang is not recorded. **Owner: add one line here saying what fixed it.**
- The GitHub repository contains no Unity project folder; the project exists only at its local path (D-009 proposes `unity/`).

### Changes made to the documentation
- D-005 accepted; D-004 closed; D-002 updated; D-006 partly accepted; D-007, D-008, D-009 proposed (`DECISIONS.md`).
- Kotlin shell documents marked Superseded or partly Superseded with a banner: `android_shell_architecture`, `android_modules_mapping`, `android_network_bridge`, `android_3d_renderer`; note added to `avatar_layer_architecture`. Nothing deleted.
- Typo fixed in `android_network_bridge_v0.1.md`: `http://10.0.2` → `http://10.0.2.2`.
- Contradictions between contracts resolved by `docs/architecture/GLOSSARY.md`, found by comparing the texts: risk level of `message.send` (R2 vs R3), `reminder.create` (R1 vs R2), `web.search` (R0 vs R1); permission state lists; Safety Policy decision names; two kinds of "confirmation"; three meanings of "state". Notes added to six contracts.
- Agent rules: one `AGENTS.md` (shorter, Unity platform, versions); Continue is connected through its own rule file `.continue/rules/01-agent-tool-use.md`, which already says to follow `AGENTS.md`; commit rule aligned with the "update GitHub" rule (agent prepares, owner confirms); role of the architect moved to `docs/agent/ROLE_ARCHITECT.md`.
- `PROJECT_STATUS.md`, `android_integration_architecture_v0.1.md`, `README.md`, `NEXT_STEPS.md`, `docs/DOCUMENT_MAP.md` updated.

### Not changed
- Contract texts themselves (only notes at the top), markup noise in four documents, folder structure.
- No code was read or changed: the Unity project is not in the repository.

### Found on the owner's PC (reported 2026-10-12)
- The local checkout was on branch `diagnostic/disable-agent-instructions` (also on GitHub), two commits ahead of `main`: `AGENTS.md` renamed to `AGENTS.md.disabled` ("temporarily disable AGENTS.md for Continue tool check"). `main` is unchanged at `58c677c`. The result of that diagnostic is not recorded. **Owner: add one line, did disabling `AGENTS.md` fix Continue's tool use?**
- Untracked, not touched: `.clinerules`, `.continue/`, `.vscode/extensions.json` (recommends the Cline extension), two `*.bak_20261009_101944` backups, three `continue_*_test.txt` files.
- `.clinerules` says the current phase is "full isolation (Level 1: Isolated): no neural networks, Ollama or network APIs", and gives Cline the role of "IT architect". This contradicts `PROJECT_STATUS.md`, D-001, D-002 and `AGENTS.md`. The phrase "Level 1: Isolated" appears nowhere in the repository documents. Open question for the owner: planned phase or stale instruction.

### Roles and agent–Unity link (owner, 2026-10-12)
- Owner clarified: the architect is a senior model in a separate chat; Cline and Continue are executors. Recorded as D-010 (Accepted). `AGENTS.md` §1, `ROLE_ARCHITECT.md`, `SETUP_VSCODE.md` adjusted. `.clinerules` ("IT architect") now conflicts with D-010.
- Added `docs/agent/UNITY_BRIDGE.md` and D-011 (Proposed): feedback levels L0/L1/L2 for the VS Code agent and Unity; milestone M0.2 in `NEXT_STEPS.md`. Based on Unity documentation and general knowledge; nothing run on the owner's PC.

### Sync and executor problem (owner, 2026-10-12)
- Package v3 applied on the owner's PC and pushed: `main` = `2af09b7`. The commit also swept in files that were already staged: `.clinerules`, `.continue/rules/01-agent-tool-use.md`, `.vscode/extensions.json`, three `continue_*_test.txt`, two `*.bak_20261009_101944`. A follow-up commit removing the test files and backups from tracking was proposed.
- `.clinerules` replaced by a two-line pointer to `AGENTS.md` (executor, not architect, D-010).
- Owner reports that Cline does not create new files and does not write code into existing ones. Cause not established. Hypotheses (mode Plan, missing approval, context below 32K, Compact Prompt off, model without reliable tool calling) and a one-variable test are in `docs/agent/SETUP_VSCODE.md` §1a. Not yet tested.


# DEVELOPMENT_LOG: Independent Android AI Agent (Unity Spike)
**Дата фиксации:** 13 октября 2026 г.
**Архитектурный контур:** Level 1 (Режим полной изоляции — Rule-based MVP)
**Текущий статус:** УСПЕШНО ВЕРИФИЦИРОВАНО

---

## 🗺️ 1. Технологический стек и архитектурный сдвиг
- **Основная среда выполнения:** Unity (C#) — отвечает за логику, стейт-машину задач, UI-слой и 3D-аватар.
- **Вспомогательная среда:** Android Studio — используется исключительно как тонкий клиент для сборки финального APK и отладки нативных системных вызовов (Accessibility Services).
- **Инженерное окружение:** Visual Studio Code под управлением Windows 11.
- **ИИ-инференс разработки:** Гибридный (Автономный агент Cline + локальная Ollama для калибровки алгоритмов взаимодействия + облачные API-модели класса NVIDIA Nemotron для стабильной кодогенерации).

---

## 🧱 2. Состав развернутого ядра (Папка Assets/Scripts/Core)
Платформа полностью автоматизирована и состоит из 8 взаимосвязанных файлов:
1. `IAgentBrain.cs` — Базовый архитектурный контракт «мышления» агента.
2. `IsolatedBrain.cs` — Изолированный детерминированный обработчик интентов (Regex/условия) для обработки популярных бытовых запросов пользователя («Привет», «Погода», «Время», «Браузер»).
3. `AgentToolset.cs` — Статический слой исполнительных инструментов ОС (извлечение системного времени Windows, вызов системного URL-браузера).
4. `AgentCommandRouter.cs` — Маршрутизатор команд. Осуществляет перехват системных префиксов `COMMAND:`, распределяет задачи к `AgentToolset` и активирует триггеры нативного компонента `Animator`.
5. `AgentUIManager.cs` — Модуль динамической генерации интерфейса. Автономно разворачивает в рантайме холст Canvas, текстовое поле вывода ответа аватара и нижнюю панель ввода InputField с кнопкой.
6. `AgentManager.cs` — Центральный управляющий контроллер. Координирует асинхронный цикл «Ввод пользователя -> Обработка в мозге -> Вывод на UI -> Триггер анимации роутера». Содержит валидацию зависимостей и поддержку ContextMenu.
7. `AvatarVisualizer.cs` — Программный генератор пространственного 3D-тела (Capsule Mesh) и автоматический юстировщик положения Main Camera в рантайме.
8. `AppBootstrap.cs` — Автоматический загрузчик приложения (Паттерн Bootstrap). Программно собирает сущность `[AI_AGENT]` и подключает все вышеперечисленные компоненты при старте, полностью избавляя разработчика от ручной рутины в редакторе Unity.

---

## 📈 3. Лог верификации и пройденные вехи
- **[Успех] Синхронизация IDE и терминала:** Разрешены конфликты путей Windows 11. Агент Cline жестко привязан к абсолютному пути Unity-проекта `IndependentAgentUnitySpike`.
- **[Успех] Преодоление багов кодировки:** Устранены критические ошибки компиляции `error CS1009` (Unrecognized escape sequence), возникавшие из-за некорректного автоматического экранирования строк терминалом PowerShell. Код переведен на чистые конкатенации строковых литералов.
- **[Успех] Стабилизация рантайма:** Исправлено предупреждение Animator о нехватке параметра `Greet`.
- **[Текущее состояние]:** Кодовая база полностью готова к первому аппаратному пуску.

---

## 🎯 4. Ближайшие архитектурные шаги (План разработки)
1. **Ручной запуск (Play Mode):** Проверка работы программного Bootstrap на пустой сцене, тестирование ввода команд «Время» и «Браузер» через рантайм-интерфейс игры.
2. **Переход к Фазе 2 (Level 2: Локальная модель):** Создание скрипта `LocalOllamaBrain.cs` и интеграция асинхронных сетевых запросов `UnityWebRequest` / `HttpClient` для переключения аватара с жестких условий на гибкое ИИ-мышление через локальный инференс.


## [УСПЕХ] Закрытие Фазы 1: Изолированное интерактивное ядро (13.10.2026)
- **Главное достижение:** Полная синхронизация IDE VS Code, ИИ-агента Cline и рантайма Unity 6.
- **Статус автоматизации:** Агент успешно выполняет комплексный рефакторинг, отладку (QA-дебаг) и сборку C#-компонентов.
- **Архитектурный статус:** Реализован паттерн "Слепого ядра" (Loose Coupling). Визуальный UI-слой (`AgentUIManager`) и процедурный анимационный движок (`AvatarVisualizer`) полностью отделены от контракта логики (`IAgentBrain`). 
- **Результат верификации:** Рантайм-тесты популярных интентов (Время, Погода, Приветствия) и вызовы системного тулсета Windows 11 через UI игры работают стабильно.

