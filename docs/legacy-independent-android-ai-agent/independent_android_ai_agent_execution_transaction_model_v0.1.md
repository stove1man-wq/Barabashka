\# Independent Android AI Agent

> **Примечание (D-006, 2026-10-12).** Состояния транзакции — только перечисленные в этом документе. Доступность capability и состояние разрешения — другие понятия (`docs/architecture/GLOSSARY.md`, раздел 5).

\## Execution Transaction Model v0.1



\*\*Document:\*\* `execution\_transaction\_model\_v0.1.md`

\*\*Version:\*\* 0.1

\*\*Status:\*\* Draft Architecture Contract

\*\*Project:\*\* Independent Android AI Agent



\---



\## 1. Purpose



This document defines the transaction model for executing actions in the Independent Android AI Agent.



The purpose of the model is to ensure that an agent action is not treated as an uncontrolled sequence of UI operations, but as a \*\*bounded, observable, recoverable and verifiable execution transaction\*\*.



The transaction model must provide:



\* explicit execution boundaries;

\* deterministic transaction state;

\* controlled action ordering;

\* precondition validation;

\* execution monitoring;

\* result verification;

\* failure classification;

\* rollback or recovery where possible;

\* safe interruption;

\* persistence of transaction state;

\* compatibility with the Recovery \& Resilience Contract;

\* prevention of uncontrolled repeated actions.



This document defines the architectural contract only.



It does not yet define the concrete Android implementation.



\---



\# 2. Core Principle



The agent must never interpret:



> "I decided to perform an action"



as equivalent to:



> "The action has been successfully completed."



Instead, execution follows the lifecycle:



```text

Intent

&#x20; ↓

Plan

&#x20; ↓

Transaction

&#x20; ↓

Preconditions

&#x20; ↓

Execution

&#x20; ↓

Observation

&#x20; ↓

Verification

&#x20; ↓

Commit / Recovery / Abort

```



The transaction is considered successful only after the expected postcondition has been verified.



\---



\# 3. Transaction Definition



An \*\*Execution Transaction\*\* is a bounded unit of agent-controlled work that:



1\. has a unique identifier;

2\. has an explicit objective;

3\. contains a finite sequence of actions;

4\. has defined preconditions;

5\. has defined expected postconditions;

6\. maintains execution state;

7\. records observations and outcomes;

8\. can be interrupted;

9\. can enter recovery;

10\. eventually reaches a terminal state.



A transaction must never remain logically ambiguous.



Every transaction must eventually become one of:



```text

COMMITTED

FAILED

ABORTED

RECOVERY\_REQUIRED

```



or another explicitly defined terminal state introduced by a future contract revision.



\---



\# 4. Transaction Boundaries



A transaction must have a clear beginning and end.



\### Transaction start



A transaction starts only after:



\* the user's intent has been interpreted;

\* an executable plan exists;

\* required capabilities have been evaluated;

\* required permissions/interfaces are available;

\* the transaction receives a unique ID.



\### Transaction end



A transaction ends only after:



\* success has been verified;

\* failure has been established;

\* the transaction has been explicitly aborted;

\* or recovery has been delegated to a recovery subsystem.



There must be no implicit transaction completion.



\---



\# 5. Transaction Identity



Each transaction receives a globally unique identifier.



Example:



```text

tx\_20261007\_000001

```



The identifier must remain stable throughout the entire transaction lifecycle.



Retries and recovery attempts must not silently create the appearance of a new logical transaction.



Where necessary, individual attempts may receive separate identifiers:



```text

transaction\_id

attempt\_id

action\_id

```



Example:



```text

transaction\_id: tx\_20261007\_000001

attempt\_id:     attempt\_0002

action\_id:      action\_0005

```



This distinction is important for recovery and duplicate-action prevention.



\---



\# 6. Transaction State Machine



The minimum transaction state machine is:



```text

CREATED

&#x20;  ↓

VALIDATING

&#x20;  ↓

READY

&#x20;  ↓

EXECUTING

&#x20;  ↓

OBSERVING

&#x20;  ↓

VERIFYING

&#x20;  ↓

COMMITTED

```



