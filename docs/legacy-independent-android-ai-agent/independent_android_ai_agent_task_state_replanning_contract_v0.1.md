# Independent Android AI Agent
## Task State & Re-planning Contract v0.1

**Document type:** Architecture Contract  
**Version:** 0.1  
**Status:** Draft for architectural approval  
**Project:** Independent Android AI Agent

---

## 1. Purpose

This contract defines the lifecycle of a user task from initial goal creation to final result.

It connects the previously defined contracts:

```text
Goal
 ↓
Capability
 ↓
Strategy
 ↓
Execution
 ↓
Verification
 ↓
Task Result
```

and defines what happens when the original plan is no longer valid.

Core principle:

> **The Agent must preserve the user's Goal while allowing the Plan to change.**

---

# 2. Core Distinction

The system must distinguish:

```text
Goal
Task
Plan
Execution
Verification
Result
```

They are related but not interchangeable.

### Goal

What the user ultimately wants.

Example:

```text
"Создай встречу с Иваном завтра в 15:00."
```

### Task

The active system-level work required to achieve the Goal.

### Plan

A current proposal for how the Goal can be achieved.

### Execution

One concrete attempt using a selected Strategy.

### Verification

Evidence-based assessment of whether the intended state was achieved.

### Result

The user-facing conclusion about the Goal.

---

# 3. Goal vs Task

A Goal may survive several Task plan revisions.

Example:

```text
Goal:
"Создай встречу завтра в 15:00"

Task Plan v1:
Calendar Provider

↓ provider unavailable

Task Plan v2:
AppFunction

↓ unavailable

Task Plan v3:
UI automation
```

The Goal remains unchanged.

This is essential:

> **A failed plan does not automatically mean a failed Goal.**

---

# 4. Task Entity

Conceptual structure:

```text
Task
 ├─ task_id
 ├─ goal_id
 ├─ goal
 ├─ constraints
 ├─ priority
 ├─ plan_version
 ├─ state
 ├─ cancellation_token
 ├─ execution_budget
 ├─ active_execution
 ├─ verification_state
 ├─ result
 ├─ history
 └─ timestamps
```

The Task is the primary lifecycle container.

---

# 5. Task Lifecycle

Primary state machine:

```text
CREATED
   ↓
UNDERSTANDING
   ↓
PLANNING
   ↓
READY
   ↓
EXECUTING
   ↓
VERIFYING
   ↓
COMPLETED
```

Alternative states:

```text
NEEDS_CLARIFICATION
WAITING_FOR_USER
WAITING_FOR_PERMISSION
RE-PLANNING
PARTIAL
FAILED
BLOCKED
CANCEL_REQUESTED
CANCELLED
UNKNOWN
EXPIRED
```

---

# 6. State Semantics

## CREATED

The system has accepted a new user task.

No execution has started.

---

## UNDERSTANDING

The Agent is constructing the Goal Model.

It may identify:

- intent;
- entities;
- constraints;
- required capabilities;
- ambiguity;
- missing information.

If required information is missing:

```text
NEEDS_CLARIFICATION
```

---

## PLANNING

The Agent is generating or revising a plan.

Planning may include:

- capability discovery;
- strategy discovery;
- strategy scoring;
- dependency analysis;
- verification planning;
- safety checks.

---

## READY

A valid current plan exists and execution can begin.

---

## EXECUTING

One or more authorized executions are active.

The MVP should normally maintain one active execution chain per Task.

---

## VERIFYING

The Agent is evaluating whether the intended state has been achieved.

---

## COMPLETED

The Goal has been sufficiently verified according to policy.

---

## WAITING_FOR_USER

The Task cannot safely continue without user involvement.

Examples:

- confirmation;
- authentication;
- manual action;
- clarification;
- unavailable permission;
- sensitive decision.

---

## WAITING_FOR_PERMISSION

Required Android/system permission is not available.

---

## RE-PLANNING

The current plan is no longer valid or sufficient.

A new plan must be generated without unnecessarily discarding the original Goal.

---

## PARTIAL

Some required Goal conditions are achieved, while others remain unresolved.

---

## FAILED

The system has sufficient evidence that the Goal cannot be completed through the current task path and no acceptable alternative remains.

---

## BLOCKED

The Goal may be achievable in principle, but current policy, permission, platform, provider, or environment prevents execution.

---

## CANCEL_REQUESTED

The user or system requested cancellation.

The Agent is stopping active work and preventing continuation.

---

## CANCELLED

The Task has been stopped.

Existing side effects remain real unless independently reversed.

