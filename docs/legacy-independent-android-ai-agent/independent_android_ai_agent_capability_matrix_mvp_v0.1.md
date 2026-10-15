# Independent Android AI Agent — Capability Matrix MVP v0.1

> **Примечание (D-006, 2026-10-12).** Эта таблица — единственный источник базового уровня риска capability (`docs/architecture/GLOSSARY.md`, раздел 1).

**Статус:** архитектурный baseline, pre-code  
**Версия:** 0.1  
**Дата:** 2026-10-07

## 1. Назначение

Capability Matrix MVP определяет первый набор базовых возможностей Independent Android AI Agent.

Матрица не является перечнем Android API. Она описывает пользовательские возможности (WHAT), для которых позднее могут существовать разные стратегии выполнения (HOW) и разные провайдеры (WHO/WHERE).

Основной принцип:

> Capability = WHAT  
> Strategy = HOW  
> Provider = WHO/WHERE

Агент не должен быть жёстко связан с одним способом выполнения.

---

## 2. Критерии отбора MVP capabilities

В MVP включаются возможности, которые:

1. покрывают основные типы взаимодействия с Android;
2. позволяют проверить Goal → Plan → Safety → Execute → Verify;
3. демонстрируют несколько стратегий выполнения;
4. позволяют тестировать разрешения и пользовательское подтверждение;
5. имеют понятный результат, который можно независимо проверить;
6. дают основу для дальнейшего расширения без переписывания Agent Core.

MVP намеренно не пытается охватить сотни Android-возможностей.

---

## 3. Capability Matrix MVP

| ID | Capability | Категория | Основное назначение | Риск | Offline |
|---|---|---|---|---|---|
| CAP-001 | device.get_state | Device | Получить состояние устройства | R0 | Да |
| CAP-002 | app.open | Apps | Открыть приложение | R1 | Да* |
| CAP-003 | app.search | Apps | Найти установленное приложение | R0 | Да |
| CAP-004 | notification.read | Communication | Получить доступные уведомления | R2 | Да |
| CAP-005 | notification.create | Communication | Создать локальное уведомление | R1 | Да |
| CAP-006 | contact.search | Communication | Найти контакт | R1 | Да |
| CAP-007 | contact.create | Communication | Создать контакт | R2 | Да |
| CAP-008 | calendar.search_events | Productivity | Найти события | R1 | Да |
| CAP-009 | calendar.create_event | Productivity | Создать событие календаря | R2 | Да |
| CAP-010 | calendar.update_event | Productivity | Изменить событие | R2 | Да |
| CAP-011 | reminder.create | Productivity | Создать напоминание | R2 | Да* |
| CAP-012 | timer.create | Productivity | Установить таймер | R1 | Да |
| CAP-013 | alarm.create | Productivity | Установить будильник | R2 | Да |
| CAP-014 | text.compose | Communication | Подготовить текст | R0 | Да |
| CAP-015 | message.send | Communication | Отправить сообщение | R3 | Частично |
| CAP-016 | call.initiate | Communication | Инициировать звонок | R3 | Да* |
| CAP-017 | location.get | Device | Получить текущее местоположение | R2 | Да* |
| CAP-018 | web.search | Information | Найти информацию в интернете | R1 | Нет |
| CAP-019 | web.open | Information | Открыть веб-ресурс | R1 | Нет |
| CAP-020 | file.search | Files | Найти файл на устройстве | R1 | Да |
| CAP-021 | file.read | Files | Прочитать доступный файл | R1 | Да |
| CAP-022 | share.content | System | Передать контент через системный share | R2 | Да* |
| CAP-023 | settings.open | System | Открыть системный раздел настроек | R1 | Да |
| CAP-024 | ui.interact | Automation | Выполнить UI-действие как fallback | R3 | Да* |

\* Зависит от состояния устройства, приложения, разрешений или конкретного сценария.

---

## 4. Приоритет MVP

### P0 — первый вертикальный срез

- CAP-001 device.get_state
- CAP-008 calendar.search_events
- CAP-009 calendar.create_event
- CAP-010 calendar.update_event

### P1 — расширение базового агента

- CAP-002 app.open
- CAP-003 app.search
- CAP-005 notification.create
- CAP-006 contact.search
- CAP-014 text.compose
- CAP-018 web.search
- CAP-019 web.open
- CAP-020 file.search
- CAP-021 file.read
- CAP-023 settings.open

### P2 — более чувствительные действия