Failure branches:



```text

VALIDATING ─────→ ABORTED

EXECUTING ──────→ FAILED

OBSERVING ──────→ RECOVERY\_REQUIRED

VERIFYING ──────→ RECOVERY\_REQUIRED

```



Recovery:



```text

RECOVERY\_REQUIRED

&#x20;       ↓

&#x20;    RECOVERING

&#x20;       ↓

&#x20;  ┌────┴────┐

&#x20;  ↓         ↓

RESUME      ABORT

&#x20;  ↓

EXECUTING

```



A future implementation may refine this state machine, but must preserve its fundamental semantics.



\---



\# 7. State Semantics



\## 7.1 CREATED



The transaction exists but has not yet been validated.



No external side effects should occur in this state.



\---



\## 7.2 VALIDATING



The system verifies:



\* transaction structure;

\* required capabilities;

\* permissions;

\* target availability;

\* action prerequisites;

\* safety constraints;

\* required application state.



\---



\## 7.3 READY



All known preconditions are satisfied.



The transaction may begin execution.



\---



\## 7.4 EXECUTING



One or more actions are actively being performed.



Execution must remain bounded by the transaction plan.



The agent must not spontaneously introduce unrelated actions.



\---



\## 7.5 OBSERVING



The system observes the resulting device/application state.



Observation may include:



\* UI state;

\* accessibility state;

\* application state;

\* API response;

\* system state;

\* filesystem state;

\* network state where applicable;

\* other permitted signals.



Observation is not equivalent to verification.



\---



\## 7.6 VERIFYING



The observed state is compared against the expected postcondition.



Only this stage can establish successful completion.



\---



\## 7.7 COMMITTED



The transaction's objective has been successfully verified.



No further actions belonging to the transaction may be executed.



\---



\## 7.8 FAILED



The transaction cannot complete under current conditions and recovery is not required or not possible.



The failure must be classified and recorded.



\---



\## 7.9 ABORTED



The transaction was deliberately stopped.



Possible causes include:



\* user cancellation;

\* safety policy;

\* invalidated preconditions;

\* unrecoverable state;

\* timeout;

\* explicit system shutdown.



\---



\## 7.10 RECOVERY\_REQUIRED



The system cannot safely determine that execution can continue.



This state is intentionally conservative.



When transaction state becomes uncertain, the agent must prefer:



> stop → inspect → recover



over:



> continue blindly.



\---



\## 7.11 RECOVERING



A recovery procedure is actively attempting to restore a safe and known execution state.



Recovery is governed by the separate Recovery \& Resilience Contract.



\---



\# 8. Action Model



A transaction consists of discrete actions.



Conceptually:



```text

Transaction

&#x20;├── Action 1

&#x20;├── Action 2

&#x20;├── Action 3

&#x20;└── Action N

```



Each action must have:



\* unique action ID;

\* action type;

\* target;

\* preconditions;

\* execution method;

\* timeout;

\* expected result;

\* verification method;

\* retry policy;

\* failure behavior.



Example conceptual action:



```text

Action:

&#x20;   id: action\_0003

&#x20;   type: CLICK

&#x20;   target: "Send"

&#x20;   precondition:

&#x20;       button\_exists == true

&#x20;   expected\_result:

&#x20;       message\_sent == true

&#x20;   verification:

&#x20;       message\_status == SENT

```



\---



\# 9. Action Atomicity



An action should be treated as the smallest meaningful execution unit.



However, physical atomicity cannot always be guaranteed.



For example:



```text

tap button

```



may physically occur, but the system may lose visibility immediately afterward.



Therefore the model distinguishes:



```text

Action execution

```



from:



```text

Action completion

```



An action is complete only after its result is sufficiently observed and verified.



\---



\# 10. Preconditions



Before executing an action, required preconditions must be evaluated.



Examples:



```text

target application is foreground

target element exists

device is unlocked

required permission is available

network is available

required API is available

current screen matches expected state

```



If a critical precondition is not satisfied:



```text

DO NOT EXECUTE

```



The transaction must instead:



\* wait;

\* re-plan;

