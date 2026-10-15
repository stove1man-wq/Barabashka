# Independent Android AI Agent

> **Нормативный документ по политике безопасности (D-006, 2026-10-12).** Общие термины: `docs/architecture/GLOSSARY.md`. Базовый уровень риска capability задаёт `independent_android_ai_agent_capability_matrix_mvp_v0.1.md`; примеры в этом документе иллюстративны (в частности, `message.send` — R3, `reminder.create` — R2).

## Safety & Permission Contract v0.1

**Версия:** 0.1  
**Статус:** Архитектурный контракт  
**Назначение:** формализация локальной политики безопасности, Android-разрешений, подтверждений пользователя и границ автономности Agent Core.

---

## 1. Назначение

Safety & Permission Contract определяет правила, по которым Agent решает:

- разрешено ли действие;
- требуется ли Android permission;
- требуется ли подтверждение пользователя;
- может ли действие выполняться автономно;
- когда действие должно быть отклонено;
- какие внешние рекомендации могут быть приняты только после локальной проверки;
- как обрабатываются STOP, отмена и изменение разрешений;
- как фиксируется принятое решение.

Главный принцип:

> **Ни модель, ни стратегия, ни центральная сеть знаний не получают право на действие. Право определяется локальной политикой безопасности и текущим состоянием устройства.**

---

# 2. Базовая модель безопасности

Архитектурный поток:

```text
Goal
  ↓
Capability
  ↓
Candidate Strategy
  ↓
Safety & Permission Evaluation
  ↓
 ┌───────────────────────────────┐
 │ ALLOW                         │
 │ ALLOW_WITH_CONFIRMATION      │
 │ WAIT_FOR_PERMISSION          │
 │ WAIT_FOR_USER                │
 │ BLOCK                        │
 └───────────────────────────────┘
  ↓
Execution
  ↓
Verification
```

Никакая стратегия не может пропустить этап Safety & Permission Evaluation.

---

# 3. Принцип локального авторитета

Источники рекомендаций могут иметь разную степень доверия:

```text
Local Safety Policy          ← highest authority
        ↓
Android OS / granted state
        ↓
Current device state
        ↓
Capability Contract
        ↓
User constraints/preferences
        ↓
Strategy evaluation
        ↓
Experience
        ↓
Central knowledge
        ↓
Model recommendation
```

Это не означает, что модель или центральная база всегда ненадёжны.

Это означает:

> **Информация может приходить извне, но решение о безопасности остаётся локальным.**

---

# 4. Что Safety Policy имеет право решать

Safety Policy может:

- разрешить действие;
- запретить действие;
- потребовать подтверждение;
- потребовать Android permission;
- потребовать пользователя;
- ограничить объём действия;
- ограничить количество повторов;
- потребовать более сильную верификацию;
- запретить конкретную стратегию;
- запретить передачу определённых данных;
- потребовать human-in-the-loop;
- остановить выполнение.

Safety Policy не должна:

- самостоятельно менять Goal пользователя;
- самостоятельно выдавать Android permissions;
- создавать фактические доказательства успеха;
- подменять Verification;
- превращать рекомендацию модели в разрешение;
- отключаться ради достижения более высокого Score.

---

# 5. Risk Levels

Используется согласованная шкала:

| Risk | Название | Примеры |
|---|---|---|
| R0 | Informational | поиск, чтение открытой информации |
| R1 | Low Impact | открытие приложения, создание локального напоминания |
| R2 | User-Impacting | создание/изменение календаря, отправка сообщения |
| R3 | Sensitive / Consequential | звонки, чувствительные данные, действия с существенными последствиями |
| R4 | Highly Sensitive | действия с потенциально критическими последствиями |

Risk является свойством Capability, но итоговый Effective Risk может зависеть от:

```text
Effective Risk =
    Capability Risk
  + Strategy Modifier
  + Context Modifier
  + Data Sensitivity Modifier
```

При этом Safety Policy может только повысить ограничения.

Она не должна понижать риск ради удобства.

---

# 6. Permission ≠ Authorization

Ключевое различие:

```text
Android Permission
        ≠
Agent Authorization
        ≠
User Confirmation
```

### Android Permission

Техническое разрешение ОС.

Например:

- доступ к календарю;
- доступ к контактам;
- доступ к микрофону;
- доступ к местоположению.

### Agent Authorization

Разрешение, определённое локальной политикой Agent.

Например:

> Пользователь ранее разрешил создавать календарные события автоматически.

### User Confirmation

Явное подтверждение конкретного действия.

Например:

> «Создать событие завтра в 15:00?»

Наличие Android permission не означает автоматическое разрешение действия.

И наоборот, подтверждение пользователя не может заменить отсутствующее Android permission.

---

# 7. Permission States

Рекомендуемые состояния:

```text
UNKNOWN
NOT_REQUIRED
NOT_GRANTED
GRANTED
DENIED
PERMANENTLY_DENIED
RESTRICTED
TEMPORARILY_UNAVAILABLE
REQUESTABLE
```

Safety Layer должен отличать:

- permission отсутствует;
- permission можно запросить;
- permission запрещено пользователем;
- permission запрещено политикой/ОС;
- permission технически недоступно;
- permission уже предоставлено.

---

# 8. Permission Evaluation

Перед выполнением Capability:

```text
1. Is permission required?
       ↓
2. Is it granted?
       ↓
3. If not granted:
       ├─ requestable → WAIT_FOR_PERMISSION
       ├─ permanently denied → WAIT_FOR_USER / BLOCK
       └─ restricted → BLOCK
```

Permission request является отдельным событием и не должен быть скрытым побочным эффектом Execution.

---

# 9. Confirmation Policy

Confirmation Policy определяет, когда требуется явное действие пользователя.

Базовая модель:

```text
Risk
 +
Data Sensitivity
 +
Side Effects
 +
Reversibility
 +
User Preference
 +
Context
        ↓
Confirmation Decision
```

Рекомендуемые уровни:

```text
NONE
OPTIONAL
REQUIRED
REQUIRED_EVERY_TIME
```

### NONE

Действие может выполняться автономно при выполнении остальных условий.

### OPTIONAL

Agent может выполнить действие автономно, если локальная политика и пользовательские настройки это допускают.

### REQUIRED

Перед конкретным действием требуется подтверждение.

### REQUIRED_EVERY_TIME

Подтверждение нельзя заменить долговременным разрешением.

---

# 10. Confirmation должен быть контекстным

Не следует превращать Agent в систему бесконечных диалогов:

```text
«Разрешить?»
«Разрешить?»
«Разрешить?»
```

Но и нельзя превращать одно старое подтверждение в бессрочный доступ ко всему.

Поэтому разрешение должно быть связано с:

```text
Goal
Capability
Scope
Risk
Context
Time
Task
```

Пример:

> «Разрешаю создать это событие»

не обязательно означает:

> «Разрешаю Agent создавать любые будущие события без подтверждения».

---

# 11. Sensitive Actions

Некоторые действия должны иметь повышенные требования независимо от удобства.

К ним потенциально относятся:

- отправка сообщений;
- телефонные звонки;
- передача чувствительных данных;
- изменение важных пользовательских данных;
- действия с финансовыми последствиями;
- публикация информации;
- предоставление доступа третьим лицам;
- действия, которые трудно или невозможно отменить.

Для таких действий Safety Policy может требовать:

```text
explicit confirmation
+
strong verification
+
restricted scope
```

---

# 12. Reversibility

При прочих равных предпочтение отдаётся обратимым действиям.

Модель:

```text
Reversible
    ↓
Less risky
    ↓
Easier autonomous execution
```

и:

```text
Irreversible / difficult to reverse
    ↓
Higher risk
    ↓
Stronger confirmation / verification
```

Важно:

> Возможность отмены действия не должна автоматически считаться гарантией rollback.

Rollback должен существовать только там, где он действительно поддерживается.

---

# 13. User Authority

Пользователь является высшим источником требований к собственным задачам, но не может нарушить фундаментальные ограничения ОС и безопасности.

Пользователь может:

- остановить Task;
- изменить Goal;
- изменить ограничения;
- дать подтверждение;
- отказаться от действия;
- предоставить требуемое permission через системный интерфейс;
- выбрать менее автономный режим.

Пользователь не может через обычное подтверждение:

- выдать отсутствующее Android permission;
- отключить обязательную системную защиту;
- заставить Agent игнорировать критическую локальную policy;
- превратить недопустимое действие в допустимое.

---

# 14. STOP как аварийный механизм

STOP является глобальным execution-control сигналом.

При STOP:

```text
STOP
 ↓
Block new actions
 ↓
Cancel active execution where possible
 ↓
Invalidate pending retries/fallbacks
 ↓
Record actual state
 ↓
Verify/reconcile if necessary
 ↓
Inform user
```

STOP должен иметь приоритет над:

```text
model output
strategy
experience
central knowledge
pending retry
fallback
optimization
```

STOP не означает автоматический rollback.

---

# 15. Safety Evaluation Order