- CAP-004 notification.read
- CAP-007 contact.create
- CAP-011 reminder.create
- CAP-012 timer.create
- CAP-013 alarm.create
- CAP-015 message.send
- CAP-016 call.initiate
- CAP-017 location.get
- CAP-022 share.content

### P3 — универсальный fallback

- CAP-024 ui.interact

UI automation сознательно не является фундаментом архитектуры. Она должна использоваться, когда отсутствует или недоступен более надёжный структурированный интерфейс.

---

## 5. Стратегии выполнения

Каждая capability может иметь несколько стратегий.

### Типовой порядок предпочтения

1. Native Android API / системный интерфейс
2. Официальный интерфейс приложения / AppFunction
3. Структурированная интеграция / API
4. UI automation
5. Human-in-the-loop

Это не абсолютный порядок. Strategy Engine должен оценивать конкретный контекст.

Факторы оценки:

- доступность;
- разрешения;
- надёжность;
- риск;
- стоимость;
- скорость;
- возможность независимой проверки;
- приватность;
- необходимость интернета;
- известная история успешного выполнения.

---

## 6. Capability / Strategy / Provider

Пример:

```text
calendar.create_event
    │
    ├── Strategy: native provider
    │      └── Android Calendar Provider
    │
    ├── Strategy: official app interface
    │      └── Calendar application / AppFunction
    │
    ├── Strategy: UI automation
    │      └── Calendar UI
    │
    └── Strategy: human-assisted
           └── user completes final action
```

Capability Registry хранит capability-контракт.

Strategy Registry хранит способы выполнения.

Provider Registry описывает конкретные источники/механизмы.

Agent Core не должен содержать отдельную жёстко зашитую ветку для каждого приложения.

---

## 7. Формат capability-контракта

Минимальная модель:

```text
Capability
 ├─ id
 ├─ version
 ├─ name
 ├─ description
 ├─ category
 ├─ input_schema
 ├─ output_schema
 ├─ preconditions
 ├─ required_permissions
 ├─ risk_level
 ├─ strategies[]
 ├─ providers[]
 ├─ availability
 ├─ offline_capability
 ├─ verification_strategy
 └─ user_confirmation_policy
```

---

## 8. Availability

Capability должна уметь сообщать не только AVAILABLE/UNAVAILABLE.

Базовые состояния:

```text
AVAILABLE
UNAVAILABLE
BLOCKED
REQUIRES_PERMISSION
REQUIRES_USER
TEMPORARILY_UNAVAILABLE
UNKNOWN
```

Пример:

```text
calendar.create_event
    status = REQUIRES_PERMISSION
```

Агент не должен пытаться «обойти» такое состояние через несанкционированный механизм.

---

## 9. Risk model

Базовые уровни:

### R0 — informational
Чтение или вычисление без существенного воздействия.

### R1 — low impact
Незначительное действие с легко обратимым результатом.

### R2 — user-impacting
Действие изменяет состояние или создаёт обязательство для пользователя.

### R3 — sensitive / consequential
Коммуникация, звонки или действия, способные существенно повлиять на пользователя или третьих лиц.

### R4 — highly sensitive
Потенциально высокие последствия. Такие capabilities в MVP могут отсутствовать или быть явно заблокированы.

Risk не определяется только API.

Официальный API может выполнять действие высокого риска, а UI automation — низкого. Оценивается именно capability + контекст + конкретное действие.

---

## 10. Verification

Важный принцип:

> Успешное выполнение команды не равно успешному достижению цели.

Каждая значимая capability должна по возможности иметь verification strategy.

Примеры:

```text
calendar.create_event
    Execute → create
    Verify  → search event by stable identifiers / matching fields
```

```text
message.send
    Execute → send
    Verify  → delivery/sent state where reliably available
```

```text
file.read
    Execute → read
    Verify  → content returned and schema validated
```

Если независимая проверка невозможна, результат должен быть помечен соответствующим уровнем уверенности.

---

## 11. Permissions

Разрешения являются частью capability-контракта, а не скрытой деталью реализации.

Пример:

```text
contact.create
    required_permissions:
      - contacts write
```

При отсутствии разрешения:

```text
Capability
    ↓
Permission Check
    ↓
REQUIRES_PERMISSION
    ↓
Ask User
    ↓
Permission granted?
    ├── yes → continue
    └── no  → alternative / stop
```

Подтверждение пользователя не может само по себе выдать Android permission.

---

## 12. Experience integration

Experience Store может влиять на выбор стратегии, но не на безопасность.

Пример:

```text
Goal:
"Создай встречу завтра в 15:00"

Experience:
calendar.create_event
→ native provider
→ success rate high
→ recent
→ same environment

Strategy Engine:
→ prefer native provider
```

Но:

```text
Experience
    ↓
Recommendation
    ↓
Local Safety Policy
    ↓
Current Environment
    ↓
Execute
```

Опыт не может:

- обходить permission;
- обходить Safety Policy;
- автоматически подтверждать рискованное действие;
- превращаться в неконтролируемое self-modification.

---

## 13. Offline / Internet

`offline_capability` описывает возможность выполнения capability без сети.

Это не означает, что агент обязан выполнять все capabilities offline.

Примеры:

```text
calendar.create_event → usually offline-capable
file.read             → offline-capable
web.search             → internet required
message.send           → depends on provider/network
```

Local-first означает, что отсутствие интернета не должно уничтожать базовую автономность агента.

---

## 14. UI Automation

`ui.interact` является специальной capability/fallback-механикой.

Она нужна для ситуаций:

```text
No structured interface
        ↓
UI automation available
        ↓
Safety assessment
        ↓
Execute
        ↓
Strong verification required
```

UI automation имеет потенциальные недостатки:

- нестабильность UI;
- координатные ошибки;
- изменение интерфейса приложения;
- неожиданные состояния;
- prompt injection / untrusted content;
- более сложная верификация.

Поэтому она не должна становиться главным способом управления Android.

---

## 15. Security boundary

Никакая capability не должна напрямую получать право на выполнение только потому, что:

- её предложила LLM;
- её рекомендовала центральная база знаний;
- она ранее успешно выполнялась;
- её предлагает внешний сервис;
- пользователь сформулировал команду естественным языком.

Общий pipeline:

```text
User Goal
    ↓
Goal Understanding
    ↓
Strategy Selection
    ↓
Capability Selection
    ↓
Local Safety Policy
    ↓
Permission / Environment Check
    ↓
Execution
    ↓
Verification
```

---

## 16. Первый вертикальный capability

Рекомендуемый первый реальный vertical slice:

### `calendar.create_event`

Пример:

```text
User:
"Создай завтра встречу с Иваном в 15:00 на час."

        ↓

Goal Model

        ↓

Capability Discovery
calendar.create_event

        ↓

Strategy Search
native Calendar Provider
official interface
UI fallback

        ↓

Experience Lookup

        ↓

Safety / Permission Gate

        ↓

Execute

        ↓

Verify
event exists with expected time/title/participants

        ↓

Result
"Встреча создана и проверена."
```

Этот сценарий проверяет большую часть архитектуры одновременно:

- language interface;
- goal understanding;
- capability registry;
- strategy engine;
- permissions;
- safety;
- execution;
- verification;
- experience;
- re-planning;
- result transparency.

---

## 17. Что НЕ входит в Capability Matrix MVP

Пока сознательно не фиксируем как обязательные capabilities:

- финансовые операции;
- банковские действия;
- удаление большого объёма данных;
- изменение системной безопасности;
- медицинские решения;
- юридически значимые действия;
- управление критической инфраструктурой;
- неконтролируемое выполнение произвольного кода.

Они могут появиться позже через отдельные capability-контракты и более строгую safety-модель.

---

## 18. Архитектурный вывод

Capability Matrix MVP должна быть не просто каталогом функций.

Она является связующим слоем между:

```text
USER GOAL
    ↓
AGENT CORE
    ↓
CAPABILITY
    ↓
STRATEGY
    ↓
PROVIDER
    ↓
EXECUTION
    ↓
VERIFICATION
```

Это позволяет развивать агента без переписывания ядра при:

- появлении новых Android API;
- появлении AppFunctions;
- изменении приложений;
- появлении новых внешних сервисов;
- изменении локальных моделей;
- добавлении cloud-моделей;
- появлении новых способов автоматизации.

---

## 19. Следующий архитектурный шаг

После утверждения Capability Matrix MVP логично перейти к:

**Capability Contract v0.1**

То есть формально описать машинно-читаемый контракт одной capability:

```text
calendar.create_event
```

с JSON/schema-моделью:

- input;
- output;
- preconditions;
- permissions;
- risk;
- strategies;
- providers;
- availability;
- verification;
- confirmation policy.

После этого можно проектировать первый vertical slice, не начиная пока массовую реализацию всех capabilities.

---

## 20. Статус

**Capability Matrix MVP v0.1 — сформирована.**

**Код не изменялся.**

**Следующий шаг требует архитектурного подтверждения пользователя.**