\* recover;

\* or abort.



\---



\# 11. Postconditions



Every externally meaningful action should define an expected postcondition.



Example:



```text

Action:

&#x20;   "press send"



Expected postcondition:

&#x20;   message appears in sent state

```



The agent must not infer success solely from:



\* the action being issued;

\* lack of an error;

\* elapsed time;

\* visual similarity;

\* model confidence.



Verification must use observable evidence.



\---



\# 12. Commit Rule



The transaction may enter `COMMITTED` only if:



```text

all required actions completed

AND

final postcondition verified

AND

no unresolved critical error exists

```



Conceptually:



```text

COMMIT =

&#x20;   execution\_complete

&#x20;   AND verification\_success

&#x20;   AND transaction\_consistent

```



If verification is inconclusive:



```text

NOT COMMITTED

```



The transaction must instead enter:



```text

RECOVERY\_REQUIRED

```



or:



```text

FAILED

```



depending on the situation.



\---



\# 13. No Blind Success



The following must never be treated as proof of success:



```text

"the tap was performed"

"the command was sent"

"the API request returned"

"the screen changed"

"the model believes it worked"

```



These are observations or intermediate signals.



Success requires verification against the transaction's expected result.



\---



\# 14. Retry Model



Retries must be explicit.



The system must never implement uncontrolled loops such as:



```text

while not successful:

&#x20;   repeat\_action()

```



Instead:



```text

attempt 1

&#x20;   ↓

observe

&#x20;   ↓

failure

&#x20;   ↓

classify

&#x20;   ↓

retry / recover / abort

```



Each retry must have:



\* maximum attempt count;

\* reason;

\* delay or recovery condition;

\* duplicate-action protection;

\* updated observation.



\---



\# 15. Idempotency



Where possible, actions should be designed to be idempotent.



An idempotent operation can safely be repeated without producing an unintended additional effect.



Preferred:



```text

SET volume = 50%

```



rather than:



```text

INCREASE volume

```



when exact state control is available.



For non-idempotent operations, the transaction must maintain stronger execution tracking.



Examples:



```text

send message

purchase item

delete file

transfer funds

submit form

```



These operations must not be automatically repeated merely because verification failed.



\---



\# 16. Duplicate-Action Protection



Before retrying a potentially non-idempotent action, the system must determine whether the action may already have succeeded.



Example:



```text

send\_message()

```



returns uncertain result.



The system must not immediately execute:



```text

send\_message()

```



again.



Instead:



```text

observe current state

&#x20;      ↓

determine whether message already exists

&#x20;      ↓

if exists:

&#x20;   mark action successful

else:

&#x20;   consider retry

```



This is a fundamental safety requirement.



\---



\# 17. Timeouts



Every externally interacting action must have a bounded timeout.



The system must distinguish:



```text

timeout

```



from:



```text

failure

```



A timeout means:



> The expected result was not observed within the allowed period.



It does not necessarily mean:



> The action did not happen.



Therefore timeout handling must normally enter an observation/recovery path before retrying.



\---



\# 18. Cancellation



Transactions must support explicit cancellation.



Cancellation may originate from:



\* user;

\* safety subsystem;

\* system shutdown;

\* resource exhaustion;

\* invalidated permissions;

\* higher-priority task.



Cancellation must stop further actions as quickly as safely possible.



For non-interruptible operations, cancellation may become:



```text

CANCEL\_PENDING

```



until the current safe boundary is reached.



\---



\# 19. Crash Recovery



Transaction state must be persistable.



If the application crashes during:



```text

EXECUTING

OBSERVING

VERIFYING

```



the system must not assume success or failure after restart.



Instead:



```text

restore transaction

&#x20;      ↓

inspect persisted state

&#x20;      ↓

observe current external state

&#x20;      ↓

determine actual outcome

&#x20;      ↓

resume / commit / recover / abort

```



This prevents duplicate side effects after process restart.



\---



\# 20. Persistence Requirements



At minimum, the persistent transaction record should contain:



```text

transaction\_id

transaction\_type

created\_at

updated\_at

current\_state

current\_action\_id

attempt\_count

plan\_reference

last\_observation

last\_error

recovery\_state

commit\_status

```



The exact storage mechanism is outside this v0.1 contract.



\---



\# 21. Transaction Journal



The system should maintain an append-oriented execution journal.



Conceptual record:



```text

timestamp

transaction\_id

action\_id

state

event

observation

result

error

```



Example:



```text

10:42:01 tx\_001 CREATED

10:42:02 tx\_001 VALIDATING

10:42:03 tx\_001 READY

10:42:04 tx\_001 action\_001 EXECUTING

10:42:05 tx\_001 action\_001 OBSERVING

10:42:06 tx\_001 action\_001 VERIFIED

10:42:07 tx\_001 COMMITTED

```



The journal is important for:



\* recovery;

\* debugging;

\* auditability;

\* deterministic replay analysis;

\* identifying duplicate actions.



\---



\# 22. External Side Effects



Actions are divided conceptually into:



\### Read-only



Examples:



```text

inspect screen

read application state

query API

read file

```



\### Reversible



Examples:



```text

open screen

change temporary UI state

set a recoverable configuration

```



\### Potentially irreversible



Examples:



```text

send message

delete data

purchase

submit

transfer

publish

```



The transaction system must apply stricter verification and retry rules to potentially irreversible actions.



\---



\# 23. Safety Boundary



The agent must not execute an action merely because it is technically possible.



Before execution, the transaction must satisfy:



```text

intent valid

AND

capability available

AND

preconditions satisfied

AND

action permitted

AND

transaction state valid

```



If any critical condition fails:



```text

execution blocked

```



\---



\# 24. Interaction with Recovery \& Resilience



The Execution Transaction Model and Recovery \& Resilience Contract are complementary.



The transaction model defines:



```text

what is being executed

```



The recovery model defines:



```text

what happens when execution becomes uncertain,

fails, or the environment changes unexpectedly

```



Relationship:



```text

Execution Transaction

&#x20;       ↓

&#x20;  uncertainty

&#x20;       ↓

Recovery \& Resilience

&#x20;       ↓

restore known state

&#x20;       ↓

resume / abort / fail

```



Recovery must never silently modify the transaction objective.



\---



\# 25. Interaction with Planning



Planning produces the transaction plan.



Conceptually:



```text

User Intent

&#x20;   ↓

Planner

&#x20;   ↓

Execution Plan

&#x20;   ↓

Transaction

&#x20;   ↓

Executor

```



The executor must not freely redesign the plan during execution.



If the plan becomes invalid, the transaction should enter:



```text

RECOVERY\_REQUIRED

```



or request replanning through an explicit mechanism.



\---



\# 26. Interaction with Observation



Observation is a first-class part of execution.



The agent must continuously distinguish:



```text

expected state

actual state

unknown state

```



Example:



```text

Expected:

&#x20;   payment confirmation visible



Observed:

&#x20;   screen changed



Verified:

&#x20;   payment confirmation with matching transaction details

```



Only the final condition establishes successful completion.



\---



\# 27. Unknown State



An unknown external state is not equivalent to failure.



For example:



```text

action issued

application stopped responding

process restarted

```



The system cannot know whether the action succeeded.



Therefore:



```text

UNKNOWN ≠ FAILED

UNKNOWN ≠ SUCCESS

```



The correct response is:



```text

RECOVERY\_REQUIRED

```



followed by state inspection.



\---



\# 28. Transaction Invariants



The following invariants must always hold.



\### Invariant 1



A committed transaction must have verified success.



\### Invariant 2



A transaction must never execute actions after `COMMITTED`.



\### Invariant 3



An aborted transaction must not resume automatically.



\### Invariant 4



An uncertain non-idempotent action must not be blindly retried.



\### Invariant 5



Every action must belong to exactly one transaction.



\### Invariant 6



Every transaction must have a unique ID.



\### Invariant 7



Every terminal transaction state must be persistent.



\### Invariant 8



Recovery must be explicit and observable.



\### Invariant 9



Timeout must not automatically imply safe retry.



\### Invariant 10



