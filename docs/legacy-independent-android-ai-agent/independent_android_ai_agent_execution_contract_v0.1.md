# Independent Android AI Agent
## Execution Contract v0.1

**Document type:** Architecture Contract  
**Version:** 0.1  
**Status:** Draft for architectural approval  
**Project:** Independent Android AI Agent

---

## 1. Purpose

Execution Contract defines how the Agent executes a previously selected strategy.

It establishes the boundary between:

```text
Goal
  ↓
Capability
  ↓
Strategy
  ↓
Strategy Selection
  ↓
Execution
  ↓
Verification
  ↓
Result / Re-plan
```

The Execution Layer is responsible for performing an authorized action.

It is **not** responsible for deciding whether the action is safe or whether the final user goal has been achieved.

Core principle:

> **Execution performs. Verification proves.**

---

# 2. Architectural Position

Execution is downstream from Strategy Selection and upstream from Verification.

```text
User
 ↓
Goal Model
 ↓
Planner
 ↓
Capability Registry
 ↓
Strategy Engine
 ↓
Safety / Permission Gate
 ↓
Execution Engine
 ↓
Verification
 ↓
Result
 ↓
Re-plan if necessary
```

Execution must not:

- bypass Safety Policy;
- invent permissions;
- select an unauthorized capability;
- silently replace the selected strategy;
- treat API success as proof of goal success;
- continue after a valid STOP request;
- modify policy to make execution possible.

---

# 3. Core Entities

## 3.1 Task

A Task represents the user-level work being performed.

```text
Task
 ├─ task_id
 ├─ goal_id
 ├─ capability_id(s)
 ├─ constraints
 ├─ priority
 ├─ cancellation_token
 ├─ execution_budget
 ├─ status
 └─ timestamps
```

A Task may contain multiple executions.

Example:

```text
Task:
"Создай встречу с Иваном завтра в 15:00"

Possible execution chain:

calendar.search_events
        ↓
calendar.create_event
        ↓
verification
```

---

## 3.2 Execution

Execution represents one selected strategy being attempted for a capability.

```text
Execution
 ├─ execution_id
 ├─ task_id
 ├─ capability_id
 ├─ strategy_id
 ├─ provider_id
 ├─ attempt_budget
 ├─ timeout_policy
 ├─ cancellation_token
 ├─ status
 ├─ side_effects
 └─ timestamps
```

One Task may have several Executions because re-planning may select another strategy.

Example:

```text
Task
 ├─ Execution #1
 │    └─ Android Calendar Provider
 │
 ├─ Verification → failed
 │
 └─ Execution #2
      └─ AppFunction
```

---

## 3.3 Attempt

Attempt is one concrete invocation of an Execution Adapter.

```text
Attempt
 ├─ attempt_id
 ├─ execution_id
 ├─ adapter
 ├─ start_time
 ├─ end_time
 ├─ timeout
 ├─ status
 ├─ raw_provider_result
 ├─ normalized_result
 └─ side_effect_report
```

An Execution may contain several attempts only when the retry policy permits it.

---

# 4. Execution Contract

Conceptual contract:

```text
ExecutionContract
 ├─ execution_id
 ├─ task_id
 ├─ capability_id
 ├─ strategy_id
 ├─ provider_id
 ├─ input
 ├─ preconditions
 ├─ timeout
 ├─ retry_policy
 ├─ cancellation_policy
 ├─ side_effect_policy
 ├─ idempotency
 ├─ status
 ├─ output
 ├─ error
 └─ verification_required
```

The contract must be versioned.

Example:

```json
{
  "execution_id": "exec_01",
  "task_id": "task_01",
  "capability_id": "calendar.create_event",
  "strategy_id": "android.calendar_provider",
  "provider_id": "android_calendar",
  "status": "RUNNING",
  "verification_required": true
}
```

---

# 5. Execution Lifecycle

The normal lifecycle is:

```text
CREATED
   ↓
PREPARING
   ↓
READY
   ↓
RUNNING
   ↓
COMPLETED
   ↓
VERIFYING
   ↓
VERIFIED / VERIFICATION_FAILED
```

Possible alternative states:

```text
CANCEL_REQUESTED
CANCELLED
TIMED_OUT
FAILED
PARTIAL
UNKNOWN
BLOCKED
REQUIRES_USER
```

