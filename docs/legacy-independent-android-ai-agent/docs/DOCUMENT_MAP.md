# Document Map

Обновлено 2026-10-12 после принятия D-005 (Unity) и сверки контрактов. Карта составлена по заголовкам, статусам и выборочному чтению; расхождения терминов проверены по текстам контрактов. Правила статусов: D-006 в `DECISIONS.md`.

Обозначения: **Fact** — описывает реальное состояние; **Target** — целевой дизайн, не реализован; **Note** — сырая заметка; **Asset** — изображение. Статусы: `Draft`, `Approved`, `Superseded`.

## Управление проектом и правила агентов

| Файл | Роль |
|---|---|
| `AGENTS.md` | Правила для агентов. Единственное место правил |
| `.continue/rules/01-agent-tool-use.md` | Правило Continue: надёжное использование инструментов, ссылается на `AGENTS.md` |
| `.clinerules` | Две строки: ссылка на `AGENTS.md` и напоминание, что Cline — исполнитель (D-010). Прежний текст («ИТ-архитектор», фаза «Level 1: Isolated», без Ollama) заменён 2026-10-12 |
| `README.md` | Навигация |
| `PROJECT_STATUS.md` | Fact, главный источник истины |
| `DECISIONS.md` | Решения; обязательны только Accepted |
| `NEXT_STEPS.md` | План этапов и критерии |
| `DEVELOPMENT_LOG.md` | Хронология |
| `docs/architecture/android_integration_architecture_v0.1.md` | Fact: реализованный прототип |
| `docs/architecture/GLOSSARY.md` | Единый словарь терминов контрактов (приоритетнее контрактов) |
| `docs/ARCHITECT_REVIEW.md` | Заключение архитектора от 2026-10-12 |
| `docs/agent/SETUP_VSCODE.md`, `docs/agent/ROLE_ARCHITECT.md` | Настройка Cline и Continue; роль архитектора (старшая модель в чате) и цикл задач |
| `docs/agent/UNITY_BRIDGE.md` | Связка агента VS Code с Unity: уровни L0–L2 (предложение, D-011) |
| `.gitignore` | Исключения для Unity-проекта в `unity/` |

## Видение и принципы (Target)

| Файл | Статус и заметки |
|---|---|
| `…future_android_ai_agent_project_brief.md` | Draft. Концепция, 2026-10-07 |
| `…architecture_decisions_v0.1.md` | Draft. 22 принципа. Раздел 9 («реализацию не начинать») устарел: прототип — spike |
| `777.txt` | Note. Выгрузка чата: MVP Architecture v0.1. Раздел 6 (Kotlin) заменён D-005; критерии MVP и этапы A–E использованы в `NEXT_STEPS.md` |

## Контракты ядра (Target, Draft)

| Файл | Тема |
|---|---|
| `…capability_contract_v0.1.md` | Интерфейс capability (политика подтверждения — объявление capability) |
| `…capability_matrix_v01.md` | Предварительная матрица осуществимости (широкая) |
| `…capability_matrix_mvp_v0.1.md` | 24 capabilities MVP; **единственный источник базового риска capability**; P0 = device.get_state, calendar.search/create/update |
| `…strategy_contract_v0.1.md` | Интерфейс стратегии |
| `…strategy_selection_scoring_v0.1.md` | Выбор и оценка стратегий (отложено до M5+) |
| `planning_reasoning_model_v0.1.md` | Планирование |
| `…task_state_replanning_contract_v0.1.md` | Состояние задач и перепланирование |
| `…execution_contract_v0.1.md` | Исполнение |
| `…execution_transaction_model_v0.1.md` | Состояния транзакции. Мусор разметки |
| `…verification_contract_v0.1.md` | Верификация |
| `…model_adapter_contract_v0.1.md` | Адаптер моделей (опора для D-007) |
| `…provider_tool_contract_v0.1.md` | Провайдеры и инструменты. Мусор разметки, ~3000 строк |
| `…memory_state_model_v0.1.md` | Память и состояние. Мусор разметки |
| `…data_privacy_contract_v0.1.md` | Данные и приватность |
| `…observability_audit_contract_v0.1.md` | Наблюдаемость и аудит |