---

## UNKNOWN

The system cannot reliably determine whether the Goal was achieved.

---

## EXPIRED

The Task can no longer safely or meaningfully continue because its validity window has ended.

---

# 7. Goal Model

The Task should maintain a structured Goal Model.

Conceptually:

```text
GoalModel
 ├─ goal_id
 ├─ natural_language
 ├─ normalized_intent
 ├─ desired_state
 ├─ constraints
 ├─ preferences
 ├─ entities
 ├─ success_criteria
 ├─ priority
 └─ ambiguity
```

Example:

```text
Natural language:
"Поставь встречу с Иваном завтра на 15:00"

Desired state:
calendar event exists

Constraints:
participant = Иван
date = tomorrow
time = 15:00

Success criteria:
event exists with required fields
```

---

# 8. Success Criteria

A Task must know what "done" means.

Success criteria may include:

```text
required_conditions
optional_conditions
acceptable_variants
verification_requirements
```

Example:

```text
Required:
event exists
date correct
time correct

Optional:
location specified

Verification:
V2
```

The Agent must not continue executing merely because optional conditions are missing.

---

# 9. Goal Completion

Goal completion is evaluated from verified conditions.

Conceptual model:

```text
Goal
 ↓
Success Criteria
 ↓
Verification Results
 ↓
Goal State
```

Possible Goal States:

```text
ACHIEVED
PARTIALLY_ACHIEVED
FAILED
UNKNOWN
BLOCKED
CANCELLED
```

---

# 10. Plan Entity

A Plan is a versioned proposal.

```text
Plan
 ├─ plan_id
 ├─ task_id
 ├─ version
 ├─ goal_reference
 ├─ steps
 ├─ dependencies
 ├─ expected_results
 ├─ verification_points
 ├─ budget
 └─ status
```

Example:

```text
Plan v1
  Step 1: calendar.create_event
  Step 2: verify event
```

---

# 11. Plan Versioning

Every meaningful plan revision creates a new version.

```text
Plan v1
 ↓
failure
 ↓
Plan v2
 ↓
failure
 ↓
Plan v3
```

Old plans remain recorded but become inactive.

Only the current plan may execute.

---

# 12. Stale Plan Protection

An execution must verify that its plan is still current.

Before every significant action:

```text
task.active?
plan.current?
execution.current?
cancellation.active?
constraints.valid?
```

If not:

```text
DO NOT EXECUTE
```

This prevents:

- delayed callbacks;
- old retries;
- asynchronous race conditions;
- stale UI events;
- old provider responses;
- execution after user changes the task.

---

# 13. User Constraint Changes

The user may change the task while it is running.

Example:

```text
User:
"Создай встречу завтра в 15:00."

Agent:
working...

User:
"Нет, лучше в 16:00."
```

The Agent must not continue the old plan.

Flow:

```text
Current Task
 ↓
Constraint Change
 ↓
Invalidate current plan
 ↓
Create new Plan version
 ↓
Re-evaluate Safety
 ↓
Re-select Strategy
 ↓
Execute
 ↓
Verify
```

The Goal Model must be updated first.

---

# 14. Goal Replacement

A user may completely replace the task.

Example:

```text
User:
"Создай встречу."

↓

"Стоп. Не надо. Найди ближайшую аптеку."
```

The previous task becomes:

```text
CANCELLED / REPLACED
```

A new Task is created.

The new Task must have a new:

```text
task_id
goal_id
plan_version
cancellation_token
```

This prevents old work from continuing.

---

# 15. Re-planning Triggers

Re-planning may be triggered by:

- execution failure;
- verification failure;
- UNKNOWN result;
- provider unavailable;
- permission change;
- environment change;
- user constraint change;
- user Goal change;
- timeout;
- network change;
- application state change;
- strategy becoming unavailable;
- unexpected side effect;
- budget constraints;
- conflicting evidence.

---

# 16. Re-planning Flow

General flow:

```text
Current Plan
     ↓
Trigger
     ↓
Stop / invalidate affected execution
     ↓
Update environment
     ↓
Update Goal Model
     ↓
Recalculate remaining work
     ↓
Discover capabilities
     ↓
Select strategies
     ↓
Safety / permission gate
     ↓
Create new Plan
     ↓
Execute
     ↓
Verify
```

Re-planning must not blindly restart the entire task.

---

# 17. Preserve Completed Work

Re-planning should preserve verified progress.

Example:

```text
Goal:
Create event + send invitation

Plan v1:
Create event → VERIFIED
Send invitation → FAILED

Re-plan:
Do NOT recreate event.

Continue:
Send invitation using another strategy.
```

This prevents duplicate side effects.

---

# 18. Remaining Goal Model

After partial completion, the Agent should calculate:

```text
Goal
 ↓
Completed conditions
 +
Remaining conditions
```

Example:

```text
Goal:
A + B + C

Verified:
A
B

Remaining:
C
```

Re-planning should focus on C.

---

# 19. Re-planning vs Retry

They are different.

### Retry

Same plan/strategy remains valid.

```text
Plan v1
 └─ Strategy A
     └─ Retry
```

### Re-planning

Current plan is no longer considered sufficient.

```text
Plan v1
 ↓
invalid / insufficient
 ↓
Plan v2
```

A retry must not be disguised as re-planning, and vice versa.

---

# 20. Re-planning vs Goal Change

Goal change originates from the user.

Re-planning normally originates from the Agent's observation of changed circumstances.

```text
Execution failure
→ Re-plan

User changes desired time
→ Update Goal
→ Re-plan

User cancels
→ Cancel Task
```

This distinction is important for auditability and user trust.

---

# 21. Re-planning Budget

A Task must have a finite re-planning budget.

Possible limits:

```text
max_plan_versions
max_strategy_switches
max_total_attempts
max_execution_time
max_network_usage
max_user_interruptions
```

If exhausted:

```text
FAILED
or
REQUIRES_USER
```

depending on context.

---

# 22. Anti-loop Protection

The Agent must prevent loops such as:

```text
Strategy A
 ↓
failure
Strategy B
 ↓
failure
Strategy A
 ↓
failure
Strategy B
...
```

Possible protections:

- strategy attempt history;
- failure fingerprints;
- temporary strategy exclusion;
- plan version limit;
- execution budget;
- environment change requirement;
- hysteresis.

A strategy that already failed for the same unchanged reason should not be immediately selected again.

---

# 23. Re-planning Hysteresis

The system should avoid unstable switching:

```text
A → B → A → B
```

when both strategies remain similarly viable.

A previously failed strategy should receive a temporary penalty unless:

- environment changed;
- provider state changed;
- new evidence appeared;
- strategy version changed;
- failure cause was transient.

---

# 24. Task Budget

The Task-level budget is broader than an individual Execution budget.

```text
Task Budget
 ├─ time
 ├─ attempts
 ├─ strategies
 ├─ network
 ├─ resources
 ├─ confirmations
 └─ user interruptions
```

Execution budgets operate inside Task budgets.

---

# 25. User Attention Budget

The Agent should treat user attention as a limited resource.

Examples of costly interruptions:

- repeated confirmation requests;
- repeated permission explanations;
- unnecessary clarification;
- redundant status messages.

The Agent should prefer:

```text
one meaningful clarification
```

over:

```text
multiple small interruptions
```

unless safety requires otherwise.

This follows the principle:

> **Attention Respect.**

---

# 26. STOP Integration

STOP is a Task-level control.

When STOP is activated:

```text
Task
 ↓
CANCEL_REQUESTED
 ↓
invalidate current Plan
 ↓
invalidate pending retries
 ↓
invalidate fallback execution
 ↓
propagate cancellation
 ↓
record observable state
 ↓
CANCELLED / PARTIAL / UNKNOWN
```

No new execution may begin after cancellation unless the user explicitly creates a new Task.

---

# 27. Cancellation Propagation

Cancellation must propagate through:

```text
Task
 ↓
Plan
 ↓
Execution
 ↓
Attempt
 ↓
Provider / Adapter
```

Where the underlying mechanism supports cancellation, it should be invoked.

Where it does not, the Agent must at minimum prevent subsequent actions and stale continuation.

---

# 28. Task Replacement Protection

When a user starts a new task:

```text
Old Task → inactive
New Task → active
```

Old asynchronous results must not modify the new Task.

All callbacks should validate:

```text
task_id
plan_version
execution_id
cancellation_token
```

before applying results.

---

# 29. Task Result

The Task Result is stronger than an Execution Result.

Conceptual structure:

```text
TaskResult
 ├─ task_id
 ├─ goal_id
 ├─ status
 ├─ completed_conditions
 ├─ remaining_conditions
 ├─ verification_summary
 ├─ executions
 ├─ user_action_required
 ├─ reason
 └─ timestamps
```

Possible statuses:

```text
SUCCESS
PARTIAL
FAILED
BLOCKED
CANCELLED
UNKNOWN
REQUIRES_USER
```

---