Рекомендуемый порядок:

```text
1. Task is current?
2. STOP requested?
3. Capability allowed?
4. Strategy compatible?
5. Platform constraints satisfied?
6. Required permissions satisfied?
7. Privacy policy satisfied?
8. Data handling allowed?
9. Risk acceptable?
10. Confirmation required?
11. User confirmation available?
12. Runtime conditions valid?
13. Execution budget available?
14. Execute
```

Если критическое условие не выполнено, действие не должно выполняться.

---

# 16. Hard Safety Constraints

Следующие ограничения являются hard constraints:

- запрещённая Capability;
- запрещённая Strategy;
- отсутствующее обязательное permission;
- нарушение privacy policy;
- превышение допустимого risk;
- запрещённый тип данных;
- нарушение user constraint;
- STOP;
- устаревший Task/Plan;
- превышение execution budget;
- нарушение platform policy;
- недействительный authorization context.

Hard constraint не может быть компенсирован высоким Score.

---

# 17. Soft Safety Factors

Некоторые параметры могут участвовать в выборе стратегии как soft factors:

- дополнительный privacy risk;
- user friction;
- resource consumption;
- latency;
- количество confirmation steps;
- reliability;
- verification quality;
- reversibility;
- network dependency.

Но:

> Soft factor никогда не должен превращать запрещённую операцию в разрешённую.

---

# 18. Model Restrictions

Модель не имеет права:

```text
grant_permission()
authorize_action()
disable_safety()
override_policy()
confirm_for_user()
```

Модель может:

- интерпретировать Goal;
- предложить Capability;
- предложить Strategy;
- объяснить пользователю действие;
- предложить альтернативу;
- подготовить структурированный запрос.

После этого:

```text
Model
 ↓
Untrusted Structured Output
 ↓
Validation
 ↓
Safety Policy
 ↓
Execution
```

---

# 19. Experience Restrictions

Experience Store может рекомендовать:

> «Эта стратегия ранее хорошо работала».

Но не может утверждать:

> «Поэтому Safety Policy можно пропустить».

Experience не может:

- выдавать permission;
- заменять confirmation;
- снижать обязательный risk level;
- отменять STOP;
- заменять Verification.

---

# 20. Central Knowledge Restrictions

Central Knowledge может поставлять:

- новую информацию;
- информацию о доступных стратегиях;
- compatibility data;
- известные ограничения;
- рекомендации;
- security advisories.

Но не может непосредственно управлять устройством.

Правильная модель:

```text
Central Knowledge
        ↓
Signed / Versioned Advisory
        ↓
Local Validation
        ↓
Local Safety Policy
        ↓
Possible use
```

Неправильная модель:

```text
Central Server
      ↓
Execute on Device
```

---

# 21. Policy Versioning

Safety Policy должна быть версионируемой.

Минимально:

```text
policy_id
policy_version
effective_from
ruleset_version
compatibility
```

Каждое значимое Safety Decision должно ссылаться на конкретную версию policy.

Это необходимо для:

- аудита;
- воспроизводимости;
- диагностики;
- безопасного обновления;
- rollback policy;
- анализа конфликтов.

---

# 22. Safety Decision Record

Для значимых действий рекомендуется сохранять:

```text
SafetyDecisionRecord
 ├─ decision_id
 ├─ task_id
 ├─ execution_id
 ├─ capability_id
 ├─ strategy_id
 ├─ risk_level
 ├─ effective_risk
 ├─ required_permissions
 ├─ permission_state
 ├─ confirmation_policy
 ├─ confirmation_state
 ├─ privacy_class
 ├─ policy_version
 ├─ decision
 ├─ reason_codes
 └─ timestamp
```

Рекомендуемые решения:

```text
ALLOW
ALLOW_WITH_CONFIRMATION
WAIT_FOR_PERMISSION
WAIT_FOR_USER
BLOCK
```

---

# 23. Reason Codes

Причина решения должна быть структурированной.

Примеры:

```text
PERMISSION_GRANTED
PERMISSION_REQUIRED
PERMISSION_DENIED
CONFIRMATION_REQUIRED
CONFIRMATION_MISSING
RISK_TOO_HIGH
PRIVACY_RESTRICTED
CAPABILITY_BLOCKED
STRATEGY_BLOCKED
USER_CONSTRAINT_BLOCKED
TASK_CANCELLED
STALE_PLAN
EXECUTION_BUDGET_EXCEEDED
PLATFORM_RESTRICTION
POLICY_RESTRICTION
```

Не следует полагаться только на свободный текст.