## Безопасность и разрешения: владельцы тем

| Файл | Роль |
|---|---|
| `…safety_permission_contract_v0.1.md` (с префиксом, 31 раздел) | **Нормативный** документ по политике безопасности |
| `…permission_security_model_v0.1.md` | Концептуальная модель разрешений. Каноническое множество состояний и решений — в `GLOSSARY.md` |
| `safety_permission_contract_v0.1.md` (короткий, без префикса) | Обзор, не нормативный. Расхождения перечислены в баннере документа |

Расхождения найдены сверкой текстов и сняты правилом приоритета `GLOSSARY.md`, документы не объединялись: уровни риска примеров (`message.send`, `reminder.create`, `web.search`), состояния разрешений, решения Safety Policy (`DENY`, `CONFIRM`, `REQUIRE_CONFIRMATION` и `BLOCK`, `ALLOW_WITH_CONFIRMATION`), два слоя подтверждения, три смысла слова «состояние».

## Android-оболочка (D-005: платформа — Unity)

| Файл | Статус |
|---|---|
| `android_shell_architecture_v0.1.md` | Частично Superseded: разделы 2 и 5. Разделы 1, 3, 4 (системные роли, оверлей, сервис, раскладка UI, EMERGENCY STOP) — требования |
| `android_modules_mapping_v0.1.md` | Superseded. Принцип «ядро без зависимости от платформы» сохранён в D-005 |
| `android_network_bridge_v0.1.md` | Superseded. Опечатка адреса исправлена. В Unity HTTP разрешается в Player Settings |
| `android_3d_renderer_v0.1.md` | Superseded. Аватар реализуется в Unity |
| `avatar_layer_architecture_v0.1.md` | Действует. `AvatarRenderAdapter` реализуется в Unity |

Файлы не удалены, в начале каждого стоит баннер.

## Сырые заметки (Note)

| Файл | Тема |
|---|---|
| `Аватар.txt` | Рекомендация Unity URP + glTF. Согласуется с D-005 |
| `Память агента.txt` | Четыре слоя локальной памяти |
| `Сетевое взаимодействие.txt` | Сервер меток окружения |
| `Системный промт.txt` | Superseded: роль архитектора перенесена в `docs/agent/ROLE_ARCHITECT.md` без требования «отвергать стандартные решения» (противоречило `AGENTS.md`); архитектор — модель в чате, не агент VS Code |
| `принцип офлайн-режима.txt` | Честность в офлайне |

## Изображения (Asset)

| Файл | Содержимое |
|---|---|
| `mermaid-diagram.png` | Цикл: Goal Model → Planner → Strategy Engine → Safety → Execution → Capability → Verification → Update Experience |
| `ULdI…jpg` (длинное случайное имя) | 1080×1080, абстрактная синяя сфера; похоже на концепт-образ аватара |

## Предлагаемая структура (пока не применена)

```
/
├─ AGENTS.md  README.md  PROJECT_STATUS.md  DECISIONS.md
├─ DEVELOPMENT_LOG.md  NEXT_STEPS.md  .gitignore  .continue/
├─ unity/                (Unity-проект, D-009)
└─ docs/
   ├─ ARCHITECT_REVIEW.md  DOCUMENT_MAP.md
   ├─ agent/        (SETUP_VSCODE.md, ROLE_ARCHITECT.md, UNITY_BRIDGE.md)
   ├─ architecture/
   │   ├─ GLOSSARY.md  android_integration_architecture_v0.1.md
   │   ├─ overview/      (brief, principles, mvp_architecture)
   │   ├─ contracts/     (все *_contract, *_model; префикс independent_android_ai_agent_ убрать)
   │   └─ android-shell/ (пять документов оболочки)
   ├─ notes/        (бывшие .txt с латинскими именами)
   └─ assets/
```

Переносы выполнять через `git mv`, отдельным коммитом, с обновлением ссылок. Очистка разметки в четырёх помеченных документах — отдельная задача: убрать только остатки экранирования, без изменения текста, и показать diff.
