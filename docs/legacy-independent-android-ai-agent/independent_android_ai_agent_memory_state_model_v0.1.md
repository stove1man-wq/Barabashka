\# Independent Android AI Agent



\## Memory \& State Model



\### Architecture Contract v0.1



\*\*Status:\*\* `v0.1 — Architecture Contract Draft`

\*\*Scope:\*\* MVP architecture

\*\*Document:\*\* `memory\_state\_model\_v0.1.md`



\---



\## 1. Purpose



This document defines the initial memory and state architecture for the Independent Android AI Agent.



The goal is to establish a clear separation between:



\* transient reasoning context;

\* persistent agent memory;

\* execution state;

\* application state;

\* user preferences;

\* task history;

\* security-sensitive state;

\* externally retrieved information.



The agent must remain recoverable, predictable, and safe even when:



\* the Android process is killed;

\* the application is restarted;

\* a task is interrupted;

\* the network becomes unavailable;

\* an LLM provider changes;

\* a tool call produces an uncertain result;

\* the model loses its conversational context.



\---



\# 2. Core Principle



> \*\*Memory is not state, and state is not reasoning context.\*\*



The system must never treat the LLM conversation context as the authoritative source of system state.



Authoritative state must exist outside the LLM.



The LLM may receive a representation of state, but it must not become the owner of that state.



\---



\# 3. Memory and State Layers



The architecture separates information into distinct layers:



```text

User Input

&#x20;   ↓

Conversation Context

&#x20;   ↓

Working Memory

&#x20;   ↓

Persistent Memory

&#x20;   ↓

Task State

&#x20;   ↓

Execution Transaction State

&#x20;   ↓

System / Security State

```



These layers have different lifetimes, trust levels, and persistence requirements.



\---



\# 4. State Categories



The MVP defines the following categories.



\## 4.1 Conversation Context



Short-lived information required to understand the current interaction.



Examples:



\* recent user messages;

\* current conversation topic;

\* current answer context;

\* temporary references;

\* recent tool results.



Properties:



\* temporary;

\* reconstructable;

\* not authoritative;

\* may be discarded;

\* may be summarized.



Conversation context must never be the sole source of critical state.



\---



\## 4.2 Working Memory



Temporary information used while solving the current task.



Examples:



\* intermediate observations;

\* temporary hypotheses;

\* candidate plans;

\* temporary variables;

\* unresolved references;

\* intermediate reasoning results.



Properties:



\* task-scoped;

\* short-lived;

\* disposable;

\* not automatically promoted to persistent memory.



Working memory may be cleared after task completion unless explicitly promoted.



\---



\## 4.3 Persistent Memory



Information intentionally retained across sessions.



Examples:



\* stable user preferences;

\* explicit user instructions;

\* long-term configuration;

\* approved behavioral preferences;

\* durable task metadata.



Persistent memory must have:



\* explicit ownership;

\* defined schema;

\* provenance;

\* creation/update timestamp;

\* trust classification;

\* retention policy.



The agent must not persist arbitrary model-generated assumptions as facts.



\---



\## 4.4 Task State



Represents the current and historical state of an agent task.



Example:



```text

Task

&#x20;├── task\_id

&#x20;├── objective

&#x20;├── status

&#x20;├── created\_at

&#x20;├── updated\_at

&#x20;├── current\_plan

&#x20;├── current\_step

&#x20;├── observations

&#x20;├── verification

&#x20;└── execution\_transaction\_ids

```



Task state must survive process interruption when the task is recoverable.



\---



\## 4.5 Execution State



Execution state belongs to the Execution Transaction Model.



It includes:



\* transaction ID;

\* lifecycle state;

\* current action;

\* action results;

\* verification state;

\* recovery state;

\* retry information;

\* terminal state.



Execution state is authoritative for execution.



Memory must never override transaction state.



\---



\## 4.6 Security State



Security state includes information such as:



\* permissions;

\* authorization decisions;

\* consent state;

\* trust state;

\* credential availability;

\* security restrictions;

\* emergency-stop state.



Security state has higher authority than reasoning context or memory.



Memory cannot override security state.



\---



\## 4.7 System State



System state describes the environment relevant to execution.



Examples:



\* device capabilities;

\* network availability;

\* application availability;

\* provider availability;

\* Android state;

