# Independent Android AI Agent — Strategy Selection & Scoring Model v0.1

**Статус:** архитектурный baseline, pre-code  
**Версия:** 0.1  
**Дата:** 2026-10-07  
**Связанные документы:** Capability Matrix MVP v0.1, Capability Contract v0.1, Strategy Contract v0.1

---

# 1. Назначение

Этот документ определяет механизм выбора лучшей стратегии для достижения цели пользователя.

Главный принцип:

> **Scoring выбирает лучший из допустимых вариантов. Он никогда не делает недопустимый вариант допустимым.**

Архитектура разделяется на два этапа:

```text
Candidate Generation
        ↓
Hard Constraint / Safety Filtering
        ↓
Scoring
        ↓
Ranking
        ↓
Selection
```

---

# 2. Главная архитектурная идея

Пусть существует:

```text
Goal
  ↓
Capability
  ↓
Strategy Candidates
```

Некоторые кандидаты должны быть исключены ещё до scoring.

Например:

```text
Strategy A
permission missing
        ↓
ELIMINATED

Strategy B
policy blocked
        ↓
ELIMINATED

Strategy C
available + safe
        ↓
SCORING
```

Нельзя делать:

```text
Strategy A
very high score
        ↓
permission missing
        ↓
execute anyway
```

---

# 3. Hard Constraints

Hard constraint — условие, без которого стратегия не может быть выполнена.

Минимальный набор:

```text
HC-01 capability compatibility
HC-02 provider availability
HC-03 required permissions
HC-04 platform compatibility
HC-05 mandatory network requirement
HC-06 required authentication
HC-07 safety policy
HC-08 privacy policy
HC-09 user constraints
HC-10 current environment constraints
```

Если хотя бы один критический hard constraint не выполнен:

```text
Candidate → ELIMINATED
```

---

# 4. Hard Constraint Examples

## Permission

```text
calendar.write = false
```

Если стратегия требует:

```text
calendar.write
```

результат:

```text
ELIMINATED
```

---

## Safety

```text
policy = BLOCKED
```

Результат:

```text
ELIMINATED
```

Никакой score не может это изменить.

---

## Network

```text
strategy.requires_network = true
network.available = false
```

Если offline fallback отсутствует:

```text
ELIMINATED
```

---

# 5. Soft Factors

После hard filtering остаются допустимые кандидаты.

Тогда оцениваются soft factors:

```text
Reliability
Verification
Experience
Compatibility
Latency
Resource Cost
Privacy
User Friction
Network Dependency
Recovery Quality
```

Soft factor влияет на score, но сам по себе обычно не исключает стратегию.

---

# 6. Normalized Score

Для MVP каждый фактор нормализуется:

```text
0.0 ... 1.0
```

где:

```text
0.0 = worst
1.0 = best
```

Например:

```text
Reliability = 0.95
Verification = 1.00
Experience = 0.80
```

Это позволяет использовать единую модель.

---

# 7. Базовая формула

Предлагается начальная модель:

```text
Score =
    W_r  * Reliability
  + W_v  * Verification
  + W_e  * Experience
  + W_c  * Compatibility
  + W_p  * Privacy
  + W_rec * Recovery
  - W_l  * LatencyCost
  - W_res * ResourceCost
  - W_f  * UserFriction
  - W_n  * NetworkCost
  - W_risk * RiskPenalty
```

Сумма положительных и отрицательных компонентов нормализуется в диапазон:

```text
0.0 ... 1.0
```

Формула является версионируемой частью архитектуры.

---

# 8. Предварительные веса MVP

Для обычной задачи предлагаются:

```text
Reliability      0.22
Verification     0.20
Safety/Risk      0.18
Compatibility    0.12
Experience       0.10
Privacy          0.08
Recovery         0.04
Latency          0.02
Resource         0.02
User Friction    0.02
```

Но риск в этой записи требует особого обращения.

Safety нельзя просто компенсировать другими факторами.

