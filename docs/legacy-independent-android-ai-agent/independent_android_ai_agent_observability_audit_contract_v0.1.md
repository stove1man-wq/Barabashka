# Independent Android AI Agent
## Observability & Audit Contract v0.1

**Версия:** 0.1  
**Статус:** Архитектурный контракт  
**Назначение:** формализация наблюдаемости, аудита, диагностики и объяснимости Agent без сохранения внутренней цепочки рассуждений модели.

---

# 1. Назначение

Observability & Audit Layer должен позволять ответить на вопросы:

- что Agent пытался сделать;
- какую Capability использовал;
- какую Strategy выбрал;
- почему стратегия была выбрана;
- какие Safety/Privacy ограничения применялись;
- что реально было выполнено;
- что вернул provider;
- что показала Verification;
- почему произошёл re-plan;
- почему задача завершилась SUCCESS / PARTIAL / FAILED / BLOCKED / UNKNOWN;
- какие ошибки произошли;
- можно ли воспроизвести принятие архитектурного решения.

При этом система **не должна превращать Audit в хранилище приватной жизни пользователя** и не должна сохранять скрытую chain-of-thought модельного ответа.

Главный принцип:

> **Мы должны уметь объяснить действие Agent, не сохраняя внутренние рассуждения модели.**

---

# 2. Observability ≠ Audit

Это два связанных, но разных понятия.

## Observability

Отвечает:

> «Что происходит с системой сейчас и где проблема?»

Использует:

- metrics;
- logs;
- traces;
- health state;
- execution state;
- latency;
- error rates.

## Audit

Отвечает:

> «Что произошло и почему было принято конкретное решение?»

Использует:

- Decision Records;
- Task History;
- Safety Decisions;
- Data Sharing Decisions;
- Execution Records;
- Verification Records;
- Re-plan Records.

---

# 3. Архитектура

```text
                    ┌──────────────────┐
                    │    Agent Core    │
                    └────────┬─────────┘
                             │
          ┌──────────────────┼──────────────────┐
          ↓                  ↓                  ↓
      Metrics             Logs              Events
          │                  │                  │
          └──────────────────┼──────────────────┘
                             ↓
                    Observability Layer
                             │
                             ↓
                       Audit Recorder
                             │
              ┌──────────────┼──────────────┐
              ↓              ↓              ↓
          Task History   Decision Records   Diagnostics
```

Observability не должна напрямую управлять Execution.

---

# 4. Основные принципы

1. Structured over free-form.
2. Correlation over isolated logs.
3. Event-based task history.
4. Minimal data collection.
5. Privacy by default.
6. No chain-of-thought storage.
7. Deterministic decision records.
8. Versioned records.
9. Monotonic task lifecycle where possible.
10. Audit data не является источником authority.
11. Observability не должна изменять поведение Agent.
12. Sensitive data должны быть redacted.
13. Metrics должны быть агрегируемыми.
14. Logs должны иметь уровни важности.
15. Local diagnostics должны работать offline.

---

# 5. Correlation Model

Каждое значимое событие должно быть связано с контекстом.

Минимально:

```text
event_id
task_id
goal_id
plan_version
execution_id
timestamp
event_type
```

При необходимости:

```text
strategy_id
provider_id
capability_id
verification_id
decision_id
request_id
```

Это позволяет построить:

```text
Goal
 ↓
Task
 ↓
Plan
 ↓
Execution
 ↓
Verification
 ↓
Result
```

---

# 6. Event Model

Концептуально:

```text
AgentEvent
 ├─ event_id
 ├─ event_type
 ├─ timestamp
 ├─ task_id
 ├─ goal_id
 ├─ plan_version
 ├─ execution_id
 ├─ source
 ├─ severity
 ├─ status
 ├─ payload
 ├─ privacy_class
 └─ schema_version
```

`payload` должен быть структурированным.

---

# 7. Event Types

Минимальный набор:

```text
TASK_CREATED
TASK_UPDATED
PLAN_CREATED
PLAN_INVALIDATED
STRATEGY_SELECTED
SAFETY_DECISION
PERMISSION_STATE_CHANGED
CONFIRMATION_REQUESTED
CONFIRMATION_RECEIVED
EXECUTION_STARTED
EXECUTION_PROGRESS
EXECUTION_COMPLETED
EXECUTION_FAILED
EXECUTION_TIMEOUT
EXECUTION_CANCELLED
VERIFICATION_STARTED
VERIFICATION_COMPLETED
REPLAN_STARTED
REPLAN_COMPLETED
RESULT_FINALIZED
TASK_CANCELLED
TASK_EXPIRED
MODEL_REQUESTED
MODEL_COMPLETED
MODEL_FAILED
DATA_SHARED
DATA_DELETED
SYSTEM_ERROR
```

---

# 8. Event Severity

Рекомендуемые уровни:

```text
DEBUG
INFO
NOTICE
WARNING
ERROR
CRITICAL
```

Примеры:

```text
DEBUG
→ internal timing information

INFO
→ execution started

NOTICE
→ strategy fallback selected

WARNING
→ provider temporarily unavailable

ERROR
→ execution failed

CRITICAL
→ safety subsystem failure
```

---

# 9. Structured Logs

Нельзя строить диагностику только на строках:

```text
"Something went wrong"
```

Вместо этого:

```json
{
  "event_type": "EXECUTION_FAILED",
  "task_id": "task-123",
  "execution_id": "exec-4",
  "error_code": "PROVIDER_UNAVAILABLE",
  "retryable": true
}
```

Это позволяет:

- фильтровать;
- агрегировать;
- анализировать;
- тестировать;
- строить UI диагностики.

---

# 10. Error Model

Ошибки должны быть структурированными.

```text
AgentError
 ├─ error_code
 ├─ category
 ├─ severity
 ├─ retryable
 ├─ user_action_required
 ├─ technical_details
 ├─ task_id
 ├─ execution_id
 └─ timestamp
```

Категории:

```text
INPUT
CAPABILITY
STRATEGY
PERMISSION
SAFETY
PRIVACY
MODEL
PROVIDER
NETWORK
EXECUTION
VERIFICATION
STORAGE
SYSTEM
UNKNOWN
```

---

# 11. Error Code Principles

Error code должен описывать причину, а не UI-текст.

Хорошо:

```text
PERMISSION_DENIED
MODEL_TIMEOUT
VERIFICATION_CONFLICT
PROVIDER_UNAVAILABLE
STALE_PLAN
PRIVACY_POLICY_BLOCKED
```

Плохо:

```text
"Oops"
"Something went wrong"
"Try again"
```

UI может преобразовывать code в понятное пользователю сообщение.

---

# 12. Decision Records

Значимые архитектурные решения должны иметь отдельные records.

Например:

```text
StrategyDecisionRecord
SafetyDecisionRecord
DataSharingDecisionRecord
ModelSelectionDecisionRecord
VerificationDecisionRecord
ReplanDecisionRecord
```

Каждый record должен ссылаться на:

```text
task_id
plan_version
policy_version
relevant contract versions
```

---

# 13. Decision Record не является Chain-of-Thought

Нужно различать:

```text
Decision:
Strategy B selected
Reason:
higher verification + lower privacy risk
```

и:

```text
полный внутренний текст рассуждений модели
```

Первое допустимо и полезно.

Второе не требуется для архитектурной объяснимости.

Правило:

> **Store decision facts and reason codes, not hidden reasoning traces.**

---

# 14. Explainability Model

Agent должен уметь объяснить решение на нескольких уровнях.

## Level 0 — Minimal

```text
«Готово»
```

## Level 1 — User Explanation

```text
«Я создал событие в календаре и проверил, что оно появилось.»
```

## Level 2 — Decision Explanation