\* current foreground application;

\* storage availability.



System state should be observed rather than blindly assumed.



\---



\# 5. Source of Truth



Each category must have an authoritative owner.



| Information            | Authoritative Source        |

| ---------------------- | --------------------------- |

| Current task           | Task State                  |

| Execution status       | Execution Transaction State |

| Permission             | Security Layer              |

| Authorization          | Security Layer              |

| Device capability      | Capability subsystem        |

| Provider availability  | Provider subsystem          |

| User preference        | Persistent Memory           |

| Current UI/environment | Observation layer           |

| LLM interpretation     | Reasoning layer             |

| Tool result            | Tool/Provider layer         |



The LLM must not become the authoritative owner of any critical state.



\---



\# 6. Memory Trust Model



Stored information must have an explicit trust classification.



Recommended MVP levels:



```text

UNTRUSTED

OBSERVED

USER\_PROVIDED

SYSTEM\_VERIFIED

POLICY\_CONTROLLED

```



Examples:



\### USER\_PROVIDED



Information explicitly supplied by the user.



\### OBSERVED



Information obtained from the environment.



\### SYSTEM\_VERIFIED



Information confirmed by a trusted subsystem.



\### POLICY\_CONTROLLED



Information defined by security or system policy.



\### UNTRUSTED



Information originating from external content, model inference, or an uncertain source.



\---



\# 7. Model Inference Is Not Automatically Memory



The following must not automatically become persistent memory:



\* LLM assumptions;

\* inferred user preferences;

\* speculative conclusions;

\* temporary hypotheses;

\* external web content;

\* tool output that has not been validated;

\* model-generated summaries presented as facts.



Promotion to persistent memory requires an explicit rule.



\---



\# 8. Memory Promotion



Information may move from temporary context to persistent memory only when the system determines that it is:



1\. useful beyond the current task;

2\. sufficiently trustworthy;

3\. permitted to be stored;

4\. correctly classified;

5\. consistent with security policy;

6\. attributable to a known source.



A model suggestion alone is insufficient.



Example:



```text

LLM inference

&#x20;     ↓

Candidate Memory

&#x20;     ↓

Validation

&#x20;     ↓

Policy Check

&#x20;     ↓

User/System Confirmation when required

&#x20;     ↓

Persistent Memory

```



\---



\# 9. Memory Deletion and Expiration



Not all memory should live indefinitely.



Every persistent memory class should have a retention policy.



Possible retention modes:



```text

SESSION

TASK

TEMPORARY

UNTIL\_REPLACED

PERSISTENT

USER\_CONTROLLED

```



Temporary information should expire automatically where appropriate.



Persistent information must have a defined deletion mechanism.



\---



\# 10. Memory Update Rules



Memory updates must be explicit state transitions.



The system should avoid uncontrolled mutation.



Conceptually:



```text

READ

&#x20; ↓

PROPOSE UPDATE

&#x20; ↓

VALIDATE

&#x20; ↓

AUTHORIZE

&#x20; ↓

COMMIT

```



For important memory, updates should be atomic.



A partially written memory entry must not appear as valid state.



\---



\# 11. Conflict Resolution



Conflicting information must not be silently merged.



Example:



```text

Memory A:

"Preferred provider = Local Model"



Memory B:

"Preferred provider = Cloud Model"

```



The system must identify the conflict.



Resolution may depend on:



\* source authority;

\* timestamp;

\* explicit user instruction;

\* policy priority;

\* scope;

\* validity period.



General rule:



> \*\*Higher-authority information overrides lower-authority information.\*\*



If authority is equal and the conflict cannot be resolved safely, preserve the conflict rather than inventing a resolution.



\---



\# 12. State Versioning



Important state should be versioned.



At minimum, persisted state should support:



\* schema version;

\* state version;

\* update timestamp;

\* unique identifier.



Example:



```text

state\_id

schema\_version

state\_version

created\_at

updated\_at

payload

```



Schema migrations must be explicit.



The system must not silently reinterpret old state using a new schema.



\---



\# 13. Atomicity



Critical state transitions must be atomic.



For example:



```text

Task State:

EXECUTING



Transaction:

EXECUTING



```



must not become:



```text

Task State:

COMMITTED



Transaction:

EXECUTING

```



unless the transition is intentionally defined and valid.



