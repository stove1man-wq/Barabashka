# Independent Android AI Agent — Strategy Contract v0.1

**Статус:** архитектурный baseline, pre-code  
**Версия:** 0.1  
**Дата:** 2026-10-07  
**Связанные документы:** Capability Matrix MVP v0.1, Capability Contract v0.1

---

# 1. Назначение

Strategy Contract определяет формальный интерфейс для способов выполнения capability.

Если:

```text
Capability = WHAT
```

то:

```text
Strategy = HOW
```

Strategy Contract отвечает на вопрос:

> **Как конкретно агент может попытаться достичь capability в текущем окружении?**

Стратегия не является самой capability и не является конкретным UI/API provider.

---

# 2. Архитектурное разделение

```text
USER GOAL
    ↓
CAPABILITY
    ↓
STRATEGY
    ↓
PROVIDER
    ↓
EXECUTION ADAPTER
```

Пример:

```text
Goal:
"Создай встречу завтра в 15:00"

Capability:
calendar.create_event

Strategy:
native_calendar_provider

Provider:
Android Calendar Provider

Execution Adapter:
AndroidCalendarAdapter
```

Другой вариант:

```text
Capability:
calendar.create_event

Strategy:
official_app_function

Provider:
Calendar application

Execution Adapter:
AppFunctionAdapter
```

---

# 3. Основные принципы Strategy Engine

## 3.1 Не существует вечного "лучшего" способа

Стратегия может быть лучшей:

- на одном устройстве;
- для одного пользователя;
- при одном наборе permissions;
- в одном состоянии приложения;
- в одной версии Android;
- при наличии сети;
- в конкретный момент времени.

Поэтому:

> **Best Strategy = best available strategy for current context.**

---

## 3.2 Priority ≠ Decision

Стратегия может иметь:

```text
priority_hint = 100
```

но это не означает, что она будет выбрана.

Например:

```text
Native Provider
priority = 100
availability = false
```

не может победить:

```text
AppFunction
priority = 80
availability = true
```

---

# 4. Strategy Contract

Базовая структура:

```text
StrategyContract
 ├─ id
 ├─ version
 ├─ name
 ├─ description
 ├─ supported_capabilities
 ├─ provider
 ├─ preconditions
 ├─ requirements
 ├─ risk
 ├─ cost
 ├─ reliability
 ├─ verification
 ├─ cancellation
 ├─ idempotency
 ├─ availability
 └─ execution_adapter
```

---

# 5. Пример JSON

```json
{
  "id": "native_calendar_provider",
  "version": "1.0",
  "name": "Native Android Calendar Provider",
  "description": "Create and manage calendar events through Android structured calendar access.",

  "supported_capabilities": [
    "calendar.search_events",
    "calendar.create_event",
    "calendar.update_event"
  ],

  "provider": {
    "id": "android_calendar_provider",
    "type": "native"
  },

  "requirements": {
    "permissions": [
      "calendar.write"
    ],
    "internet": false
  },

  "risk": {
    "base_level": "R2"
  },

  "cost": {
    "latency": "low",
    "resource_usage": "low",
    "network": "none"
  },

  "reliability": {
    "expected": "high"
  },

  "verification": {
    "supported": true,
    "minimum_level": "V2"
  },

  "cancellation": {
    "supported": true
  },

  "idempotency": {
    "supported": true
  }
}
```

---

# 6. Strategy Preconditions

Стратегия может объявить предварительные условия.

Пример:

```text
native_calendar_provider

Preconditions:
    calendar_provider_available
    calendar_permission_granted
    valid_calendar_account
```

Preconditions бывают:

```text
STATIC
DYNAMIC
ENVIRONMENTAL
USER_DEPENDENT
```

---

# 7. Strategy Availability

Стратегия должна иметь собственную availability-модель.

```text
AVAILABLE
UNAVAILABLE
BLOCKED
REQUIRES_PERMISSION
REQUIRES_USER
TEMPORARILY_UNAVAILABLE
UNKNOWN
```

Важно:

```text
Capability AVAILABLE
```

не означает:

```text
Every Strategy AVAILABLE
```

Например:

```text
calendar.create_event
    AVAILABLE

native_calendar_provider
    REQUIRES_PERMISSION

ui_automation
    AVAILABLE
```

В таком случае Strategy Engine может рассматривать UI automation как fallback.

---

# 8. Strategy Candidate

Перед выбором стратегия превращается в candidate:

```text
StrategyCandidate
 ├─ strategy_id
 ├─ capability_id
 ├─ provider_id
 ├─ availability
 ├─ estimated_risk
 ├─ estimated_cost
 ├─ reliability_score
 ├─ verification_score
 ├─ experience_score
 ├─ compatibility_score
 └─ confidence
```

Candidate — это оценка стратегии в текущем контексте, а не изменение самого Strategy Contract.

---

# 9. Факторы выбора

Strategy Engine оценивает минимум:

```text
Availability
Permissions
Risk
Reliability
Verification
Experience
Compatibility
Latency
Resource cost
Network dependency
Privacy
User confirmation burden
```

---

# 10. Модель оценки

В MVP можно использовать детерминированную scoring model.

Концептуально:

```text
Score =
    Availability
  + Reliability
  + Verification
  + Experience
  + Compatibility
  - RiskCost
  - ResourceCost
  - LatencyCost
  - PrivacyCost
  - UserFriction
```

Это не окончательная математическая формула.

Она должна быть:

- объяснимой;
- детерминированной;
- тестируемой;
- версионируемой.

---

# 11. Hard Constraints vs Soft Factors

Это критически важное разделение.

## Hard constraints

Если условие не выполнено, стратегия исключается.

Примеры:

```text
required permission missing
unsupported Android version
provider unavailable
mandatory network absent
policy explicitly blocks strategy
```

Результат:

```text
ELIMINATED
```

---

## Soft factors

Если фактор хуже, стратегия не обязательно исключается.

Примеры:

```text
higher latency
lower historical reliability
higher resource usage
less convenient verification
lower experience confidence
```

Результат:

```text
LOWER SCORE
```

---

# 12. Strategy Selection Pipeline

```text
Capability
    ↓
Discover Strategies
    ↓
Build Candidates
    ↓
Hard Constraint Filter
    ↓
Safety / Policy Filter
    ↓
Score Candidates
    ↓
Rank
    ↓
Select
    ↓
Execute
```

---

# 13. Safety Boundary

Strategy Engine не может самостоятельно отменять Safety Policy.

Правильная архитектура:

```text
Strategy Candidates
        ↓
Local Safety Policy
        ↓
Allowed Candidates
        ↓
Ranking
        ↓
Selected Strategy
```

Неправильно:

```text
Strategy Engine
    ↓
"эта стратегия удобнее"
    ↓
bypass safety
```

---

# 14. Risk-aware Selection

Риск стратегии может отличаться от базового риска capability.

Например:

```text
Capability:
message.send
Base Risk:
R3
```

Стратегия:

```text
Official Messaging API
Risk modifier:
+0
```

Другой вариант:

```text
UI automation
Risk modifier:
+1
```

Получаем:

```text
Effective Risk =
Capability Risk
+
Strategy Risk Modifier
+
Context Modifier
```

---

# 15. Verification-aware Selection

Стратегия с лучшей возможностью проверки результата может быть предпочтительнее даже при большей стоимости.

Например:

```text
Strategy A
fast
cheap
verification = V1

Strategy B
slower
verification = V3
```

Для значимого действия:

```text
Strategy B
```

может победить.

Это реализует принцип:

> **Result over Process.**

---

# 16. Experience-aware Selection

Experience Store предоставляет рекомендацию:

```text
strategy:
native_calendar_provider

success_rate:
0.97

samples:
124

last_success:
recent
```

Но Experience не является гарантией.

```text
Experience
    ↓
Score modifier
    ↓
Safety Policy
    ↓
Selection
```

Нельзя:

```text
Experience says safe
→ skip safety
```

---

# 17. Cold Start

Для новой стратегии:

```text
experience_score = neutral
```

Она не должна автоматически считаться плохой только из-за отсутствия истории.

Например:

```text
new AppFunction
```

может оказаться лучшей стратегией, несмотря на:

```text
samples = 0
```

если она:

- доступна;
- безопасна;
- хорошо проверяется;
- совместима;
- соответствует цели.

---

# 18. Exploration vs Exploitation

В будущем Strategy Engine может балансировать:

```text
Exploitation:
использовать проверенный способ

Exploration:
попробовать новый потенциально лучший способ
```

Но exploration не должно означать рискованные эксперименты на реальных данных.

Безопасная exploration может выполняться:

- в sandbox;
- на тестовых данных;
- через dry-run;
- с пользовательским подтверждением;
- при низком риске.

Для MVP рекомендуется:

> **Conservative exploration.**

---

# 19. Fallback

Fallback — это не просто:

```text
try another API
```

Это новый цикл оценки.