Execution state and goal state must remain separate.

For example:

```text
Execution = COMPLETED
Goal = NOT_VERIFIED
```

is valid.

---

# 6. State Semantics

## CREATED

Execution record exists but has not started.

## PREPARING

Execution Adapter is preparing the provider/environment.

Examples:

- locating application;
- opening provider;
- preparing request;
- validating runtime conditions.

## READY

All runtime prerequisites are satisfied and execution can begin.

## RUNNING

Action is actively being performed.

## COMPLETED

Provider reported successful completion.

Important:

> COMPLETED does not mean the user goal is proven.

Verification follows where required.

## VERIFIED

Independent verification confirms the expected result.

## VERIFICATION_FAILED

Execution completed, but expected result was not confirmed.

This may trigger re-planning.

## FAILED

Execution failed before producing the expected result.

## PARTIAL

Some intended effects occurred, but the complete execution objective was not achieved.

## UNKNOWN

The system cannot reliably determine whether the intended side effect occurred.

UNKNOWN is intentionally different from FAILED.

Example:

```text
Calendar request sent
↓
network disconnected
↓
provider response unavailable
```

The event may or may not have been created.

The agent must not blindly repeat the operation if duplication is possible.

## CANCEL_REQUESTED

Cancellation was requested, but the underlying mechanism has not yet stopped.

## CANCELLED

Execution stopped before completion or was confirmed as cancelled.

Cancellation does not imply rollback.

## TIMED_OUT

Execution exceeded its allowed time.

A timeout does not automatically mean that no side effect occurred.

Therefore:

```text
TIMEOUT → VERIFY
```

may be required before retrying.

## BLOCKED

Execution cannot proceed because of policy, permission, capability, provider, or environment constraints.

## REQUIRES_USER

Execution requires explicit user involvement.

Examples:

- confirmation;
- authentication;
- permission grant;
- manual interaction;
- resolving ambiguous information.

---

# 7. Preconditions

Before execution starts, the Execution Engine must validate runtime preconditions.

Examples:

- selected strategy is still available;
- provider is available;
- permissions are still valid;
- capability is still valid;
- task has not been cancelled;
- environment has not changed in a blocking way;
- required application is installed;
- network is available if mandatory;
- authentication state is valid;
- user constraints remain valid.

Important:

> Strategy Selection is not a permanent authorization.

The environment may change between selection and execution.

Therefore Execution performs a final runtime validation.

---

# 8. Safety Boundary

Execution cannot override Safety Policy.

```text
Strategy Selection
       ↓
Safety / Permission Gate
       ↓
Execution
```

Additionally:

```text
Execution start
       ↓
Runtime Preconditions
       ↓
Execution
```

If a critical safety condition changes after selection:

```text
STOP EXECUTION
→ BLOCK / RE-PLAN / ASK USER
```

Execution must never interpret:

```text
"the user asked for it"
```

as sufficient authority to bypass local safety rules.

---

# 9. Permission Handling

Execution must distinguish:

```text
PERMISSION_GRANTED
PERMISSION_MISSING
PERMISSION_DENIED
PERMISSION_REVOKED
PERMISSION_UNKNOWN
```

Missing permissions must not be simulated.

The Execution Engine may return:

```text
REQUIRES_PERMISSION
```

and hand control back to the user.

User confirmation cannot itself create an Android permission.

---

# 10. Timeout Contract

Every execution must have a timeout policy.

```text
timeout_policy
 ├─ preparation_timeout
 ├─ execution_timeout
 └─ verification_timeout
```

Timeout behavior depends on side-effect characteristics.

### Read-only operation

Possible:

```text
TIMEOUT
→ retry
```

### Potentially side-effecting operation

Possible:

```text
TIMEOUT
→ UNKNOWN
→ VERIFY
→ decide whether retry is safe
```

Therefore:

> Timeout is an execution event, not automatically a failure classification.

---

# 11. Retry Contract

Retry is not identical to fallback.

## Retry

Same strategy/provider is attempted again.

```text
Strategy A
 ↓
temporary failure
 ↓
Strategy A retry
```

## Fallback

A different strategy/provider is selected.

```text
Strategy A
 ↓
failure
 ↓
re-plan
 ↓
Strategy B
```

