# Independent Android AI Agent — Capability Contract v0.1

> **Примечание (D-006, 2026-10-12).** Политика подтверждения здесь (`NEVER_REQUIRED` … `POLICY_BLOCKED`) — объявление capability. Вычисленный уровень подтверждения для вызова — см. `docs/architecture/GLOSSARY.md`, раздел 4.

**Статус:** архитектурный baseline, pre-code  
**Версия:** 0.1  
**Дата:** 2026-10-07  
**Связанный документ:** Capability Matrix MVP v0.1

---

## 1. Назначение

Capability Contract определяет формальный интерфейс между Agent Core и конкретной capability.

Контракт отвечает на вопрос:

> **Что агент может попросить выполнить и что он вправе ожидать в ответ?**

Контракт не определяет конкретный Android API и не заставляет Agent Core знать детали реализации.

Основное разделение:

```text
Capability Contract
        ↓
WHAT

Strategy
        ↓
HOW

Provider
        ↓
WHO / WHERE

Execution Adapter
        ↓
CONCRETE IMPLEMENTATION
```

---

# 2. Основные принципы

## 2.1 Capability не является API

Например:

```text
calendar.create_event
```

не означает:

```text
использовать конкретный Android API
```

Одна capability может иметь:

```text
Native Calendar Provider
AppFunction
External API
UI automation
Human-assisted
```

---

## 2.2 Agent Core не зависит от Provider

Agent Core работает с capability-контрактом.

Он не должен содержать:

```text
if provider == AndroidCalendar:
    ...
```

Вместо этого:

```text
Agent Core
    ↓
Capability Request
    ↓
Strategy Engine
    ↓
Provider Adapter
```

---

## 2.3 Capability должна быть версионируемой

Каждая capability имеет собственную версию контракта:

```text
id:
    calendar.create_event

version:
    1.0
```

Изменение входного или выходного контракта, влияющее на совместимость, требует новой версии.

---

# 3. Capability Contract

Базовая структура:

```text
CapabilityContract
 ├─ id
 ├─ version
 ├─ name
 ├─ description
 ├─ category
 ├─ input
 ├─ output
 ├─ preconditions
 ├─ permissions
 ├─ risk
 ├─ confirmation
 ├─ strategies
 ├─ availability
 ├─ verification
 └─ metadata
```

---

# 4. JSON-представление

Предварительная машинно-читаемая форма:

```json
{
  "id": "calendar.create_event",
  "version": "1.0",
  "name": "Create calendar event",
  "description": "Create a calendar event according to the user's goal.",
  "category": "productivity",

  "input": {
    "schema": "CalendarCreateEventInput",
    "required": [
      "title",
      "start_time",
      "end_time"
    ]
  },

  "output": {
    "schema": "CalendarCreateEventOutput"
  },

  "preconditions": [],

  "permissions": [
    "calendar.write"
  ],

  "risk": {
    "level": "R2"
  },

  "confirmation": {
    "required_by_default": false,
    "required_when": [
      "external_participants",
      "ambiguous_time",
      "high_impact_context"
    ]
  },

  "strategies": [
    "native_calendar_provider",
    "official_app_function",
    "ui_automation",
    "human_assisted"
  ],

  "availability": {
    "offline": true
  },

  "verification": {
    "required": true,
    "method": "calendar_event_lookup"
  },

  "metadata": {
    "contract_version": "0.1"
  }
}
```

Это **архитектурный контракт**, а не окончательная JSON Schema.

---

# 5. Input Contract

Input описывает данные, необходимые для выполнения capability.

Пример:

```json
{
  "title": "Встреча с Иваном",
  "start_time": "2026-10-08T15:00:00+03:00",
  "end_time": "2026-10-08T16:00:00+03:00",
  "location": null,
  "description": null,
  "participants": []
}
```

Важно:

Natural Language не является Input Contract.

```text
"Создай завтра встречу с Иваном в три"
```

сначала преобразуется:

```text
Language
    ↓
Goal Model
    ↓
Capability Input
```

---

# 6. Input Validation

Перед передачей capability на выполнение должны быть проверены:

- обязательные поля;
- типы;
- диапазоны;
- форматы;
- взаимосвязи полей;
- временная зона;
- неоднозначность;
- потенциально опасные значения.

Например:

```text
start_time < end_time
```

должно быть проверено до Execution.

---

# 7. Output Contract

Output описывает результат выполнения.

Пример:

```json
{
  "status": "CREATED",
  "resource_id": "event-123",
  "provider": "android_calendar",
  "created_at": "2026-10-07T10:00:00+03:00"
}
```

Однако:

> Output capability ≠ Proof of success.

`status = CREATED` означает результат работы execution adapter.

Доказательство результата определяется Verification Contract.

---

# 8. Execution Result

Базовые состояния:

```text
SUCCESS
FAILED
PARTIAL
CANCELLED
BLOCKED
REQUIRES_USER
REQUIRES_PERMISSION
TIMEOUT
UNKNOWN
```

Особенно важно различать:

```text
FAILED
```

и:

```text
UNKNOWN
```

`UNKNOWN` означает:

> Агент не смог надёжно определить, произошло действие или нет.

Это критично для повторных попыток.

---

# 9. Preconditions

Preconditions описывают условия, которые должны быть выполнены до запуска capability.

Примеры:

```text
calendar.create_event

Preconditions:
- device_available
- calendar_provider_available
- valid_input
- required_permissions_available
```

Preconditions могут быть:

```text
STATIC
DYNAMIC
ENVIRONMENTAL
USER_DEPENDENT
```

---

# 10. Permissions

Permission model является частью capability contract.

Пример:

```json
{
  "permissions": {
    "required": [
      "calendar.write"
    ],
    "optional": []
  }
}
```

Capability не должна самостоятельно обходить permission system.

Если permission отсутствует:

```text
REQUIRES_PERMISSION
```

и управление возвращается в Agent Core.

---

# 11. Risk Contract

Минимальная структура:

```json
{
  "level": "R2",
  "factors": [
    "state_change",
    "external_effect"
  ]
}
```

Risk определяется не только capability.

Фактический риск:

```text
Capability
+
Input
+
Context
+
Target
+
Strategy
+
Provider
=
Effective Risk
```

Поэтому:

```text
R2 capability
```

не гарантирует, что любой конкретный вызов автоматически является R2.

---

# 12. Confirmation Policy

Confirmation policy должна быть контекстной.

Базовые режимы:

```text
NEVER_REQUIRED
CONTEXTUAL
ALWAYS_REQUIRED
POLICY_BLOCKED
```

Пример:

```text
calendar.create_event
    normal personal event
        → CONTEXTUAL

    sensitive external commitment
        → confirmation may be required
```

Подтверждение не заменяет:

- permission;
- Safety Policy;
- verification.

---

# 13. Strategy Contract

Capability объявляет допустимые стратегии, но не обязана выбирать одну.

Например:

```json
{
  "strategies": [
    {
      "id": "native_calendar_provider",
      "priority_hint": 100
    },
    {
      "id": "official_app_function",
      "priority_hint": 90
    },
    {
      "id": "ui_automation",
      "priority_hint": 40
    },
    {
      "id": "human_assisted",
      "priority_hint": 20
    }
  ]
}
```

`priority_hint` — рекомендация, а не приказ.

Strategy Engine может изменить порядок на основании:

- availability;
- permissions;
- risk;
- experience;
- verification quality;
- current environment.

---

# 14. Availability Contract

Capability должна сообщать текущее состояние.

```json
{
  "status": "AVAILABLE",
  "checked_at": "2026-10-07T10:00:00+03:00",
  "reason": null
}
```

Возможные состояния:

```text
AVAILABLE
UNAVAILABLE
BLOCKED
REQUIRES_PERMISSION
REQUIRES_USER
TEMPORARILY_UNAVAILABLE
UNKNOWN
```

---

# 15. Verification Contract

Verification является отдельной частью capability.

Базовая структура:

```json
{
  "required": true,
  "strategy": "calendar_event_lookup",
  "independence": "independent",
  "confidence": 0.98
}
```

Проверка должна по возможности использовать механизм, независимый от непосредственного execution response.