```text
Strategy A
    ↓
FAIL
    ↓
Analyze failure
    ↓
Update environment state
    ↓
Re-evaluate candidates
    ↓
Strategy B
```

---

# 20. Failure Classification

Минимальные категории:

```text
UNAVAILABLE
PERMISSION_DENIED
INVALID_INPUT
TIMEOUT
TRANSIENT_FAILURE
PROVIDER_ERROR
EXECUTION_ERROR
VERIFICATION_FAILED
UNKNOWN
POLICY_BLOCKED
USER_CANCELLED
```

Тип ошибки влияет на возможность fallback.

Например:

```text
INVALID_INPUT
```

может требовать исправления Goal/Input.

А:

```text
PROVIDER_ERROR
```

может разрешать переход к другой стратегии.

---

# 21. Retry Policy

Retry и fallback — разные вещи.

### Retry

Повтор той же стратегии.

```text
same strategy
same provider
new attempt
```

Подходит для:

```text
TRANSIENT_FAILURE
TIMEOUT
```

при соблюдении idempotency/safety.

### Fallback

Переход на другую стратегию.

```text
strategy A
    ↓
strategy B
```

Подходит для:

```text
UNAVAILABLE
PROVIDER_ERROR
unsupported path
```

---

# 22. UNKNOWN Result

Особенно опасен результат:

```text
UNKNOWN
```

Например:

```text
message.send
    ↓
network timeout
    ↓
Did message actually send?
    ↓
UNKNOWN
```

Нельзя автоматически делать:

```text
retry send
```

пока не выполнена проверка, потому что возможно:

```text
message was already sent
```

Strategy Engine должен сначала искать:

```text
verification strategy
```

---

# 23. Re-planning

Strategy failure не означает Goal failure.

Полный цикл:

```text
Goal
 ↓
Capability
 ↓
Strategy A
 ↓
Failure
 ↓
Re-evaluate
 ↓
Strategy B
 ↓
Verification
 ↓
Success
```

Если все стратегии исчерпаны:

```text
Goal Failure
```

или:

```text
REQUIRES_USER
```

---

# 24. Strategy Diversity

Для одной capability желательно иметь несколько независимых стратегий.

Например:

```text
calendar.create_event

1. native provider
2. official AppFunction
3. external API
4. UI automation
5. human-assisted
```

Но количество стратегий не должно быть самоцелью.

Стратегия полезна только если она реально отличается механизмом или существенно меняет надёжность/доступность.

---

# 25. Independence of Strategies

Две стратегии считаются независимыми, если отказ одной не делает автоматически невозможной другую.

Пример:

```text
Native Calendar Provider
```

и:

```text
UI Automation
```

частично независимы.

Но:

```text
API wrapper A
```

и:

```text
API wrapper B
```

могут зависеть от одного и того же backend.

Это важно для fallback.

---

# 26. Provider vs Strategy

Не следует смешивать:

```text
Provider = source/mechanism
Strategy = method of using it
```

Например:

```text
Provider:
Google Calendar API

Strategy:
structured_api_call
```

Другой provider:

```text
Android Calendar Provider

Strategy:
native_provider_access
```

Одна стратегия может поддерживать несколько providers.

---

# 27. Execution Adapter

Strategy не должна содержать низкоуровневую реализацию.

Архитектура:

```text
Strategy Contract
       ↓
Execution Adapter
       ↓
Provider
       ↓
Android / Network / UI
```

Например:

```text
NativeCalendarStrategy
       ↓
AndroidCalendarAdapter
       ↓
Calendar Provider
```

---

# 28. Cancellation

Каждая стратегия должна объявлять:

```text
cancellation.supported
```

и желательно:

```text
cancellation.granularity
```

Например:

```text
IMMEDIATE
BEST_EFFORT
NOT_SUPPORTED
```

STOP должен приводить к прекращению дальнейших действий стратегии, насколько это технически возможно.

---

# 29. Idempotency

Стратегия должна сообщать:

```text
SUPPORTED
UNSUPPORTED
CONDITIONAL
```

Это влияет на retry.

Например:

```text
calendar.create_event
```

может поддерживать идемпотентность через client-generated request ID.

Если idempotency неизвестна:

```text
UNKNOWN
```

повтор потенциально опасного действия должен быть ограничен.

---

# 30. Resource Cost

Стратегия может описывать:

```text
CPU
GPU
RAM
battery
network
latency
storage
```

В MVP достаточно грубой оценки:

```text
LOW
MEDIUM
HIGH
```

Пример:

```text
native API:
LOW

LLM + cloud:
MEDIUM/HIGH

UI automation:
MEDIUM

human-assisted:
LOW device cost / HIGH user cost
```

---

# 31. Privacy Cost

Стратегия должна учитывать передачу данных.

Пример:

```text
Local strategy:
privacy cost = LOW

Cloud API:
privacy cost = CONTEXTUAL

External third-party service:
privacy cost = HIGHER
```

Это особенно важно для:

- contacts;
- notifications;
- files;
- messages;
- location.

Privacy policy может полностью исключить cloud strategy.

---

# 32. User Friction

Некоторые стратегии требуют участия пользователя.

Пример:

```text
native API
    user friction = LOW

UI automation
    user friction = MEDIUM

human-assisted
    user friction = HIGH
```

При равных прочих условиях Strategy Engine должен предпочитать путь с меньшим вмешательством пользователя.

Но:

> User friction не может победить безопасность.

---

# 33. Deterministic Decision Record

Каждый выбор стратегии должен быть объясним.

Пример:

```text
Decision:
native_calendar_provider

Rejected:
official_app_function
Reason:
not available

Rejected:
ui_automation
Reason:
higher risk + lower verification

Selected:
native_calendar_provider
Reason:
available + low cost + V2 verification + high reliability
```

Для MVP это может быть структурированный Decision Record.

В дальнейшем его можно хешировать/подписывать аналогично другим integrity-механизмам проекта.

---

# 34. Strategy Decision Record

Предлагаемая структура:

```text
StrategyDecision
 ├─ decision_id
 ├─ task_id
 ├─ capability_id
 ├─ candidates[]
 ├─ rejected[]
 ├─ selected_strategy
 ├─ selected_provider
 ├─ scoring_version
 ├─ policy_version
 ├─ experience_reference
 ├─ timestamp
 └─ reason_summary
```

---

# 35. Пример Decision Record

```json
{
  "decision_id": "decision-001",
  "task_id": "task-001",
  "capability_id": "calendar.create_event",

  "candidates": [
    {
      "strategy": "native_calendar_provider",
      "score": 0.94,
      "status": "SELECTED"
    },
    {
      "strategy": "official_app_function",
      "score": 0.82,
      "status": "REJECTED",
      "reason": "unavailable"
    },
    {
      "strategy": "ui_automation",
      "score": 0.51,
      "status": "REJECTED",
      "reason": "higher risk"
    }
  ],

  "selected_strategy": "native_calendar_provider",
  "selected_provider": "android_calendar_provider",

  "scoring_version": "0.1",
  "policy_version": "0.1"
}
```

---

# 36. Dynamic Context

Strategy selection должна учитывать текущее состояние:

```text
Device
Android version
App state
Permissions
Network
Battery
Connectivity
User preferences
Current task
Current risk context
Available providers
Recent failures
Experience
```

Это означает:

> Strategy selection является runtime decision.

---

# 37. User Preferences

Пользователь может влиять на soft factors.

Например:

```text
Prefer:
- local execution
- privacy
- battery saving
- speed
- minimal confirmations
```

Но пользовательские предпочтения не должны обходить hard safety constraints.

Пример:

```text
User preference:
"Never ask me."

Policy:
confirmation required.

Result:
confirmation still required.
```

---

# 38. Strategy Policy Hierarchy

Рекомендуемый порядок:

```text
1. Platform / Security Constraints
2. Local Safety Policy
3. Capability Contract
4. Current Environment
5. User Constraints
6. Privacy Policy
7. Strategy Scoring
8. Experience
9. Optimization Preferences
```

Более высокие уровни не могут быть отменены нижними.

---

# 39. Strategy Engine — MVP

На первом этапе не нужен сложный AI-оптимизатор.

Достаточно:

```text
1. discover
2. filter hard constraints
3. apply safety
4. calculate deterministic score
5. rank
6. select
7. execute
8. verify
9. re-plan if necessary
```

Это должно быть полностью тестируемо.

---

# 40. Почему не отдавать выбор стратегии напрямую LLM

LLM может:

- понять пользовательскую цель;
- предложить capabilities;
- предложить возможные стратегии;
- объяснить варианты.

Но LLM не должна единолично решать:

```text
"Эта стратегия разрешена, значит выполняем."
```

Финальное решение принадлежит детерминированным слоям:

```text
Safety
+
Permissions
+
Environment
+
Strategy Engine
```

---

# 41. LLM как Strategy Advisor

Допустимая модель:

```text
LLM
 ↓
Strategy Candidates / Hints
 ↓
Registry Validation
 ↓
Safety
 ↓
Deterministic Ranking
 ↓
Execution
```