Поэтому:

> **Risk имеет одновременно hard-boundary и scoring component.**

Если риск запрещён политикой:

```text
ELIMINATED
```

Если разрешён, но выше альтернативы:

```text
RISK PENALTY
```

---

# 9. Почему Reliability и Verification имеют большой вес

Цель агента:

> получить правильный результат.

Поэтому:

```text
fast but unverifiable
```

не обязательно лучше:

```text
slower but verifiable
```

Особенно для:

- сообщений;
- звонков;
- календаря;
- файлов;
- финансовых/чувствительных действий;
- внешних обязательств.

---

# 10. Verification Priority

Для значимых действий предлагается минимальная проверка:

```text
R0/R1:
V1 may be acceptable

R2:
prefer V2

R3:
prefer V3

R4:
special policy
```

Это не абсолютное правило, а baseline.

Если необходимый уровень verification недоступен:

```text
strategy may be eliminated
```

или:

```text
confirmation required
```

в зависимости от policy.

---

# 11. Risk Model

Effective Risk:

```text
EffectiveRisk =
    CapabilityRisk
  + StrategyModifier
  + ContextModifier
```

Затем:

```text
if EffectiveRisk > PolicyMaximum:
    ELIMINATED
```

Если:

```text
EffectiveRisk <= PolicyMaximum
```

то:

```text
RiskPenalty
```

может уменьшать score.

---

# 12. Пример Risk

Capability:

```text
message.send
R3
```

Strategy A:

```text
official API
modifier = 0
```

Strategy B:

```text
UI automation
modifier = +1
```

Получаем:

```text
A → R3
B → R4
```

Если policy разрешает R3, но блокирует R4:

```text
A → candidate
B → eliminated
```

Это сильнее обычного scoring.

---

# 13. Reliability

Reliability может рассчитываться из:

```text
historical success
recent success
environment compatibility
provider stability
verification outcomes
failure rate
```

Для MVP можно использовать:

```text
reliability_score
```

предоставленный Strategy Registry.

В дальнейшем его можно рассчитывать автоматически.

---

# 14. Experience Score

Experience должна быть:

- time-aware;
- environment-aware;
- capability-specific;
- strategy-specific.

Пример:

```text
strategy:
native_calendar_provider

successes:
120

failures:
4

recent_success:
yes

same_android_version:
yes
```

Experience Score:

```text
high
```

---

# 15. Experience Decay

Старая история не должна иметь тот же вес бесконечно.

Предлагается использовать decay:

```text
ExperienceWeight =
    e^(-λ * age)
```

где:

```text
age = возраст наблюдения
λ = decay parameter
```

Это позволит учитывать:

- обновления Android;
- обновления приложений;
- изменение API;
- изменение среды;
- устаревшие стратегии.

---

# 16. Cold Start

Если опыта нет:

```text
Experience = neutral
```

Например:

```text
ExperienceScore = 0.5
```

Новая стратегия не должна автоматически проигрывать только потому, что она новая.

---

# 17. Reliability vs Experience

Важно не смешивать:

```text
Reliability
```

и:

```text
Experience
```

Reliability:

> насколько стратегия технически надёжна по известным характеристикам.

Experience:

> насколько хорошо она реально работала в конкретной истории/среде.

Например:

```text
new strategy:
Reliability = 0.90
Experience = 0.50
```

---

# 18. Compatibility Score

Compatibility учитывает:

```text
Android version
device capabilities
installed apps
provider versions
current app state
network
permissions
account state
```

Пример:

```text
Strategy A:
Android >= 16
current device = Android 16
→ compatibility = 1.0
```

---

# 19. Privacy Score

Пример:

```text
local execution
→ privacy = 1.0

trusted local provider
→ privacy = 0.9

cloud provider with approved policy
→ privacy = 0.6

untrusted external provider
→ may be eliminated
```

Privacy score должен учитывать тип данных.

Например:

```text
public web query
```

и:

```text
private notification content
```