Retry requires:

- retry policy allows it;
- attempt budget remains;
- failure is retryable;
- duplicate side effect is controlled;
- task has not been cancelled;
- safety policy still allows it.

---

# 12. Idempotency

Side-effecting operations should define idempotency behavior.

Example:

```text
calendar.create_event
```

must ideally use an idempotency key or equivalent duplicate-detection mechanism.

Possible policy:

```text
idempotency:
  supported: true
  key: task_id + logical_operation_id
```

If an operation is not safely idempotent:

```text
UNKNOWN
→ VERIFY
→ DO NOT BLINDLY RETRY
```

---

# 13. Side Effects

Execution must report expected and observed side effects.

```text
SideEffectReport
 ├─ expected
 ├─ observed
 ├─ confirmed
 └─ reversible
```

Example:

```text
Expected:
calendar event created

Observed:
provider returned success

Confirmed:
verification not yet performed

Reversible:
yes
```

Side effects must be tracked separately from provider response.

---

# 14. Partial Success

Partial success is a first-class state.

Example:

User asks:

```text
"Создай встречу и отправь приглашение"
```

Possible result:

```text
Calendar event → created
Invitation → not sent
```

Execution should not report the entire task as SUCCESS.

Instead:

```text
PARTIAL
```

with structured information about completed and incomplete parts.

The Planner may then continue with a new execution.

---

# 15. Unknown Result

UNKNOWN is one of the most important execution states.

Example:

```text
Action sent
 ↓
connection lost
 ↓
provider result unavailable
```

Possible states:

```text
UNKNOWN
 ├─ verification available → VERIFY
 ├─ safe reconciliation available → RECONCILE
 ├─ no reliable verification → ASK USER
 └─ never blindly duplicate a risky side effect
```

Unknown must be treated conservatively.

---

# 16. STOP / Cancellation

STOP is a global execution-control mechanism.

When the user presses STOP:

```text
STOP
 ↓
mark Task cancellation requested
 ↓
invalidate active execution chain
 ↓
send cancellation signal
 ↓
stop new actions
 ↓
stop current action where mechanism allows
 ↓
record actual state
 ↓
inform user
```

Every active Task must have a cancellation token.

Example:

```text
task_id = task_123
cancellation_token = cancel_123
```

Execution must check cancellation:

- before starting;
- before each significant action;
- during long-running operations where supported;
- before retry;
- before fallback;
- before applying a stale plan.

---

# 17. Cancellation vs Rollback

These are different concepts.

```text
STOP ≠ ROLLBACK
```

STOP means:

> Do not continue executing this task.

It does not necessarily mean:

> Undo everything already performed.

Example:

```text
Event created
 ↓
User presses STOP
```

The event may remain created.

If rollback is possible and explicitly supported, it must be a separate capability/strategy.

Example:

```text
calendar.delete_event
```

Rollback must never be assumed automatically.

---

# 18. Stale Plan Protection

A previously selected execution must not continue after task cancellation or replacement.

Each execution is bound to:

```text
task_id
plan_version
execution_id
cancellation_token
```

Before executing an action:

```text
is_task_active?
is_plan_current?
is_execution_current?
is_cancelled?
```

If any required condition fails:

```text
DO NOT EXECUTE
```

This protects against:

- delayed asynchronous operations;
- old retries;
- race conditions;
- stale UI events;
- provider callbacks arriving late.

---

# 19. Execution Adapter

Execution Adapter isolates the Execution Engine from the concrete mechanism.

```text
Execution Engine
       ↓
Execution Adapter
       ↓
Provider / Platform
```

Examples:

```text
AndroidCalendarProviderAdapter
AppFunctionAdapter
WebApiAdapter
UiAutomationAdapter
HumanAssistedAdapter
```

The Execution Engine should not contain provider-specific logic.

This preserves:

> Capability–Strategy–Provider separation.

---

# 20. UI Automation

UI automation is an execution mechanism, not the architecture foundation.

```text
Capability
   ↓
Strategy
   ↓
UiAutomationAdapter
```

UI automation must provide:

- cancellation where possible;
- timeout;
- state detection;
- result normalization;
- failure classification;
- verification support.

Because UI state can change unexpectedly, UI automation generally receives a higher reliability/risk penalty during strategy selection.