Недопустимая модель:

```text
LLM
 ↓
raw tool call
 ↓
Android action
```

без промежуточных ограничений.

---

# 42. Central Knowledge Network

В будущем центральная система может рекомендовать:

```text
strategy_id
provider_id
compatibility
known_failures
best_practices
```

Но:

```text
Central Knowledge
        ↓
Local Validation
        ↓
Local Safety
        ↓
Local Strategy Selection
```

Центральный сервер не должен напрямую выбирать и выполнять действие на устройстве.

---

# 43. Strategy Versioning

Стратегия должна иметь:

```text
strategy_id
version
provider_version
compatibility
```

Изменение существенной логики требует новой версии.

Decision Record должен сохранять версии, чтобы решение можно было воспроизвести.

---

# 44. Compatibility

Стратегия может иметь ограничения:

```text
Android >= 16
```

или:

```text
requires provider version >= X
```

или:

```text
requires AppFunction support
```

Compatibility проверяется до scoring.

---

# 45. Graceful Degradation

Если лучший путь недоступен:

```text
Best Strategy unavailable
        ↓
Next viable strategy
        ↓
...
        ↓
Human-assisted
        ↓
REQUIRES_USER
```

Но деградация не должна снижать safety ниже допустимого уровня.

---

# 46. Strategy Exhaustion

Когда кандидаты закончились:

```text
No viable strategy
```

Agent Core должен различать:

```text
GOAL_UNACHIEVABLE
```

и:

```text
TEMPORARILY_UNAVAILABLE
```

и:

```text
REQUIRES_USER
```

Это позволит корректно продолжить задачу позже.

---

# 47. Первый пример

Goal:

```text
"Создай завтра в 15:00 встречу с Иваном."
```

Capability:

```text
calendar.create_event
```

Candidates:

```text
A:
native_calendar_provider
available
risk R2
verification V2
experience 0.97

B:
official_app_function
available
risk R2
verification V2
experience 0.61

C:
ui_automation
available
risk R3
verification V1
experience 0.88
```

После hard constraints:

```text
A
B
C
```

После safety/risk:

```text
A
B
C lower priority
```

После scoring:

```text
A = 0.94
B = 0.79
C = 0.48
```

Выбор:

```text
A
```

---

# 48. Failure Example

Если A завершился:

```text
PROVIDER_ERROR
```

Pipeline:

```text
A failure
 ↓
classify failure
 ↓
re-evaluate candidates
 ↓
B
 ↓
execute
 ↓
verify
```

Если B также не сработал:

```text
C
```

может быть рассмотрен только если:

- safety позволяет;
- risk acceptable;
- verification достаточна;
- user confirmation obtained if required.

---

# 49. Core Invariant

Главный инвариант:

> **Strategy Engine optimizes the path to the goal, but never owns the authority to violate safety.**

То есть:

```text
Strategy Engine
    ≠
Safety Authority
```

---

# 50. Архитектурная формула

Полная модель:

```text
Goal
  ↓
Capability
  ↓
Candidate Strategies
  ↓
Hard Constraints
  ↓
Safety
  ↓
Context
  ↓
Scoring
  ↓
Best Available Strategy
  ↓
Execution
  ↓
Verification
  ↓
Result
```

При необходимости:

```text
Failure
  ↓
Re-plan
  ↓
New Strategy
```

---

# 51. Что Strategy Contract сознательно НЕ решает

Пока не определяются:

- конкретная математическая формула scoring;
- реализация Strategy Registry;
- реализация Execution Engine;
- конкретные Android adapters;
- формат Experience Store;
- формат центральной базы знаний;
- конкретный LLM;
- конкретные AppFunctions;
- UI.

Эти вопросы должны быть вынесены в отдельные архитектурные документы.

---

# 52. Следующий шаг

После Strategy Contract логично формализовать:

## Strategy Selection & Scoring Model v0.1

Это будет отдельный документ, где мы определим:

1. hard constraints;
2. soft factors;
3. веса;
4. нормализацию;
5. risk penalty;
6. verification bonus;
7. experience weighting;
8. privacy weighting;
9. user-friction weighting;
10. deterministic tie-breaking;
11. fallback;
12. decision record;
13. тестируемость выбора.

После него можно будет перейти к **Execution Contract v0.1**.

---

# 53. Статус

**Strategy Contract v0.1 — сформирован.**

**Код не изменялся.**

**Следующий этап: Strategy Selection & Scoring Model v0.1.**