Пример:

```text
Execute:
    create event

Verify:
    search calendar
    match expected properties
    confirm event exists
```

---

# 16. Verification Levels

Предлагаются следующие уровни:

### V0 — no verification

Проверка отсутствует или невозможна.

### V1 — response verification

Проверяется только ответ execution mechanism.

### V2 — state verification

Проверяется фактическое состояние системы.

### V3 — independent verification

Результат подтверждается независимым механизмом.

### V4 — externally confirmed result

Результат подтверждён внешней системой/стороной.

Для значимых действий Agent Core должен стремиться к V2/V3.

---

# 17. Cancellation Contract

Каждый execution request должен иметь:

```text
task_id
execution_id
cancellation_token
```

Пример:

```json
{
  "task_id": "task-001",
  "execution_id": "exec-001",
  "cancellation_token": "cancel-001"
}
```

Это необходимо для STOP и защиты от stale execution.

Если пользователь остановил задачу:

```text
STOP
 ↓
cancel current execution
 ↓
block new actions from old plan
 ↓
record actual state
```

---

# 18. Idempotency

Capability должна по возможности указывать:

```text
idempotency:
    supported
```

или:

```text
idempotency:
    unsupported
```

Это особенно важно после:

```text
TIMEOUT
UNKNOWN
PARTIAL
```

Агент не должен автоматически повторять потенциально неидемпотентное действие, если не понимает, было ли оно выполнено.

---

# 19. Side Effects

Capability должна описывать потенциальные side effects.

Пример:

```json
{
  "side_effects": [
    "calendar_state_changed",
    "external_participants_may_be_notified"
  ]
}
```

Это помогает:

- Safety Policy;
- Confirmation Policy;
- Planner;
- Verification;
- Result explanation.

---

# 20. Privacy Classification

Capability может иметь классификацию данных:

```text
PUBLIC
LOCAL
PERSONAL
SENSITIVE
HIGHLY_SENSITIVE
```

Например:

```text
web.search
    → PUBLIC / user-query dependent

contact.search
    → PERSONAL

notification.read
    → SENSITIVE
```

Privacy classification должна учитываться при выборе:

- local model;
- cloud model;
- external provider;
- logging;
- Experience Store.

---

# 21. Data Handling Policy

Capability contract может указывать:

```json
{
  "data_policy": {
    "cloud_allowed": false,
    "experience_storage": "metadata_only",
    "audit_logging": "redacted"
  }
}
```

Это не заменяет глобальную Privacy Policy.

Это локальное описание требований capability.

---

# 22. Provider Contract

Provider не является частью Capability ID.

Пример:

```text
Capability:
    calendar.create_event

Provider A:
    android_calendar_provider

Provider B:
    google_calendar_api

Provider C:
    calendar_app_function

Provider D:
    calendar_ui
```

Каждый provider должен объявлять:

```text
provider_id
capabilities_supported
availability
permissions
risk modifiers
verification support
offline support
version
```

---

# 23. Capability Registry

Registry отвечает за поиск capability.

Пример:

```text
CapabilityRegistry.find(
    goal = "создать встречу"
)
```

может вернуть:

```text
calendar.create_event
```

Agent Core не обязан знать все capability заранее.

---

# 24. Capability Discovery

Рекомендуемый pipeline:

```text
User Goal
    ↓
Goal Understanding
    ↓
Capability Discovery
    ↓
Candidate Capabilities
    ↓
Capability Matching
    ↓
Strategy Search
```

Например:

```text
"Напомни мне завтра купить молоко"

Possible candidates:
    reminder.create
    calendar.create_event
    notification.create
```

Planner должен выбрать capability, наиболее соответствующую цели, а не просто первую совпавшую.

---

# 25. Error Model

Ошибки capability должны быть структурированными.

Пример:

```json
{
  "code": "PERMISSION_DENIED",
  "recoverable": true,
  "retryable": false,
  "requires_user": true,
  "message": "Calendar permission is required."
}
```

Минимальные свойства:

```text
code
category
recoverable
retryable
requires_user
details
```

---

# 26. Re-planning

Ошибка capability не обязательно означает провал цели.

Например:

```text
calendar.create_event
    ↓
Native provider unavailable
    ↓
Strategy failure
    ↓
Strategy Engine
    ↓
Official AppFunction available
    ↓
Retry
```

Таким образом:

```text
Strategy failure
≠
Goal failure
```

Это один из центральных принципов агента.

---

# 27. Experience Contract

Experience может хранить:

```text
goal_fingerprint
capability_id
strategy_id
provider_id
outcome
verification_level
confidence
timestamp
environment
```

Но Experience не может менять capability contract.

Experience только помогает выбрать стратегию.

---

# 28. Audit Contract

Для значимых действий должен существовать audit record.

Пример:

```text
task_id
goal_id
capability_id
strategy_id
provider_id
started_at
finished_at
result_status
verification_status
risk_level
confirmation_status
```

Audit log не должен автоматически содержать:

- полные личные данные;
- секреты;
- токены;
- необработанные чувствительные данные.

---

# 29. Полный execution pipeline

Итоговая модель:

```text
USER
  ↓
GOAL
  ↓
CAPABILITY DISCOVERY
  ↓
CAPABILITY CONTRACT
  ↓
STRATEGY SELECTION
  ↓
SAFETY POLICY
  ↓
PERMISSION CHECK
  ↓
CONFIRMATION
  ↓
EXECUTION
  ↓
RESULT
  ↓
VERIFICATION
  ↓
SUCCESS / FAILURE / UNKNOWN
  ↓
RE-PLAN
```

При STOP:

```text
STOP
 ↓
Cancellation Controller
 ↓
cancel execution
 ↓
block stale actions
 ↓
record actual state
 ↓
report result
```

---

# 30. Первичный контракт calendar.create_event

Концептуальная версия:

```json
{
  "id": "calendar.create_event",
  "version": "1.0",

  "input": {
    "title": "string",
    "start_time": "datetime",
    "end_time": "datetime",
    "location": "string|null",
    "description": "string|null",
    "participants": "Participant[]"
  },

  "output": {
    "status": "SUCCESS|FAILED|PARTIAL|CANCELLED|UNKNOWN",
    "resource_id": "string|null"
  },

  "permissions": [
    "calendar.write"
  ],

  "risk": {
    "level": "R2"
  },

  "strategies": [
    "native_calendar_provider",
    "official_app_function",
    "ui_automation",
    "human_assisted"
  ],

  "verification": {
    "required": true,
    "minimum_level": "V2"
  },

  "idempotency": {
    "supported": true
  },

  "cancellation": {
    "supported": true
  }
}
```

---

# 31. Что контракт НЕ должен содержать

Capability Contract не должен содержать:

- конкретные UI coordinates;
- конкретные screen IDs;
- LLM prompts;
- внутреннюю chain-of-thought;
- конкретный layout приложения;
- жёсткую последовательность кликов;
- секреты;
- API tokens;
- implementation-specific state;
- инструкции по обходу Android permissions.

Это относится к другим слоям архитектуры.

---

# 32. Архитектурное преимущество

Capability Contract позволяет заменить:

```text
Android API
```

на:

```text
AppFunction
```

или:

```text
External API
```

или:

```text
UI automation
```

без изменения пользовательской цели и без переписывания Agent Core.

То же самое относится к моделям:

```text
Local LLM
Cloud LLM
Another Model
```

Agent Core продолжает работать через стабильные контракты.

---

# 33. Следующий шаг

После утверждения Capability Contract v0.1 логично спроектировать:

**Strategy Contract v0.1**

Он должен формально описать:

- как стратегия объявляет свои возможности;
- как оценивается её пригодность;
- как учитываются риск и permissions;
- как сравниваются несколько стратегий;
- как Experience влияет на выбор;
- как Strategy Engine выбирает победителя;
- когда разрешается fallback;
- когда стратегия должна быть отклонена.

Это позволит перейти от:

```text
Capability существует
```

к:

```text
Agent умеет выбирать лучший способ достижения цели.
```

---

## Статус

**Capability Contract v0.1 — сформирован.**

**Код не изменялся.**

**Следующий этап: Strategy Contract v0.1.**