```text
«Выбран официальный Calendar Provider,
потому что он доступен на устройстве и позволяет проверить результат напрямую.»
```

## Level 3 — Technical Diagnostics

```text
strategy_id
provider_id
policy_version
verification_level
error_code
timings
```

Пользователь не должен получать Level 3 автоматически, если он ему не нужен.

---

# 15. Task History

История должна представлять задачу как жизненный цикл:

```text
Goal
 ↓
Plan v1
 ↓
Safety Decision
 ↓
Execution
 ↓
Verification
 ↓
Failure
 ↓
Plan v2
 ↓
Execution
 ↓
Verification
 ↓
Final Result
```

Это лучше, чем обычная:

```text
Chat transcript
```

---

# 16. Task History не должна быть копией всего диалога

История задачи должна хранить:

- Goal;
- важные user constraints;
- планы;
- выбранные стратегии;
- execution states;
- verification;
- re-planning;
- final result.

Не требуется автоматически хранить:

- полный chain-of-thought;
- все промежуточные model tokens;
- весь web content;
- весь screen content;
- все доступные данные устройства.

---

# 17. Privacy-aware Audit

Audit сам является потенциально чувствительным источником данных.

Поэтому:

```text
Audit Data
    ↓
Classification
    ↓
Redaction
    ↓
Retention Policy
    ↓
Storage
```

Audit не должен обходить Data & Privacy Contract.

---

# 18. Redaction

Sensitive значения должны заменяться:

```text
[REDACTED]
```

или структурированным digest/reference.

Примеры:

```text
password → [REDACTED]
token → [REDACTED]
private_key → [REDACTED]
full_message → [REDACTED]
```

Для диагностики иногда достаточно:

```text
hash
length
type
classification
```

---

# 19. Hashes вместо Raw Data

Если нужно доказать идентичность данных, предпочтительнее хранить:

```text
sha256(data)
```

вместо самих данных, когда raw content не нужен.

Это полезно для:

- artifact verification;
- deterministic decision records;
- integrity checks;
- duplicate detection.

Hash не должен рассматриваться как абсолютная анонимизация.

---

# 20. Metrics

Минимальные метрики:

### Task

```text
task_success_rate
task_failure_rate
task_cancel_rate
task_unknown_rate
task_duration
```

### Strategy

```text
strategy_success_rate
strategy_failure_rate
fallback_rate
replan_rate
```

### Execution

```text
execution_duration
timeout_rate
provider_error_rate
retry_rate
```

### Verification

```text
verification_success_rate
verification_conflict_rate
verification_unknown_rate
```

### Model

```text
model_latency
model_failure_rate
schema_error_rate
fallback_rate
```

### Safety / Privacy

```text
safety_block_rate
permission_wait_rate
confirmation_rate
privacy_block_rate
```

---

# 21. Metrics должны быть privacy-safe

Например:

```text
strategy_success_rate = 94%
```

не требует:

```text
все пользовательские сообщения
```

Метрики должны по возможности агрегироваться.

---

# 22. Tracing

Для одной задачи можно построить trace:

```text
Task
 ├─ Goal Understanding
 ├─ Strategy Selection
 ├─ Safety
 ├─ Model
 ├─ Execution
 └─ Verification
```

Каждая операция может иметь:

```text
span_id
parent_span_id
start
end
status
```

Это особенно полезно для поиска:

- задержек;
- повторных вызовов;
- зависших операций;
- неправильного re-planning;
- provider bottlenecks.

---

# 23. Local-first Observability

Основные диагностические возможности должны работать без интернета.

Например:

```text
No Network
   ↓
Task executes locally
   ↓
Local metrics
   ↓
Local audit
   ↓
Local diagnostics
```

Синхронизация внешней telemetry, если она вообще включена, является отдельной функцией.

---

# 24. Telemetry Policy

Telemetry должна быть:

```text
OPTIONAL
MINIMAL
CONFIGURABLE
PRIVACY-AWARE
```