# 30. Final Result Semantics

User-facing result must correspond to Goal state.

Examples:

### Success

```text
Goal:
ACHIEVED

User:
"Готово. Встреча создана на завтра в 15:00."
```

### Partial

```text
Goal:
PARTIALLY_ACHIEVED

User:
"Встреча создана, но приглашение отправить не удалось."
```

### Unknown

```text
Goal:
UNKNOWN

User:
"Я не могу надёжно подтвердить, была ли встреча создана."
```

### Blocked

```text
Goal:
BLOCKED

User:
"Я не могу выполнить это действие без разрешения календаря."
```

The Agent must not use "Готово" when the Goal is merely attempted.

---

# 31. Clarification

The Agent should ask for clarification when ambiguity materially affects:

- Goal interpretation;
- safety;
- target;
- side effects;
- verification;
- strategy selection.

It should avoid unnecessary clarification when a safe, reasonable interpretation is available.

Example:

```text
"Напомни мне завтра."
```

If the time is essential and ambiguous:

```text
NEEDS_CLARIFICATION
```

If a default can be safely used and policy permits:

```text
continue
```

---

# 32. User Control During Re-planning

The user can modify:

- Goal;
- constraints;
- preferred strategy;
- privacy preference;
- speed vs reliability;
- network allowance;
- confirmation requirements.

User preferences affect planning and scoring only within safety boundaries.

The user cannot use preferences to bypass:

- system permissions;
- local safety policy;
- platform restrictions;
- mandatory verification.

---

# 33. Goal Priority

Tasks may have priorities.

Example:

```text
CRITICAL
HIGH
NORMAL
LOW
BACKGROUND
```

Priority can influence scheduling and resource allocation.

It must not bypass safety.

Example:

```text
CRITICAL ≠ permission to execute a blocked operation
```

---

# 34. Expiration

Some Tasks have validity windows.

Examples:

```text
"Открой приложение сейчас."
"Напомни через 10 минут."
"Оплати до 18:00."
```

If the validity window expires:

```text
EXPIRED
```

The Agent should not continue executing a stale time-sensitive plan without reassessment.

---

# 35. Background Continuation

If Android stops or suspends the application, the Task must be recoverable where possible.

Persist at minimum:

```text
task_id
goal
current_plan
plan_version
execution_state
verification_state
cancellation_state
budget
history
```

Upon restart:

```text
restore Task
 ↓
validate environment
 ↓
validate plan
 ↓
check cancellation
 ↓
verify existing side effects
 ↓
resume / re-plan / stop
```

The Agent must not blindly resume an old execution.

---

# 36. Crash Recovery

If the Agent crashes during an operation:

```text
last persisted state
+
actual environment
```

must be reconciled.

Recommended flow:

```text
Restart
 ↓
recover Task
 ↓
identify interrupted Execution
 ↓
determine possible side effects
 ↓
verify state
 ↓
mark VERIFIED / FAILED / UNKNOWN
 ↓
re-plan if necessary
```

This is especially important for side-effecting operations.

---

# 37. Task History

Task History should represent:

```text
Goal
 ↓
Plan versions
 ↓
Executions
 ↓
Verification
 ↓
Re-plans
 ↓
Final Result
```

It should not require storing private model chain-of-thought.

User-visible history should focus on:

- what the Agent attempted;
- what happened;
- what was verified;
- why it changed strategy;
- what remains unresolved.

---

# 38. Audit Record

Each important transition should be auditable.

Example:

```text
Task created
Plan v1 created
Strategy A selected
Execution started
Execution timeout
Verification performed
Result UNKNOWN
Plan v2 created
Strategy B selected
Execution completed
Verification VERIFIED
Task COMPLETED
```

Audit records should include version references:

```text
policy_version
strategy_scoring_version
plan_version
capability_version
verification_policy_version
```

---

# 39. Observability

The system should expose:

- current Task state;
- current Plan version;
- active Execution;
- verification status;
- remaining conditions;
- budget usage;
- cancellation state;
- re-planning count;
- final result.

It should not expose hidden model chain-of-thought.

---

# 40. Experience Integration

Task history may generate Experience Store records.

Useful experience:

```text
Goal fingerprint
Environment
Plan
Strategy
Execution result
Verification result
Re-plan count
Latency
Failure reason
```

Experience can influence future planning.

It cannot:

- bypass Safety;
- bypass permissions;
- guarantee future success;
- replace verification.

---

# 41. Central Knowledge Integration

Central Knowledge may provide:

- known plan patterns;
- strategy compatibility;
- known provider changes;
- known failure/recovery patterns;
- recommended verification methods.

It remains advisory.

The local Agent owns:

```text
current Goal
current device state
permissions
Safety Policy
current Task state
final decision
```

---

# 42. Concurrency

MVP default:

```text
one active Task
one active execution chain per Task
```

Future versions may support multiple Tasks.

If multiple Tasks are active, the system must define:

- resource arbitration;
- priority;
- cancellation;
- shared provider conflicts;
- user attention management;
- safety interaction.

No uncontrolled concurrency in MVP.

---

# 43. Security Invariants

1. Only the current Task may execute.
2. Only the current Plan may produce new Executions.
3. Cancelled Tasks cannot continue.
4. Replaced Tasks cannot modify new Tasks.
5. Old callbacks cannot mutate current state.
6. Re-planning cannot change the Goal without user input.
7. External knowledge cannot directly alter Task authority.
8. User preferences cannot bypass Safety Policy.
9. Budget limits cannot be silently exceeded.
10. Final success requires sufficient verification.

---

# 44. MVP Scope

### Required

- Goal Model;
- Task entity;
- Task state machine;
- Plan entity;
- Plan versioning;
- stale plan protection;
- user constraint changes;
- task replacement;
- STOP propagation;
- re-planning triggers;
- re-planning budget;
- anti-loop protection;
- partial completion;
- final Task Result;
- crash/restart recovery basics;
- Task History;
- audit transitions.

### Deferred

- complex multi-task scheduler;
- arbitrary parallel task graphs;
- distributed task execution;
- autonomous task prioritization;
- long-term background agents;
- advanced resource arbitration;
- multi-agent coordination.

---

# 45. Vertical Slice Requirement

The architecture must be validated through an end-to-end scenario.

Recommended:

```text
User:
"Создай встречу завтра в 15:00."
```

Expected lifecycle:

```text
CREATED
 ↓
UNDERSTANDING
 ↓
PLANNING
 ↓
READY
 ↓
EXECUTING
 ↓
VERIFYING
 ↓
COMPLETED
```

Failure scenario:

```text
EXECUTION
 ↓
TIMEOUT
 ↓
VERIFY
 ↓
UNKNOWN
 ↓
RE-PLANNING
 ↓
Alternative Strategy
 ↓
EXECUTION
 ↓
VERIFY
 ↓
COMPLETED
```

User change scenario:

```text
"Создай на 15:00"
 ↓
working
 ↓
"Лучше на 16:00"
 ↓
invalidate Plan v1
 ↓
Plan v2
 ↓
execute
 ↓
verify
```

STOP scenario:

```text
working
 ↓
STOP
 ↓
CANCEL_REQUESTED
 ↓
invalidate plan
 ↓
cancel active execution
 ↓
CANCELLED / PARTIAL
```

---

# 46. Architectural Invariants

### Invariant 1

> The Goal survives plan changes unless the user changes the Goal.

### Invariant 2

> A failed Execution does not automatically mean Goal failure.

### Invariant 3

> Only the current Plan may initiate new execution.

### Invariant 4

> A cancelled Task cannot continue executing.

### Invariant 5

> User constraint changes invalidate affected plan versions.

### Invariant 6

> Verified completed work must not be unnecessarily repeated.

### Invariant 7

> UNKNOWN requires reconciliation or explicit escalation where appropriate.

### Invariant 8

> Re-planning is bounded and protected against loops.

### Invariant 9

> Final Goal success requires sufficient verification.

### Invariant 10

> The Agent must preserve the user's Goal while adapting the method of achieving it.

---

# 47. Final Task Principle

A task is not a fixed script.

It is a controlled attempt to achieve a Goal in a changing environment.

The correct model is:

```text
GOAL
 ↓
CURRENT PLAN
 ↓
EXECUTION
 ↓
OBSERVATION
 ↓
VERIFICATION
 ↓
RESULT
       ↘
        RE-PLAN
          ↓
      NEW PLAN
          ↓
      EXECUTION
          ↓
      VERIFICATION
```

The central rule is:

> **Plans are replaceable. The user's Goal is authoritative.**

---

## Next Architectural Artifact

Recommended next step:

**Model Adapter Contract v0.1**

It should define:

- model independence;
- local vs cloud models;
- common model interface;
- capability of models;
- routing;
- context limits;
- streaming;
- structured output;
- model failure;
- fallback;
- privacy boundaries;
- model permissions;
- LLM authority limits;
- how models interact with Goal, Planning and Strategy layers.