The agent must never assume success solely from its own action invocation.



\---



\# 29. Minimal Transaction Record



A conceptual v0.1 representation:



```text

ExecutionTransaction {

&#x20;   transaction\_id

&#x20;   objective

&#x20;   state



&#x20;   plan {

&#x20;       actions\[]

&#x20;   }



&#x20;   current\_action\_id



&#x20;   preconditions\[]

&#x20;   postconditions\[]



&#x20;   attempts

&#x20;   timeout\_policy

&#x20;   retry\_policy



&#x20;   last\_observation

&#x20;   last\_error



&#x20;   recovery\_status



&#x20;   created\_at

&#x20;   updated\_at

}

```



This is a conceptual contract and not yet a required programming language schema.



\---



\# 30. Example: Safe UI Action



Objective:



```text

Send a message.

```



Transaction:



```text

CREATED

&#x20; ↓

VALIDATING

&#x20; ↓

READY

```



Action 1:



```text

focus message field

```



Action 2:



```text

enter message

```



Action 3:



```text

press send

```



After Action 3:



```text

OBSERVING

```



The agent checks:



```text

message appears in conversation

AND

message content matches expected content

```



If verified:



```text

COMMITTED

```



If uncertain:



```text

RECOVERY\_REQUIRED

```



The agent must not immediately press Send again.



\---



\# 31. Example: Application Crash



Suppose:



```text

Action:

&#x20;   submit form

```



The submit button was pressed.



Immediately afterward:



```text

application crashes

```



The system restarts.



The transaction state is restored as:



```text

transaction = tx\_001

action = submit

state = RECOVERY\_REQUIRED

```



The system inspects the external state.



If the submission already exists:



```text

mark action VERIFIED

→ COMMITTED

```



If it does not exist:



```text

resume or retry according to policy

```



This prevents duplicate submissions.



\---



\# 32. Example: Timeout



Action:



```text

open application

```



Expected:



```text

application main screen visible

```



Timeout occurs.



The system must not immediately execute:



```text

open application

```



again.



Instead:



```text

OBSERVE

&#x20;  ↓

is application already open?

&#x20;  ↓

YES → VERIFY

NO  → RECOVERY / RETRY

```



\---



\# 33. Transaction Completion Guarantee



The transaction model aims to provide the following guarantee:



> The agent never reports successful completion unless the expected outcome has been verified to the degree required by the action's risk level.



This does not imply that external systems are infallible.



It means the agent's internal transaction state must remain honest about what is known and unknown.



\---



\# 34. Risk-Based Verification



Different actions require different verification strength.



\### Low risk



Example:



```text

open settings

```



Possible verification:



```text

expected screen visible

```



\### Medium risk



Example:



```text

change configuration

```



Possible verification:



```text

configuration value read back

```



\### High risk



Example:



```text

delete / submit / purchase / transfer

```



Required:



```text

strong postcondition verification

\+

duplicate protection

\+

explicit transaction boundary

\+

conservative recovery

```



Future versions should formalize a risk classification system.



\---



\# 35. Transaction Ownership



Only one execution controller should own a transaction at a time.



Concurrent execution of the same transaction is prohibited.



The system must prevent:



```text

Executor A → tx\_001

Executor B → tx\_001

```



from simultaneously producing external side effects.



Concurrency control will be specified in a future implementation contract.



\---



\# 36. Nested Transactions



v0.1 does not require nested transactions.



If future architecture introduces them, the relationship must be explicit.



Example:



```text

Parent Transaction

&#x20;   ├── Child Transaction A

&#x20;   └── Child Transaction B

```



A child transaction must not independently commit a parent objective.



Nested transaction semantics require a future contract.



\---



\# 37. Transaction Observability



The transaction system should expose enough state to answer:



```text

What was the agent trying to do?

What action was being executed?

What actually happened?

What was verified?

Why did execution stop?

Can execution safely continue?

```



These questions must be answerable without relying on model memory.



\---



\# 38. Failure Classification



At minimum, failures should distinguish:



```text

PRECONDITION\_FAILURE

EXECUTION\_FAILURE

OBSERVATION\_FAILURE

VERIFICATION\_FAILURE

TIMEOUT

PERMISSION\_FAILURE

CAPABILITY\_FAILURE

ENVIRONMENT\_CHANGE

APPLICATION\_CRASH

USER\_CANCELLED

SAFETY\_BLOCK

UNKNOWN\_STATE

```



The exact taxonomy may evolve.



\---



\# 39. Determinism



The transaction state machine must be deterministic for the same:



```text

transaction state

\+

observed state

\+

explicit external inputs

```



The language model may assist in planning or interpretation, but transaction state transitions must not depend on unconstrained model improvisation.



\---



\# 40. LLM Boundary



The LLM may:



\* interpret intent;

\* propose a plan;

\* suggest recovery;

\* interpret observations;

\* explain failures.



The LLM must not be the sole authority for:



\* transaction commitment;

\* irreversible-action verification;

\* duplicate-action prevention;

\* persistence;

\* transaction identity;

\* safety state transitions.



Critical transaction decisions must be enforced by deterministic system logic.



\---



\# 41. Security Principle



Transaction state must be treated as trusted system state.



LLM-generated text must not be allowed to directly overwrite:



```text

transaction state

authorization state

permission state

commit state

recovery state

```



The system must validate all transitions.



\---



\# 42. Versioning



This document defines:



```text

Execution Transaction Model v0.1

```



Future versions may add:



\* transaction schemas;

\* risk levels;

\* formal action types;

\* concurrency rules;

\* persistence format;

\* cryptographic transaction integrity;

\* distributed execution;

\* advanced rollback;

\* transaction replay;

\* formal state transition validation.



Such changes require a new contract version.



\---



\# 43. MVP Requirements



For the MVP, the implementation must provide at minimum:



1\. unique transaction ID;

2\. explicit transaction lifecycle;

3\. discrete actions;

4\. action preconditions;

5\. action postconditions;

6\. bounded execution;

7\. observation after external actions;

8\. verification before commit;

9\. explicit retry limits;

10\. duplicate-action protection;

11\. timeout handling;

12\. cancellation;

13\. persistent transaction state;

14\. crash recovery;

15\. unknown-state handling;

16\. execution journal;

17\. deterministic terminal states;

18\. separation between LLM reasoning and transaction authority.



\---



\# 44. Explicit Non-Goals for v0.1



This version does not define:



\* Android-specific APIs;

\* AccessibilityService implementation;

\* UI automation implementation;

\* database schema;

\* encryption implementation;

\* distributed transactions;

\* multi-agent transactions;

\* formal rollback for every Android operation;

\* payment-specific safety mechanisms;

\* cryptographic signing of transactions;

\* production-grade audit storage.



These may be specified in later contracts.



\---



\# 45. Architectural Summary



The fundamental execution rule is:



```text

PLAN

&#x20; ↓

CREATE TRANSACTION

&#x20; ↓

VALIDATE

&#x20; ↓

EXECUTE ONE ACTION

&#x20; ↓

OBSERVE

&#x20; ↓

VERIFY

&#x20; ↓

CONTINUE

```



If uncertainty occurs:



```text

STOP

&#x20; ↓

RECOVER

&#x20; ↓

OBSERVE

&#x20; ↓

VERIFY

&#x20; ↓

RESUME / COMMIT / ABORT

```



Never:



```text

ACTION

&#x20; ↓

ASSUME SUCCESS

&#x20; ↓

NEXT ACTION

```



\---



\# 46. Final Contract Statement



The Independent Android AI Agent must treat execution as a \*\*transactional process rather than a stream of model-generated commands\*\*.



The system must preserve the distinction between:



```text

intent

plan

action

observation

verification

commit

```



The most important invariant is:



> \*\*No verified result — no commit.\*\*



The second fundamental invariant is:



> \*\*Unknown result — no blind retry.\*\*



Together, these rules form the basis for safe, recoverable and auditable agent execution.



\---



\*\*Status:\*\* `v0.1 — Architecture Contract Draft`



\*\*Next stage:\*\* only after this document is accepted and archived, proceed to the next architectural contract.