Пользователь должен иметь возможность:

- отключить необязательную telemetry;
- выбрать privacy profile;
- удалить локальные diagnostic records, где это допустимо.

Критические локальные security/audit records могут иметь отдельные правила хранения.

---

# 25. Audit vs Telemetry

Нельзя смешивать:

```text
Audit
```

и:

```text
Telemetry
```

Audit:

> «Почему конкретная задача была разрешена?»

Telemetry:

> «Как часто такая задача вообще выполняется?»

Audit обычно task-specific.

Telemetry преимущественно aggregate/system-level.

---

# 26. Model Observability

Для Model Adapter допустимо записывать:

```text
model_id
provider_id
request_id
latency
status
schema_valid
token counts where available
error_code
fallback_used
```

Не следует по умолчанию сохранять:

```text
full prompt
full model response
private context
raw sensitive evidence
```

если это не требуется конкретной диагностикой и не разрешено privacy policy.

---

# 27. Model Output Diagnostics

Если модель вернула некорректный structured output:

```text
MODEL_COMPLETED
 ↓
Schema Validation
 ↓
INVALID_OUTPUT
```

Audit должен фиксировать:

```text
model_id
schema_version
error_code
validation_result
```

а не обязательно весь исходный ответ модели.

---

# 28. Execution Observability

Для каждого execution:

```text
ExecutionRecord
 ├─ execution_id
 ├─ task_id
 ├─ capability_id
 ├─ strategy_id
 ├─ provider_id
 ├─ start_time
 ├─ end_time
 ├─ status
 ├─ timeout
 ├─ cancellation_state
 ├─ side_effect_summary
 └─ error_code
```

---

# 29. Side Effects

Нужно фиксировать факт и тип побочных эффектов:

```text
SideEffectRecord
 ├─ type
 ├─ expected
 ├─ observed
 ├─ reversible
 └─ verification_state
```

Например:

```text
calendar_event_created
expected = true
observed = true
verified = true
```

Не обязательно хранить всё содержимое события в Audit.

---

# 30. Verification Observability

Verification Record:

```text
VerificationRecord
 ├─ verification_id
 ├─ execution_id
 ├─ level
 ├─ expected_state
 ├─ evidence_type
 ├─ result
 ├─ confidence
 ├─ conflict
 ├─ stale
 └─ timestamp
```

Особенно важно фиксировать:

```text
UNKNOWN
CONFLICT
PARTIALLY_VERIFIED
```

а не превращать их в обычный FAILED.

---

# 31. Re-planning Observability

Каждый re-plan должен иметь:

```text
ReplanRecord
 ├─ replan_id
 ├─ task_id
 ├─ previous_plan_version
 ├─ new_plan_version
 ├─ trigger
 ├─ preserved_progress
 ├─ discarded_actions
 ├─ selected_strategy
 └─ timestamp
```

Trigger examples:

```text
EXECUTION_FAILED
VERIFICATION_FAILED
UNKNOWN_RESULT
PROVIDER_UNAVAILABLE
PERMISSION_CHANGED
USER_CONSTRAINT_CHANGED
TIMEOUT
ENVIRONMENT_CHANGED
```

---

# 32. Audit Integrity

Для критических records желательно иметь integrity mechanism.

Например:

```text
record
 ↓
canonical serialization
 ↓
hash
```

Для release/critical audit можно дополнительно:

```text
hash
 ↓
signature
```

Это позволяет обнаружить изменение записи.

Но:

> Audit integrity не означает, что audit сам является источником authority.

---

# 33. Event Ordering

Система должна учитывать, что события могут приходить асинхронно.

Для восстановления порядка используются:

```text
timestamp
sequence_number
parent_event_id
task_id
plan_version
execution_id
```

Нельзя полагаться только на wall-clock timestamp.

---

# 34. Idempotent Event Recording

Повторная доставка события не должна создавать ложное состояние.