---

# 21. Human-in-the-Loop

Human involvement is a valid execution mechanism.

Example:

```text
Strategy:
human_assisted

Provider:
user
```

Possible reasons:

- authentication;
- CAPTCHA;
- permission;
- ambiguous UI;
- unsupported provider;
- sensitive action;
- unavailable automation.

The agent should clearly communicate:

```text
WHAT is needed
WHY it is needed
WHAT the user should do
WHAT happens afterwards
```

Human assistance does not bypass Safety Policy.

---

# 22. Handoff

An Execution may hand control to:

- another strategy;
- another provider;
- the user;
- another capability;
- a verification capability.

Example:

```text
Execution A
 ↓
REQUIRES_USER
 ↓
User authentication
 ↓
Execution resumes
```

A handoff must preserve:

```text
task_id
goal context
constraints
authorization state
cancellation state
execution history
```

---

# 23. Verification Boundary

Execution answers:

> "Did the selected mechanism report completion?"

Verification answers:

> "Did the intended result actually occur?"

Example:

```text
Execution:
Calendar Provider → SUCCESS

Verification:
Event exists with expected fields → VERIFIED
```

The Execution Engine must not manufacture verification evidence from its own provider response when independent verification is required.

---

# 24. Execution Result

Execution returns a normalized result.

```text
ExecutionResult
 ├─ status
 ├─ execution_id
 ├─ attempt_id
 ├─ output
 ├─ side_effects
 ├─ error
 ├─ verification_required
 ├─ verification_reference
 └─ timestamps
```

Possible normalized statuses:

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

`SUCCESS` at the Execution layer does not necessarily mean Goal Success.

---

# 25. Error Model

Errors should be structured.

Example categories:

```text
PRECONDITION_FAILED
PERMISSION_DENIED
PROVIDER_UNAVAILABLE
NETWORK_UNAVAILABLE
AUTHENTICATION_REQUIRED
TIMEOUT
CANCELLED
UI_STATE_CHANGED
INVALID_INPUT
CONFLICT
RATE_LIMITED
UNKNOWN_PROVIDER_RESULT
SAFETY_BLOCKED
RESOURCE_UNAVAILABLE
INTERNAL_ERROR
```

Each error should expose:

```text
error_code
retryable
requires_verification
requires_user
recommended_next_action
```

---

# 26. Re-planning Trigger

Execution may trigger re-planning when:

- selected strategy fails;
- provider disappears;
- permission changes;
- environment changes;
- verification fails;
- timeout occurs;
- result is UNKNOWN;
- user changes constraints;
- network changes;
- application state changes;
- strategy becomes unavailable.

Flow:

```text
Execution
 ↓
Failure / Unknown / Verification Failure
 ↓
Update Environment
 ↓
Re-check Candidates
 ↓
Strategy Selection
 ↓
New Execution
```

The agent should prefer solving the original Goal over repeating the same failed action.

---

# 27. Execution Budget

Each Task may define an execution budget.

Possible limits:

```text
max_attempts
max_strategies
max_execution_time
max_network_usage
max_user_confirmations
max_resource_cost
```

Budget prevents:

- infinite retry loops;
- endless strategy switching;
- uncontrolled resource consumption;
- excessive user interruption.

When budget is exhausted:

```text
GOAL_NOT_COMPLETED
```

with an explainable reason.

---

# 28. Execution History

The system should retain structured execution history.

Example:

```text
Task
 ├─ Execution #1
 │    ├─ Strategy A
 │    ├─ Attempt 1 → TIMEOUT
 │    └─ Verification → UNKNOWN
 │
 ├─ Re-plan
 │
 └─ Execution #2
      ├─ Strategy B
      └─ VERIFIED
```

This history feeds:

- user-visible Task History;
- Experience Store;
- debugging;
- analytics;
- future strategy selection.

History must not require storing private model chain-of-thought.

---

# 29. Observability

Execution observability should expose:

- task ID;
- execution ID;
- strategy;
- provider;
- attempt number;
- state transitions;
- duration;
- normalized result;
- side effects;
- verification state;
- cancellation state;
- error category.

It should not expose or require hidden model chain-of-thought.

---

# 30. Relationship with Experience

Execution produces useful experience signals:

```text
strategy
provider
context
result
verification
latency
failure reason
```

Experience Store may later use this information to improve strategy selection.

However:

> Execution history must never modify Safety Policy automatically.

---

# 31. Relationship with Central Knowledge

Central Knowledge Service may provide:

- known provider compatibility;
- known strategy availability;
- known failure patterns;
- recommended strategy metadata;
- version compatibility.

But:

> Central knowledge cannot directly execute actions on the device.

All received information is advisory and must pass local validation.

---

# 32. Concurrency

The MVP should initially avoid uncontrolled parallel execution.

Default:

```text
one active execution chain per task
```

Parallel execution may be introduced later for explicitly independent actions.

If parallel execution is introduced, the system must define:

- dependency graph;
- cancellation propagation;
- conflict handling;
- shared-resource locking;
- deterministic result aggregation.

---

# 33. Security Invariants

The Execution Layer must preserve these invariants:

1. No execution without an authorized capability.
2. No execution without a selected strategy.
3. No execution that violates local safety policy.
4. No execution after valid task cancellation.
5. No blind retry of unsafe UNKNOWN side effects.
6. No permission fabrication.
7. No stale-plan execution.
8. No provider response treated as universal proof of goal success.
9. No external knowledge can directly control device actions.
10. No execution adapter can bypass the Execution Engine policy boundary.

---

# 34. MVP Scope

Execution Contract v0.1 MVP should support:

### Required

- Task ID;
- Execution ID;
- Attempt ID;
- lifecycle states;
- precondition validation;
- timeout;
- retry policy;
- cancellation token;
- STOP;
- idempotency declaration;
- side-effect reporting;
- PARTIAL;
- UNKNOWN;
- structured errors;
- Execution Adapter;
- verification handoff;
- re-planning trigger;
- execution history.

### Deferred

- complex distributed execution;
- arbitrary parallel execution;
- automatic rollback framework;
- autonomous budget optimization;
- advanced transaction orchestration;
- distributed agent-to-agent execution;
- autonomous policy modification.

---

# 35. Vertical Slice Requirement

Execution Contract must be validated through a real vertical slice.

Recommended first slice:

```text
User:
"Создай событие в календаре"

        ↓

Goal Model

        ↓

calendar.create_event

        ↓

Strategy Selection

        ↓

Safety / Permission Gate

        ↓

Execution

        ↓

Calendar Provider

        ↓

Verification

        ↓

Verified Result
```

The slice must demonstrate:

- normal success;
- missing permission;
- timeout;
- cancellation;
- duplicate protection;
- UNKNOWN result handling;
- verification failure;
- fallback/re-planning.

This follows the project principle:

> **Architecture is not considered validated until a real user goal passes through the complete pipeline.**

---

# 36. Architectural Invariants

The following invariants are binding for v0.1:

### Invariant 1
> Execution performs an authorized strategy; it does not authorize itself.

### Invariant 2
> Execution success is not automatically Goal success.

### Invariant 3
> STOP prevents further execution and invalidates stale continuation.

### Invariant 4
> UNKNOWN is not equivalent to FAILED.

### Invariant 5
> Timeout does not prove absence of side effects.

### Invariant 6
> Retry and fallback are different mechanisms.

### Invariant 7
> Cancellation does not imply rollback.

### Invariant 8
> Verification is an independent responsibility.

### Invariant 9
> Execution adapters cannot bypass the policy boundary.

### Invariant 10
> Re-planning must preserve the original user Goal unless the user changes it.

---

# 37. Final Execution Principle

The execution subsystem is not a macro runner.

It is a controlled mechanism for turning a validated strategy into a real-world effect while preserving:

```text
Safety
+
Cancellation
+
Idempotency
+
Observability
+
Verification
+
Re-planning
```

The central rule is:

> **Execute the selected strategy, observe what actually happened, and never confuse an attempted action with an achieved goal.**

---

## Next Architectural Artifact

Recommended next step:

**Verification Contract v0.1**

It should define:

- verification levels V0–V4;
- evidence sources;
- independent verification;
- expected-state modeling;
- verification confidence;
- conflict between provider result and observed state;
- verification of UNKNOWN;
- verification after partial success;
- goal-level success criteria;
- when verification can be skipped;
- when verification failure triggers re-planning.