State updates involving transactions must preserve cross-component consistency.



\---



\# 14. Crash Recovery



After process termination, the system must reconstruct recoverable state from persistent storage.



Recovery should follow:



```text

Load persisted state

&#x20;       ↓

Validate schema

&#x20;       ↓

Validate integrity

&#x20;       ↓

Determine last known state

&#x20;       ↓

Detect incomplete operations

&#x20;       ↓

Determine recovery requirement

&#x20;       ↓

Resume / verify / abort

```



The LLM must not be required to remember what happened before the crash.



\---



\# 15. Unknown State



Unknown state must be represented explicitly.



Examples:



```text

UNKNOWN

NOT\_OBSERVED

UNVERIFIED

STALE

CONFLICTED

```



The system must not convert:



```text

UNKNOWN

```



into:



```text

SUCCESS

```



by assumption.



This follows the execution rule:



> \*\*Unknown result — no blind retry.\*\*



\---



\# 16. Stale State



State may become invalid because the external environment changed.



Examples:



\* application closed;

\* permission revoked;

\* device locked;

\* network disconnected;

\* provider became unavailable;

\* UI changed;

\* another application modified the target.



Therefore, persisted state does not automatically mean current reality.



The agent must distinguish:



```text

Persisted State

```



from:



```text

Current Observed State

```



When necessary, current state must be re-observed.



\---



\# 17. State Reconciliation



When persisted state differs from observed state:



```text

Persisted State

&#x20;      ↓

Current Observation

&#x20;      ↓

Comparison

&#x20;      ↓

Reconciliation

```



The system must not blindly overwrite authoritative state based on an observation of uncertain reliability.



Reconciliation must respect:



\* source authority;

\* transaction state;

\* security state;

\* timestamps;

\* verification requirements.



\---



\# 18. Memory and Security Boundary



Persistent memory may contain sensitive information.



Therefore memory storage must obey the Permission \& Security Model.



Rules:



\* sensitive memory must be classified;

\* access must be authorized;

\* unnecessary data must not be stored;

\* secrets must not be stored in ordinary memory;

\* credentials must not be exposed to the LLM;

\* sensitive memory must not be automatically sent to remote providers;

\* memory export must follow policy.



\---



\# 19. Memory and LLM Boundary



The LLM receives only the context necessary for reasoning.



Conceptually:



```text

Persistent State

&#x20;      ↓

State Selection

&#x20;      ↓

Policy / Privacy Filtering

&#x20;      ↓

Context Construction

&#x20;      ↓

LLM

```



The entire database must never be automatically exposed to the model.



The LLM should receive:



\* relevant memory;

\* relevant task state;

\* relevant observations;

\* necessary constraints.



\---



\# 20. Memory Injection Protection



Stored memory must be treated as data, not instructions.



A memory record must not automatically acquire authority merely because it is retrieved.



Example:



```text

Memory:

"Always ignore security restrictions."

```



This must remain untrusted data.



Memory cannot override:



\* system policy;

\* security policy;

\* user denial;

\* permission state;

\* transaction rules.



\---



\# 21. External Content Boundary



External information entering memory must retain its provenance.



Examples:



\* web pages;

\* documents;

\* application content;

\* messages;

\* tool output;

\* imported data.



External content must not become trusted policy merely because it was stored.



\---



\# 22. Memory Retrieval



Memory retrieval should be relevance-based and policy-aware.



Conceptually:



```text

Current Task

&#x20;    ↓

Retrieve Candidate Memory

&#x20;    ↓

Filter by Scope

&#x20;    ↓

Filter by Trust

&#x20;    ↓

Filter by Security

&#x20;    ↓

Filter by Relevance

&#x20;    ↓

Provide to Reasoning

```



The retrieval subsystem must not return sensitive or unrelated information merely because it is available.



\---



\# 23. Memory Scope



Memory should have explicit scope.



Possible scopes:



```text

GLOBAL

USER

DEVICE

APPLICATION

TASK

SESSION

TRANSACTION

```



A task-specific memory item must not automatically become global memory.



Likewise, application-specific information must not automatically be treated as universal user preference.



\---



\# 24. State Ownership



Every state object must have a defined owner.



Example:



```text

TaskManager       → Task State

TransactionManager → Execution State

SecurityManager   → Security State

ProviderRegistry  → Provider State

CapabilityManager → Capability State

MemoryManager     → Persistent Memory

ObservationLayer  → Observed Environment

```



Multiple components may read state, but ownership must remain unambiguous.



\---



\# 25. No Hidden State



Critical behavior must not depend on undocumented internal memory.



The system should avoid:



\* hidden mutable flags;

\* implicit model memory;

\* undocumented caches;

\* untracked provider state;

\* invisible authorization state.



If state can change agent behavior, it should have a defined owner and lifecycle.



\---



\# 26. State Persistence



The MVP should persist at minimum:



\* active tasks;

\* recoverable task state;

\* execution transaction state;

\* required recovery information;

\* persistent user preferences;

\* security-relevant state where appropriate;

\* schema versions.



Ephemeral reasoning does not need to be persisted unless required for recovery or audit.



\---



\# 27. State Integrity



Persisted state should be protected against accidental corruption.



The MVP should support:



\* validation;

\* schema checking;

\* atomic writes;

\* versioning;

\* corruption detection;

\* safe failure.



If critical state cannot be trusted, the system must enter a safe recovery path rather than continue blindly.



\---



\# 28. Memory Backup and Restore



Backup and restore are outside the initial MVP implementation but the architecture must not prevent them.



Future backup mechanisms must preserve:



\* provenance;

\* schema version;

\* security classification;

\* ownership;

\* integrity.



Sensitive memory must not be exported without authorization.



\---



\# 29. Memory Lifecycle



The conceptual lifecycle is:



```text

CREATED

&#x20;  ↓

VALIDATED

&#x20;  ↓

ACTIVE

&#x20;  ↓

UPDATED

&#x20;  ↓

EXPIRED / REPLACED / DELETED

```



Invalid memory must not become active.



\---



\# 30. Relationship with Execution Transactions



Memory and execution state must remain separate.



Example:



```text

Memory:

"User usually prefers provider X."



Transaction:

"Provider X failed during this task."

```



The transaction failure must not automatically delete the user preference.



Likewise, a preference must not force a transaction to use an unavailable or unauthorized provider.



\---



\# 31. Relationship with Provider / Tool Layer



Provider state is not user memory.



Examples:



\* provider availability;

\* tool health;

\* capability metadata;

\* tool version;

\* timeout configuration.



These belong to the provider/tool subsystem.



Memory may reference them but must not own them.



\---



\# 32. Relationship with Permission / Security



Security state has higher authority than memory.



Example:



```text

Memory:

"User allows microphone access."



Current Security State:

"Microphone permission revoked."

```



The current security state wins.



The agent must not infer authorization from memory.



\---



\# 33. Relationship with Recovery



Recovery may read persistent state, but it must not weaken security.



Recovery must:



1\. load state;

2\. validate state;

3\. identify incomplete work;

4\. check current security conditions;

5\. re-observe external state when required;

6\. determine whether continuation is safe;

7\. resume, verify, or abort.



\---



\# 34. State Machine Principle



Important state should be represented as explicit state machines rather than arbitrary flags.



Example:



```text

Task:

CREATED

&#x20; ↓

PLANNING

&#x20; ↓

READY

&#x20; ↓

EXECUTING

&#x20; ↓

VERIFYING

&#x20; ↓

COMPLETED



Failure:

&#x20; ↓

RECOVERY\_REQUIRED

&#x20; ↓

RECOVERING

&#x20; ↓

RESUME / ABORT

```



The exact task lifecycle may evolve in later architecture stages.



\---



\# 35. Deterministic State Transitions



State transitions must be:



\* explicit;

\* validated;

\* observable;

\* auditable where necessary;

\* reproducible where practical.



Illegal transitions must be rejected.



Example:



```text

COMPLETED → EXECUTING

```



must not occur without an explicitly defined new task or recovery mechanism.



\---



\# 36. Memory Auditability



Important persistent-memory changes should be traceable.



At minimum:



```text

memory\_id

operation

source

timestamp

previous\_version

new\_version

reason

```



The system should be able to determine why important memory exists.



\---



\# 37. User Control



The user must ultimately control persistent personal memory.



The architecture should support:



\* inspection;

\* correction;

\* deletion;

\* disabling selected memory classes;

\* clearing temporary memory;