Например:

```text
EXECUTION_COMPLETED
event_id = X
```

если пришёл повторно, система должна распознать duplicate.

Для этого:

```text
event_id
idempotency_key
sequence_number
```

могут использоваться для deduplication.

---

# 35. Crash Recovery

После перезапуска:

```text
Load Task
 ↓
Load latest valid plan
 ↓
Load execution state
 ↓
Load verification state
 ↓
Identify incomplete operation
 ↓
Reconcile
 ↓
Resume / Re-plan / Stop
```

Audit должен позволять понять:

> что Agent успел сделать до crash.

---

# 36. User-visible Activity

Пользователю можно показывать:

```text
Создаю событие…
Проверяю календарь…
Готово.
```

Но внутренние технические детали:

```text
strategy score
provider adapter
span id
model latency
```

должны быть скрыты в обычном режиме.

При необходимости:

```text
Show details
```

может открыть техническое объяснение.

---

# 37. Observability Levels

Рекомендуемые режимы:

```text
USER
NORMAL
DEVELOPER
DIAGNOSTIC
```

## USER

Только понятный статус и результат.

## NORMAL

Стандартные task events и ошибки.

## DEVELOPER

Расширенные decision/execution details.

## DIAGNOSTIC

Полные технические traces, доступные без sensitive raw data.

---

# 38. Debug Mode

Debug mode не должен автоматически отключать privacy.

Неправильно:

```text
DEBUG = log everything
```

Правильно:

```text
DEBUG
 +
same privacy policy
 +
additional structured metadata
```

---

# 39. Failure Explanation

Agent должен объяснять ошибки через причинную цепочку.

Например:

```text
Goal:
создать событие

Failure:
Calendar permission denied

Result:
не выполнено

Next action:
предложить открыть настройки разрешений
```

а не:

```text
«Что-то пошло не так»
```

---

# 40. Success Explanation

Успех должен опираться на Verification.

Хорошо:

```text
«Событие создано. Я проверил, что оно появилось в календаре.»
```

Не следует говорить:

```text
«Готово»
```

если выполнение только вернуло SUCCESS от provider, но независимая проверка ещё не выполнена для требуемого уровня риска.

---

# 41. Audit Query API

Концептуально:

```text
AuditService
 ├─ getTaskHistory(task_id)
 ├─ getDecisionRecord(decision_id)
 ├─ getExecution(execution_id)
 ├─ getVerification(verification_id)
 ├─ getReplanHistory(task_id)
 ├─ getErrors(task_id)
 ├─ searchEvents(filter)
 └─ exportDiagnosticBundle(task_id)
```

---

# 42. Diagnostic Bundle

Для сложной диагностики можно сформировать:

```text
DiagnosticBundle
 ├─ task metadata
 ├─ plan versions
 ├─ decision records
 ├─ execution records
 ├─ verification records
 ├─ replan records
 ├─ error summary
 ├─ metrics
 └─ environment summary
```

Перед экспортом:

```text
Diagnostic Bundle
        ↓
Privacy Redaction
        ↓
User Review
        ↓
Export
```

Нельзя автоматически отправлять bundle внешнему серверу.

---

# 43. Environment Summary

Для диагностики допустимо хранить ограниченный набор:

```text
app_version
agent_version
android_version
device_capability_summary
active_policy_versions
model_id
provider_id
```

Не требуется сохранять:

```text
all installed apps
all contacts
all files
full location history
full user activity
```

---

# 44. Audit Retention

Audit должен иметь собственную retention policy.

Например:

```text
Active Task
→ retain

Recently completed
→ retain limited period

Old task
→ compact / delete according to policy

Critical security record
→ separate retention policy
```

Retention не должен быть бесконечным по умолчанию.

---

# 45. Privacy и Audit

Важный принцип:

> **Auditability не оправдывает сбор лишних персональных данных.**

Если достаточно:

```text
event_type
strategy_id
result
error_code
```

не нужно сохранять:

```text
full user message
```

---

# 46. Security Invariants

В MVP должны выполняться:

1. Audit не хранит chain-of-thought.
2. Logs не содержат secrets.
3. Sensitive data redacted по умолчанию.
4. Audit не может менять Execution.
5. Observability не может обходить Safety.
6. Metrics не требуют raw user content.
7. Decision Records структурированы и versioned.
8. Task History связывает Goal → Plan → Execution → Verification → Result.
9. Unknown и Conflict сохраняются как отдельные состояния.
10. Event duplication не ломает состояние.
11. Crash recovery использует audit/task state.
12. Debug mode не отключает Privacy Policy.
13. Diagnostic export проходит privacy redaction.
14. Audit retention ограничен policy.
15. Audit records могут иметь integrity protection.
16. Model diagnostics не требуют хранения полного prompt/response.
17. User-facing explanations не должны выдавать ложную степень уверенности.
18. Observability не является источником authorization.

---

# 47. MVP Scope

В MVP обязательно:

- structured event model;
- task correlation;
- structured error model;
- Decision Records;
- Task History;
- Execution Records;
- Verification Records;
- Re-plan Records;
- metrics;
- privacy-aware logging;
- redaction;
- local-first diagnostics;
- crash recovery metadata;
- user-facing progress;
- diagnostic export;
- audit retention;
- integrity hashes для критических records.

В MVP не требуется:

- полноценный distributed tracing backend;
- удалённая централизованная telemetry;
- сложный SIEM;
- ML anomaly detection;
- автоматическое расследование инцидентов;
- сложная observability cloud platform.

---

# 48. Вертикальный срез

Для `calendar.create_event`:

```text
TASK_CREATED
      ↓
PLAN_CREATED
      ↓
STRATEGY_SELECTED
      ↓
SAFETY_DECISION
      ↓
EXECUTION_STARTED
      ↓
EXECUTION_COMPLETED
      ↓
VERIFICATION_STARTED
      ↓
VERIFICATION_COMPLETED
      ↓
RESULT_FINALIZED
```

Если стратегия не сработала:

```text
EXECUTION_FAILED
      ↓
REPLAN_STARTED
      ↓
PLAN v2
      ↓
STRATEGY_SELECTED
      ↓
...
```

В конце должна существовать единая связная история Task.

---

# 49. Связь с другими контрактами

Полная архитектурная цепочка:

```text
Goal
  ↓
Task State
  ↓
Capability
  ↓
Strategy
  ↓
Strategy Selection
  ↓
Data & Privacy
  ↓
Safety & Permission
  ↓
Execution
  ↓
Verification
  ↓
Result
  ↓
Task History / Audit
```

Observability находится поперёк всей цепочки:

```text
                 Observability
                      │
Goal ─ Task ─ Plan ─ Execution ─ Verification
                      │
                    Audit
```

---

# 50. Главный архитектурный принцип

> **Если Agent невозможно диагностировать по структурированным фактам, архитектура недостаточно наблюдаема.**

Но:

> **Наблюдаемость не должна превращаться в тотальную запись пользовательской жизни.**

И итоговая формула:

```text
Observe enough to understand the system.
Audit enough to explain decisions.
Store only what is necessary.
Never store hidden reasoning as a substitute for architecture.
```

---

# 51. Итог

Observability & Audit Contract превращает диагностируемость в самостоятельный архитектурный слой.

Он обеспечивает:

- воспроизводимость решений;
- связанную историю Task;
- объяснимость;
- структурированные ошибки;
- контроль re-planning;
- диагностику crash/failure;
- privacy-aware logging;
- безопасный diagnostic export;
- основу для будущего production monitoring.

При этом сохраняется фундаментальный принцип проекта:

> **Agent должен быть объяснимым по своим действиям и решениям, но не обязан раскрывать внутреннюю chain-of-thought модели.**