не должны оцениваться одинаково.

---

# 20. User Friction

User friction учитывает:

- количество подтверждений;
- необходимость переключения приложения;
- необходимость ручного ввода;
- ожидание;
- количество пользовательских шагов.

Пример:

```text
native API:
friction = 0.05

UI automation:
friction = 0.20

human-assisted:
friction = 0.80
```

Но:

> безопасность важнее удобства.

---

# 21. Latency

Latency:

```text
local API
→ low

local LLM
→ medium

cloud LLM/API
→ medium/high

human-assisted
→ high
```

Latency обычно является soft factor.

Но для time-critical задач она может стать hard constraint.

---

# 22. Resource Cost

Учитываются:

```text
CPU
GPU
RAM
battery
storage
network
```

MVP:

```text
LOW
MEDIUM
HIGH
```

Позже:

```text
continuous measurement
```

---

# 23. Recovery Quality

Стратегия может отличаться возможностью восстановиться после ошибки.

Пример:

```text
Strategy A
failure → can safely retry

Strategy B
failure → uncertain side effect
```

A получает более высокий:

```text
RecoveryScore
```

---

# 24. Network Cost

Network cost зависит от:

- необходимости сети;
- стабильности сети;
- стоимости трафика;
- privacy implications;
- latency.

Local-first предпочтение:

```text
if local strategy is equally capable:
    prefer local
```

Но это не абсолютный запрет cloud.

---

# 25. Contextual Weight Profiles

Одна формула не должна одинаково подходить всем задачам.

Предлагаются профили:

```text
STANDARD
PRIVACY_FIRST
SAFETY_FIRST
SPEED_FIRST
OFFLINE_FIRST
RESOURCE_FIRST
```

Пример:

### PRIVACY_FIRST

```text
Privacy weight ↑
Cloud penalty ↑
Local execution preference ↑
```

### SPEED_FIRST

```text
Latency weight ↑
Resource weight moderate
```

### SAFETY_FIRST

```text
Verification ↑
Risk penalty ↑
Recovery ↑
```

---

# 26. User Preferences

Пользователь может задавать предпочтения:

```text
"Старайся делать всё локально."

"Не используй облако без необходимости."

"Экономь батарею."

"Мне важнее скорость."
```

Это влияет на soft factors.

Но:

```text
User Preference
    ↓
cannot override
    ↓
Safety / Security / Permissions
```

---

# 27. Hard Constraints Priority

Итоговая иерархия:

```text
HARD CONSTRAINTS
        ↓
SAFETY POLICY
        ↓
PERMISSIONS
        ↓
PRIVACY POLICY
        ↓
USER HARD CONSTRAINTS
        ↓
SCORING
        ↓
EXPERIENCE / OPTIMIZATION
```

---

# 28. Deterministic Tie-Breaking

Если две стратегии имеют почти одинаковый score:

```text
A = 0.901
B = 0.900
```

не следует случайно выбирать одну.

Предлагается deterministic tie-breaking:

```text
1. higher verification
2. lower effective risk
3. higher reliability
4. higher privacy
5. lower user friction
6. lower resource cost
7. stable strategy priority
8. lexical strategy_id
```

Последний пункт гарантирует детерминизм.

---

# 29. Score Threshold

Нужен минимальный допустимый score.

Например:

```text
MIN_ACCEPTABLE_SCORE = 0.60
```

Если:

```text
best_score < threshold
```

агент не должен автоматически выполнять стратегию.

Возможные результаты:

```text
REQUIRES_USER
NO_SAFE_STRATEGY
GOAL_UNACHIEVABLE
```

Порог является policy/version parameter.

---

# 30. Confidence vs Score

Score и confidence — разные величины.

### Score

Насколько стратегия выглядит лучшей среди кандидатов.

### Confidence

Насколько агент уверен, что оценка корректна.

Пример:

```text
Strategy A
score = 0.91
confidence = 0.55
```

Причина:

```text
мало опыта
неполная информация о provider
```

В таком случае может потребоваться:

```text
verification усилить
```

или:

```text
user confirmation
```

---

# 31. Decision Margin

Важно учитывать разницу между первым и вторым местом.

```text
A = 0.91
B = 0.90
```

не то же самое, что:

```text
A = 0.91
B = 0.61
```

Вводится:

```text
DecisionMargin = Score1 - Score2
```

Малый margin означает:

```text
choice uncertain
```

Большой margin:

```text
choice clear
```

---

# 32. Low Margin Policy

Если:

```text
DecisionMargin < threshold
```

в зависимости от риска:

```text
R0/R1
→ choose deterministic winner

R2
→ stronger verification

R3
→ confirmation may be required

R4
→ policy-dependent / likely blocked
```

---

# 33. Experience Cannot Dominate Safety

Даже:

```text
success_rate = 99.9%
```

не позволяет стратегии обойти:

```text
permission
safety
privacy
policy
```

Experience является optimization signal, не authority.

---

# 34. Central Knowledge Cannot Dominate Local Policy

Центральный сервер может сообщить:

```text
"Strategy X is recommended."
```

Но устройство выполняет:

```text
local validation
local safety
local permissions
local scoring
```

Таким образом:

> **Remote knowledge is advisory. Local policy is authoritative.**

---

# 35. LLM Cannot Dominate Selection

LLM может предложить:

```text
Strategy A
Strategy B
```

но окончательный выбор:

```text
Registry
+
Constraints
+
Safety
+
Scoring
```

LLM не может:

```text
invent capability
invent permission
invent provider availability
```

без валидации.

---

# 36. Candidate Evaluation Pipeline

Полный алгоритм:

```text
1. Identify capability
2. Discover registered strategies
3. Build candidates
4. Check capability compatibility
5. Check provider availability
6. Check permissions
7. Check platform compatibility
8. Check network
9. Apply privacy policy
10. Apply safety policy
11. Apply user hard constraints
12. Eliminate invalid candidates
13. Calculate normalized factors
14. Apply context profile
15. Calculate score
16. Calculate confidence
17. Calculate decision margin
18. Rank
19. Select
20. Create Decision Record
```

---

# 37. Pseudocode

```text
function select_strategy(goal, capability, context):

    candidates = discover(capability)

    valid = []

    for candidate in candidates:

        if not hard_constraints_pass(candidate, context):
            record_rejection(candidate)
            continue

        if not safety_policy_allows(candidate, context):
            record_rejection(candidate)
            continue

        factors = evaluate(candidate, context)

        score = calculate_score(factors, context.profile)

        confidence = calculate_confidence(factors)

        valid.append(
            candidate,
            score,
            confidence
        )

    if valid is empty:
        return NO_SAFE_STRATEGY

    ranked = deterministic_sort(valid)

    best = ranked[0]

    if best.score < minimum_threshold:
        return REQUIRES_USER

    if requires_confirmation(best, context):
        return CONFIRMATION_REQUIRED

    return best
```

---

# 38. Strategy Decision Record

Каждый выбор должен оставлять структурированный record.

Минимум:

```text
decision_id
task_id
capability_id
policy_version
scoring_version
context_profile
candidates
eliminated_candidates
scores
selected_strategy
selected_provider
confidence
decision_margin
reason_summary
timestamp
```

---

# 39. Example

Goal:

```text
"Создай встречу завтра в 15:00."
```

Capability:

```text
calendar.create_event
```

Candidates:

```text
A native
B AppFunction
C UI automation
```

Hard filtering:

```text
A valid
B valid
C valid
```

Factors:

```text
A:
reliability 0.96
verification 1.00
experience 0.92
privacy 1.00
risk penalty low

B:
reliability 0.90
verification 0.90
experience 0.62
privacy 0.90

C:
reliability 0.82
verification 0.60
experience 0.88
privacy 0.95
risk penalty high
```

Possible result:

```text
A = 0.94
B = 0.82
C = 0.56
```

Select:

```text
A
```

---

# 40. Failure and Re-selection

Suppose A returns:

```text
PROVIDER_ERROR
```

Не следует просто:

```text
B immediately
```

Сначала:

```text
classify failure
update environment
re-check candidates
```

Затем:

```text
B
```

может получить новый score.

Например:

```text
A = eliminated
B = 0.84
C = 0.56
```

B selected.

---

# 41. Unknown Result

Если:

```text
A → UNKNOWN
```

то перед fallback:

```text
verification search
```

Например:

```text
Was event actually created?
```

Если:

```text
YES
```

goal achieved.

Если:

```text
NO
```

можно переходить к следующей стратегии.

Если:

```text
UNKNOWN
```

нельзя бездумно повторять действие.

---

# 42. Anti-loop Protection

Agent должен предотвращать:

```text
Strategy A
 → fail
Strategy B
 → fail
Strategy A
 → retry
Strategy B
 → retry
...
```

Для одной задачи вводятся:

```text
attempt budget
strategy attempt history
failure fingerprints
```

Если стратегия уже доказала невозможность в текущем контексте:

```text
temporarily exclude
```

---

# 43. Strategy Attempt Budget

Например:

```text
max_attempts_per_strategy = 1
```

для потенциально опасных действий.

Для безопасных transient операций:

```text
max_retries = 2
```

Параметры зависят от capability и policy.

---

# 44. Goal-level Budget

Также может существовать:

```text
GoalExecutionBudget
```

который ограничивает:

- количество стратегий;
- время;
- network requests;
- user confirmations;
- resource usage;
- total retries.

Это предотвращает бесконечную автономность.

---

# 45. Strategy Selection and STOP

STOP имеет абсолютный приоритет над текущим execution cycle.

После STOP:

```text
old candidates
old plan
old retries
```

не могут автоматически продолжить выполнение.

Новая задача должна получить новый:

```text
task_id
```

или explicit resume state.

---

# 46. Re-planning Trigger Conditions

Re-planning выполняется при:

```text
strategy failure
provider unavailable
permission change
environment change
user constraint change
verification failure
timeout
network change
application state change
```

Не требуется полный re-plan при каждой мелкой внутренней ошибке.

---

# 47. Stability

Чтобы стратегия не переключалась из-за незначительных колебаний score:

```text
score hysteresis
```

может применяться.

Например:

```text
current strategy = A
new strategy B better by only 0.01
```

не обязательно немедленно переключаться.

Это предотвращает:

```text
A → B → A → B
```

---

# 48. Versioned Scoring

Scoring Model должна иметь:

```text
scoring_version = "0.1"
```

Decision Record сохраняет эту версию.

Если веса изменились:

```text
0.1 → 0.2
```

старые решения остаются объяснимыми.

---

# 49. Testing Requirements

Scoring Model должна тестироваться отдельно.

Минимальные тесты:

```text
TC-01 unavailable strategy eliminated
TC-02 missing permission eliminated
TC-03 blocked by policy eliminated
TC-04 highest valid score selected
TC-05 verification beats speed when configured
TC-06 experience influences score
TC-07 experience cannot bypass safety
TC-08 remote recommendation cannot bypass local policy
TC-09 deterministic tie-breaking
TC-10 low score requires user / failure
TC-11 low decision margin handled correctly
TC-12 UNKNOWN prevents unsafe retry
TC-13 STOP prevents stale strategy execution
TC-14 strategy failure triggers re-evaluation
TC-15 anti-loop protection
```

---

# 50. Observability

Для диагностики должны быть доступны:

```text
candidate count
eliminated count
elimination reasons
factor values
score
confidence
decision margin
selected strategy
fallback count
verification result
```

Но observability не должна автоматически раскрывать пользователю внутреннюю chain-of-thought.

Пользователю можно показывать:

```text
"Выбран локальный способ: он доступен, безопаснее альтернатив и позволяет проверить результат."
```

Вместо внутренних рассуждений модели.

---

# 51. Explainability

Strategy decision должна быть объяснима на уровне фактов:

```text
Selected:
Native Calendar Provider

Why:
- available
- permission granted
- local
- low latency
- result can be independently verified

Alternative rejected:
UI automation
- higher effective risk
- weaker verification
```

Это соответствует принципу:

> **Result Transparency.**

---

# 52. MVP Simplification

В первой реализации не требуется:

- reinforcement learning;
- neural strategy optimizer;
- complex Bayesian model;
- online autonomous weight modification;
- genetic optimization;
- uncontrolled exploration.

Достаточно:

```text
hard filtering
+
deterministic scoring
+
experience modifier
+
verification
+
fallback
+
decision record
```

---

# 53. Эволюция модели

В будущем scoring может стать более интеллектуальным:

```text
v0.1
deterministic weighted scoring

v0.2
context-aware profiles

v0.3
statistical reliability model

v0.4
learned ranking

v1.x
hybrid learned + policy-constrained optimizer
```

Но Safety boundary должна оставаться детерминированной.

---

# 54. Архитектурный инвариант

Главный инвариант:

> **Optimization may improve execution, but it cannot grant authority.**

Иными словами:

```text
Optimization
    ↓
selects among allowed paths
```

а не:

```text
Optimization
    ↓
decides what is allowed
```

---

# 55. Итоговая архитектура выбора

```text
                 USER GOAL
                     ↓
                CAPABILITY
                     ↓
             STRATEGY DISCOVERY
                     ↓
              CANDIDATE SET
                     ↓
        ┌────────────────────────┐
        │ HARD CONSTRAINTS       │
        │ PERMISSIONS            │
        │ SAFETY                 │
        │ PRIVACY                │
        │ COMPATIBILITY          │
        └────────────────────────┘
                     ↓
              VALID CANDIDATES
                     ↓
        ┌────────────────────────┐
        │ SCORING                │
        │ RELIABILITY            │
        │ VERIFICATION           │
        │ EXPERIENCE             │
        │ PRIVACY                │
        │ LATENCY                │
        │ RESOURCE               │
        │ USER FRICTION          │
        │ RECOVERY               │
        └────────────────────────┘
                     ↓
                  RANKING
                     ↓
              BEST CANDIDATE
                     ↓
             DECISION RECORD
                     ↓
                EXECUTION
                     ↓
               VERIFICATION
                     ↓
          ┌──────────┴──────────┐
        SUCCESS              FAILURE
          ↓                     ↓
       RESULT                RE-PLAN
                                ↓
                         NEXT STRATEGY
```

---

# 56. Архитектурный вывод

Теперь агент получает полноценную модель:

```text
Goal
  ↓
Capability
  ↓
Multiple Strategies
  ↓
Hard Safety Boundary
  ↓
Deterministic Optimization
  ↓
Best Available Path
  ↓
Execution
  ↓
Verification
  ↓
Result / Re-plan
```

Это уже позволяет строить не просто tool-calling систему, а агентную систему, способную искать и сравнивать пути достижения одной цели.

---

# 57. Следующий архитектурный шаг

После этого документа логично перейти к:

## Execution Contract v0.1

Он определит:

- что именно происходит после выбора стратегии;
- `Task / Execution / Attempt`;
- lifecycle выполнения;
- cancellation;
- timeout;
- retries;
- side effects;
- execution state;
- partial success;
- unknown state;
- handoff;
- связь Execution с Verification;
- связь Execution с STOP Controller.

После него цепочка будет выглядеть уже почти полностью:

```text
Goal
→ Capability
→ Strategy
→ Selection
→ Execution
→ Verification
→ Result
```

---

# 58. Статус

**Strategy Selection & Scoring Model v0.1 — сформирована.**

**Код не изменялся.**

**Следующий этап: Execution Contract v0.1.**