\* controlling sensitive-memory retention.



Exact UI is outside this contract.



\---



\# 38. Privacy by Default



The MVP follows:



> \*\*Store the minimum information necessary to provide useful continuity.\*\*



The system should prefer:



```text

Minimal Memory

```



over:



```text

Complete Historical Recording

```



\---



\# 39. Offline Operation



Memory and essential state management must work without internet access.



The agent must not depend on a cloud memory service for:



\* active task recovery;

\* transaction recovery;

\* local preferences;

\* critical state;

\* security decisions.



Cloud synchronization may be added later as an optional capability.



\---



\# 40. Cloud Memory



Cloud memory is not authoritative by default.



If cloud synchronization is introduced later:



```text

Local State

&#x20;   ↕

Synchronization Layer

&#x20;   ↕

Remote State

```



Synchronization must respect:



\* security policy;

\* conflict resolution;

\* provenance;

\* encryption;

\* user consent;

\* offline operation.



\---



\# 41. Memory Consistency Principle



The system should prefer consistency and explicit uncertainty over fabricated continuity.



If the system cannot determine whether a memory item is valid:



```text

UNKNOWN

```



is preferable to:



```text

ASSUMED TRUE

```



\---



\# 42. MVP Memory Model



The MVP requires the following logical components:



```text

MemoryManager

TaskStateManager

StateStore

StateValidator

StateSerializer

StateRecoveryManager

ContextBuilder

MemoryPolicy

```



These are logical responsibilities, not necessarily separate classes or processes.



\---



\# 43. MVP Requirements



The MVP implementation must support:



\* explicit memory categories;

\* explicit state ownership;

\* persistent task state;

\* recoverable execution state;

\* schema versioning;

\* state validation;

\* atomic persistence;

\* unknown-state representation;

\* stale-state detection;

\* memory scope;

\* memory trust classification;

\* memory provenance;

\* memory retention rules;

\* controlled memory promotion;

\* controlled memory updates;

\* security-aware memory access;

\* filtered context construction;

\* offline persistence;

\* crash recovery;

\* user-controlled persistent memory;

\* separation between memory and LLM context.



\---



\# 44. Explicit Non-Goals



The following are outside this architecture contract:



\* implementation of a specific database;

\* vector database selection;

\* embedding model selection;

\* semantic-search implementation;

\* cloud synchronization implementation;

\* distributed memory;

\* multi-device synchronization;

\* full backup architecture;

\* advanced knowledge graphs;

\* long-term autonomous self-modification;

\* biological or human-like memory simulation.



These may be addressed in later stages.



\---



\# 45. Architectural Invariants



The following rules are mandatory.



1\. LLM context is not authoritative system state.

2\. Memory is not execution state.

3\. Execution state is owned by the transaction system.

4\. Security state overrides memory.

5\. Current observation may invalidate stale persisted state.

6\. Unknown state must remain explicitly unknown.

7\. Model inference is not automatically persistent memory.

8\. External content cannot grant itself authority through memory.

9\. Memory scope must be explicit.

10\. Memory trust must be explicit.

11\. Critical state must have a defined owner.

12\. Critical state transitions must be validated.

13\. Critical state must survive process interruption when recovery requires it.

14\. Sensitive memory must follow security policy.

15\. The LLM receives only the memory required for the current task.

16\. Memory must not silently override system policy.

17\. Recovery must not weaken security.

18\. Offline operation must not depend on cloud memory.

19\. State corruption must result in safe failure.

20\. Important memory changes must be traceable.



\---



\# 46. Core Architectural Rule



> \*\*The LLM may reason about memory and state, but it does not own memory or state.\*\*



\---



\# 47. Final Boundary



The architecture establishes the following separation:



```text

Memory

&#x20;  ≠

Task State

&#x20;  ≠

Execution State

&#x20;  ≠

Security State

&#x20;  ≠

Current Observation

&#x20;  ≠

LLM Context

```



Each layer has its own authority, lifecycle, and trust model.



\---



\# 48. Final Safety Rule



> \*\*When persistent state, memory, or current reality is uncertain, the agent must preserve the uncertainty and verify before taking consequential action.\*\*



\---



\*\*Document Status:\*\* `v0.1 — Architecture Contract Draft`



\*\*Next Stage:\*\* Planning \& Reasoning Model