---

# 24. Auditability

Audit должен позволять ответить:

1. Что Agent хотел сделать?
2. Какая Capability использовалась?
3. Какая Strategy была выбрана?
4. Какие permissions были нужны?
5. Какие permissions реально были?
6. Требовалось ли подтверждение?
7. Была ли политика соблюдена?
8. Почему действие было разрешено или заблокировано?
9. Какая версия policy использовалась?
10. Что фактически произошло?
11. Как был проверен результат?

Audit не должен хранить внутреннюю цепочку рассуждений модели.

Достаточно структурированных решений и причин.

---

# 25. Privacy by Architecture

Safety Layer должен учитывать не только действие, но и данные.

Минимальная классификация:

```text
PUBLIC
NON_SENSITIVE
PRIVATE
SENSITIVE
HIGHLY_SENSITIVE
```

При выборе Strategy:

```text
Data Sensitivity
        +
Provider Trust
        +
Network Requirement
        ↓
Privacy Decision
```

Например:

```text
HIGHLY_SENSITIVE data
+
Cloud strategy
+
No explicit user authorization
        ↓
BLOCK
```

---

# 26. Permission Request UX

Если требуется permission:

```text
Agent detects missing permission
        ↓
Explains why it is needed
        ↓
User chooses
        ↓
Android permission flow
        ↓
Re-check actual permission state
        ↓
Continue / Block
```

Нельзя считать:

> «Пользователь нажал продолжить»

эквивалентом:

> «Android permission предоставлено».

Фактическое состояние ОС всегда проверяется отдельно.

---

# 27. Runtime Re-check

Permission и Safety Decision могут устареть.

Поэтому непосредственно перед Execution рекомендуется повторно проверять критические условия:

```text
current Task
current Plan
STOP state
permission state
provider availability
privacy constraints
runtime environment
authorization context
```

Это защищает от ситуации:

```text
Policy allowed at T1
        ↓
Environment changed
        ↓
Execution at T2
```

---

# 28. Race Conditions

Особое внимание необходимо уделить параллельным событиям:

```text
User presses STOP
        +
Execution starts
```

или:

```text
Permission revoked
        +
Execution starts
```

или:

```text
Plan v2 created
        +
old Plan v1 resumes
```

Для этого используются:

- task_id;
- plan_version;
- execution_id;
- cancellation_token;
- authorization context;
- runtime state checks.

Только актуальный execution имеет право продолжать работу.

---

# 29. Safety и Re-planning

Если Safety Policy блокирует Strategy:

```text
Strategy A
   ↓
BLOCK
   ↓
Can another strategy satisfy policy?
   ├─ YES → Re-plan
   └─ NO  → BLOCK / NEED USER
```

Важно:

> Safety Block не означает автоматически Goal Failure.

Например:

```text
Strategy A → требует запрещённого доступа
Strategy B → работает через официальный API
```

В этом случае Agent должен попробовать допустимую Strategy B.

---

# 30. Safety и Verification

Safety отвечает:

> «Можно ли выполнить?»

Verification отвечает:

> «Действительно ли получилось?»

Они не должны смешиваться.

```text
Safety
  ↓
ALLOW
  ↓
Execution
  ↓
Verification
  ↓
Result
```

Даже полностью разрешённое действие может завершиться неуспешно.

---

# 31. Safety и Strategy Scoring

Scoring выполняется только после hard safety filtering.

Неправильно:

```text
Strategy A = forbidden
Score = 0.98
→ execute
```

Правильно:

```text
Strategy A = forbidden
→ eliminate

Strategy B = allowed
Score = 0.74
→ select
```

Главный инвариант:

> **Optimization cannot grant authority.**

---

# 32. Emergency / High-Risk Policy

Для R4 и других специально определённых действий может существовать отдельный policy profile:

```text
HIGH_ASSURANCE
```

Он может требовать:

- explicit confirmation;
- restricted scope;
- strong verification;
- multiple independent evidence sources;
- no autonomous retry;
- no UI automation fallback;
- human-in-the-loop.

Такие правила должны быть декларативными и тестируемыми.

---

# 33. Graceful Degradation

Если требуемый уровень безопасности невозможно обеспечить:

```text
Full autonomous execution
        ↓ unavailable
Safer alternative
        ↓ unavailable
User-assisted execution
        ↓ unavailable
Inform user
```

Agent не должен снижать безопасность только ради завершения Goal.

---

# 34. Security Invariants

В MVP должны выполняться следующие инварианты:

1. Model output никогда не является authorization.
2. Android permission никогда не равен Agent authorization.
3. User confirmation не выдаёт Android permission.
4. Safety Policy нельзя обойти через Strategy.
5. Strategy Score не может отменить hard constraint.
6. Experience не может отключить Safety.
7. Central Knowledge не может напрямую управлять устройством.
8. STOP имеет приоритет над pending execution.
9. Stale Plan не может выполняться.
10. Privacy restrictions применяются локально.
11. Safety Decision использует конкретную policy version.
12. Высокий риск требует соответствующего уровня подтверждения и/или проверки.
13. Safety ALLOW не означает успешное выполнение.
14. Execution success не означает Goal success без Verification.
15. Невозможность безопасно выполнить действие должна приводить к безопасной остановке, а не к обходу политики.

---

# 35. Минимальный Safety API

Концептуально:

```text
SafetyPolicyEngine
 ├─ evaluateCapability()
 ├─ evaluateStrategy()
 ├─ checkPermissions()
 ├─ checkPrivacy()
 ├─ checkRisk()
 ├─ checkConfirmation()
 ├─ checkAuthorization()
 ├─ evaluateRuntimeState()
 ├─ emergencyStop()
 └─ createDecisionRecord()
```

Результат:

```text
SafetyDecision
 ├─ decision
 ├─ reason_codes
 ├─ required_permissions
 ├─ confirmation_required
 ├─ effective_risk
 ├─ policy_version
 └─ decision_id
```

---

# 36. MVP Scope

В MVP обязательно:

- Risk Levels R0–R4;
- permission state model;
- distinction Permission / Authorization / Confirmation;
- local Safety Policy Engine;
- hard constraints;
- confirmation policy;
- privacy classification;
- STOP priority;
- runtime re-check;
- policy versioning;
- Safety Decision Record;
- structured reason codes;
- model/experience/central knowledge restrictions;
- integration with Strategy Selection;
- integration with Execution;
- integration with Verification;
- tests for critical safety invariants.

В MVP не требуется:

- сложная ML-based risk prediction;
- автономное обучение policy;
- автоматическая генерация новых safety rules моделью;
- распределённое управление policy;
- сложная поведенческая биометрия;
- полностью автономная адаптация confirmation thresholds.

---

# 37. Вертикальный срез

Первый вертикальный срез рекомендуется построить на:

```text
calendar.create_event
```

Пример:

```text
User:
«Создай завтра встречу с Иваном в 15:00»

        ↓

Goal Understanding

        ↓

Capability:
calendar.create_event

        ↓

Strategy Selection

        ↓

Safety Evaluation
 ├─ Calendar permission?
 ├─ Risk?
 ├─ User confirmation?
 ├─ Privacy?
 └─ Runtime state?

        ↓

ALLOW / CONFIRM / PERMISSION

        ↓

Execution

        ↓

Verification

        ↓

Final Result
```

Это позволит проверить всю цепочку:

```text
Goal
 → Capability
 → Strategy
 → Safety
 → Permission
 → Confirmation
 → Execution
 → Verification
 → Result
```

---

# 38. Связь с остальными контрактами

Архитектурная цепочка:

```text
Goal
  ↓
Task State & Re-planning
  ↓
Capability Contract
  ↓
Strategy Contract
  ↓
Strategy Selection
  ↓
Safety & Permission Contract
  ↓
Execution Contract
  ↓
Verification Contract
  ↓
Task Result
```

Model Adapter может помогать на этапах понимания и планирования, но не меняет порядок authority boundaries.

Experience и Central Knowledge находятся сбоку как advisory sources.

---

# 39. Главный архитектурный принцип

> **Agent может быть автономным, но его автономность ограничена проверяемыми правилами.**

И ещё важнее:

> **Разрешение выполнить действие и факт успешного выполнения — две разные вещи.**

Полная цепочка:

```text
Can I do it?
      ↓
Safety

Should I do it now?
      ↓
Authorization / Confirmation

How should I do it?
      ↓
Strategy

Did I actually do it?
      ↓
Execution Observation

Did the user's goal actually become true?
      ↓
Verification
```

---

# 40. Итог

Safety & Permission Contract превращает безопасность из набора отдельных `if` в самостоятельный архитектурный слой.

Он связывает:

- Capability;
- Strategy;
- Android permissions;
- user authorization;
- confirmation;
- privacy;
- risk;
- execution;
- verification;
- STOP;
- re-planning;
- audit.

При этом сохраняется главный принцип проекта:

> **Agent должен быть способен действовать самостоятельно, но никогда не должен самостоятельно расширять собственные полномочия.**
