\# Independent Android AI Agent



\## Provider \& Tool Contract v0.1



\*\*Document:\*\* `provider\_tool\_contract\_v0.1.md`

\*\*Version:\*\* 0.1

\*\*Status:\*\* Architecture Contract Draft

\*\*Project:\*\* Independent Android AI Agent



\---



\# 1. Purpose



This document defines the architectural contract for \*\*providers and tools\*\* used by the Independent Android AI Agent.



The purpose is to establish a unified and controlled interface between the agent's reasoning/execution layers and external capabilities.



The agent must not depend directly on a single implementation of:



\* an LLM;

\* an Android control mechanism;

\* a network service;

\* a local model;

\* a cloud model;

\* a storage provider;

\* a search provider;

\* an application-specific integration.



Instead, capabilities must be exposed through explicit provider/tool contracts.



\---



\# 2. Core Principle



The agent should reason in terms of:



```text

capability

```



rather than:



```text

specific implementation

```



For example:



```text

"send a message"

```



must not inherently mean:



```text

tap coordinates X,Y

```



It may be implemented through:



```text

Android API

Accessibility API

UI Automation

application API

other approved mechanism

```



The execution layer selects an appropriate implementation according to capability, availability, safety and policy.



\---



\# 3. Provider vs Tool



The architecture distinguishes two concepts.



\## Provider



A \*\*Provider\*\* supplies a class of capabilities.



Examples:



```text

LLM Provider

Android Provider

Storage Provider

Search Provider

Speech Provider

Vision Provider

```



\## Tool



A \*\*Tool\*\* is a concrete callable capability exposed by a provider.



Examples:



```text

android.open\_app

android.read\_screen

android.click\_element

android.type\_text



llm.generate

llm.stream



storage.read

storage.write



speech.transcribe

```



Relationship:



```text

Provider

&#x20;  ↓

Tools

&#x20;  ↓

Execution Transaction

```



\---



\# 4. Provider Abstraction



Every provider must expose a stable interface independent of its implementation.



Conceptually:



```text

Provider

&#x20;├── identity

&#x20;├── capabilities

&#x20;├── availability

&#x20;├── health

&#x20;├── configuration

&#x20;└── tools

```



The agent must be able to determine what a provider can do before invoking it.



\---



\# 5. Provider Identity



Every provider must have a unique logical identifier.



Example:



```text

provider\_id:

&#x20;   android.accessibility



provider\_id:

&#x20;   llm.ollama



provider\_id:

&#x20;   llm.openai

```



The logical ID must remain stable even if the implementation changes.



\---



\# 6. Provider Metadata



A provider should expose:



```text

provider\_id

provider\_type

version

capabilities

availability

health

required\_permissions

execution\_constraints

configuration\_status

```



Example:



```text

Provider:

&#x20;   id: android.accessibility

&#x20;   type: android\_control

&#x20;   version: 0.1

&#x20;   availability: AVAILABLE

```



\---



\# 7. Capability Model



Capabilities are the primary abstraction.



Examples:



```text

READ\_SCREEN

CLICK\_ELEMENT

TYPE\_TEXT

OPEN\_APPLICATION

READ\_APPLICATION\_STATE

EXECUTE\_ANDROID\_ACTION



GENERATE\_TEXT

ANALYZE\_TEXT

GENERATE\_PLAN



TRANSCRIBE\_AUDIO

SYNTHESIZE\_SPEECH



READ\_LOCAL\_DATA

WRITE\_LOCAL\_DATA

```



A provider may implement one or more capabilities.



\---



\# 8. Multiple Providers



A capability may be implemented by multiple providers.



Example:



```text

Capability:

&#x20;   TEXT\_GENERATION



Providers:

&#x20;   Ollama

&#x20;   Cloud LLM A

&#x20;   Cloud LLM B

```



The architecture must allow switching providers without changing the higher-level agent logic.



This supports:



\* local-first execution;

\* offline operation;

\* provider failure;

\* cost control;

\* performance optimization;

\* privacy policies;

\* future extensibility.



\---



\# 9. Provider Selection



Provider selection must not be based solely on model preference.



The selection process should consider:



```text

capability required

&#x20;       ↓

available providers

&#x20;       ↓

device capabilities

&#x20;       ↓

permissions

&#x20;       ↓

network state

&#x20;       ↓

security policy

&#x20;       ↓

privacy policy

&#x20;       ↓

cost constraints

&#x20;       ↓

performance

&#x20;       ↓

provider health

&#x20;       ↓

selection

```



\---



\# 10. Local-First Principle



Where the required capability is available locally, the architecture should prefer local execution when consistent with:



\* required quality;

\* latency;

\* device resources;

\* privacy policy;

\* task requirements.



Example:



```text

No Internet

&#x20;   ↓

Local LLM available

&#x20;   ↓

continue execution

```



If no local provider can satisfy the requirement:



```text

Local provider unavailable

&#x20;   ↓

determine whether remote provider is permitted

&#x20;   ↓

if permitted:

&#x20;   use remote provider

else:

&#x20;   fail gracefully

```



\---



\# 11. Offline Operation



Offline operation is a first-class architectural requirement.



Providers must explicitly declare whether they require:



```text

ONLINE

OFFLINE

OPTIONAL\_NETWORK

```



Example:



```text

llm.ollama:

&#x20;   network\_requirement = OFFLINE\_CAPABLE



cloud.llm:

&#x20;   network\_requirement = ONLINE\_REQUIRED

```



The agent must not discover network requirements only after execution begins.



\---



\# 12. Tool Contract



Every tool must expose a predictable contract.



Conceptually:



```text

Tool {

&#x20;   tool\_id

&#x20;   provider\_id

&#x20;   version



&#x20;   input\_schema

&#x20;   output\_schema



&#x20;   required\_capabilities

&#x20;   required\_permissions



&#x20;   risk\_level

&#x20;   timeout



&#x20;   idempotency

&#x20;   side\_effects



&#x20;   availability

}

```



\---



\# 13. Tool Invocation



A tool invocation must contain enough information to be independently understood.



Conceptually:



```text

ToolCall {

&#x20;   call\_id

&#x20;   transaction\_id

&#x20;   tool\_id

&#x20;   input

&#x20;   requested\_at

}

```



The tool must never rely on hidden assumptions about the caller's state.



\---



\# 14. Tool Result



A tool must return a structured result.



Conceptually:



```text

ToolResult {

&#x20;   call\_id

&#x20;   status

&#x20;   output

&#x20;   observations

&#x20;   error

&#x20;   metadata

}

```



Possible statuses:



```text

SUCCESS

FAILED

TIMEOUT

DENIED

UNAVAILABLE

INVALID\_INPUT

UNKNOWN

```



\---



\# 15. No Raw Tool Side Effects



The LLM must not directly invoke arbitrary external side effects.



Instead:



```text

LLM

&#x20;↓

proposed tool call

&#x20;↓

policy validation

&#x20;↓

tool authorization

&#x20;↓

execution transaction

&#x20;↓

tool

```



The tool layer is therefore a security and execution boundary.



\---



\# 16. Tool Authorization



Before executing a tool, the system must validate:



```text

tool exists

AND

provider available

AND

required capability available

AND

permissions satisfied

AND

transaction permits operation

AND

policy permits operation

```



If any critical condition fails:



```text

tool execution blocked

```



\---



\# 17. Tool Risk Levels



Tools should declare a risk level.



Minimum conceptual levels:



```text

READ\_ONLY

LOW

MEDIUM

HIGH

CRITICAL

```



Examples:



```text

read\_screen        → READ\_ONLY

open\_application   → LOW

change\_setting     → MEDIUM

send\_message       → HIGH

delete\_data        → HIGH

financial\_action   → CRITICAL

```



Future contracts may formalize exact classifications.



\---



\# 18. Read-Only Tools



Read-only tools must not intentionally modify external state.



Examples:



```text

read\_screen

read\_notification

query\_application\_state

read\_file

inspect\_device\_state

```



These tools are preferred during:



\* verification;

\* recovery;

\* uncertainty resolution.



\---



\# 19. Side-Effect Declaration



Every tool capable of modifying external state must explicitly declare:



```text

has\_side\_effects = true

```



Examples:



```text

click

type\_text

send\_message

delete\_file

change\_setting

submit\_form

```



The transaction system must use this information when determining retry and recovery behavior.



\---



\# 20. Idempotency Declaration



Every tool must declare whether its operation is:



```text

IDEMPOTENT

NON\_IDEMPOTENT

UNKNOWN

```



If the property is unknown, the system must treat the operation conservatively as:



```text

NON\_IDEMPOTENT

```



This is particularly important when recovering from uncertain execution.



\---



\# 21. Tool Timeout



Every tool must have an execution timeout.



The timeout must be bounded and observable.



A timeout must return:



```text

TIMEOUT

```



rather than silently blocking the transaction indefinitely.



Timeout does not imply that the external action did not occur.



Therefore:



```text

TIMEOUT

&#x20;   ↓

OBSERVE

&#x20;   ↓

VERIFY

```



before any potentially duplicating retry.



\---



\# 22. Tool Health



Providers should expose health information.



Conceptual states:



```text

AVAILABLE

DEGRADED

UNAVAILABLE

UNKNOWN

```



Health must not be confused with task success.



A provider may be:



```text

AVAILABLE

```



while a specific tool invocation fails.



\---



\# 23. Capability Discovery



The agent should be able to query:



```text

What capabilities are currently available?

```



Example:



```text

Device:

&#x20;   microphone = false

&#x20;   camera = true

&#x20;   network = false



Available:

&#x20;   local\_llm

&#x20;   text\_input

&#x20;   screen\_reading



Unavailable:

&#x20;   cloud\_llm

&#x20;   speech\_input

```



This allows planning to adapt before execution.



\---



\# 24. Dynamic Availability



Capabilities may change during execution.



Examples:



```text

network disconnects

permission revoked

application closes

provider crashes

battery restrictions activate

hardware becomes unavailable

```



Therefore capability availability must be treated as dynamic state.



The transaction must revalidate critical capabilities when necessary.



\---



\# 25. Provider Failure



Provider failure must not automatically terminate the entire task.



Example:



```text

Cloud LLM

&#x20;   ↓

UNAVAILABLE

&#x20;   ↓

Local LLM available

&#x20;   ↓

switch provider

```



Provider switching must remain consistent with the current transaction and security policy.



\---



\# 26. Provider Switching



Provider switching is allowed only when:



1\. the replacement provider supports the required capability;

2\. the replacement provider satisfies policy;

3\. the transaction can safely continue;

4\. required state can be transferred;

5\. switching does not create duplicate side effects.



Provider switching must not silently change the user's intended objective.



\---



\# 27. LLM Provider Contract



LLM providers should expose a common logical interface.



Conceptually:



```text

generate()

stream()

estimate\_capabilities()

health()

```



Provider-specific implementation details remain below this interface.



Examples:



```text

llm.ollama

llm.cloud\_a

llm.cloud\_b

```



The agent should not need to know the internal API of each provider.



\---



\# 28. Android Provider Contract



Android capabilities should also be abstracted.



Conceptually:



```text

android.open\_app

android.read\_screen

android.find\_element

android.click

android.type

android.back

android.home

android.read\_state

```



These tools may be implemented through:



```text

official Android interfaces

Accessibility APIs

UI automation

application APIs

other approved mechanisms

```



The implementation must remain replaceable.



\---



\# 29. Preferred Android Control Order



When multiple mechanisms can accomplish the same operation, the architecture should prefer:



```text

1\. Official Android/API interface

2\. Application/API integration

3\. Accessibility interface

4\. UI automation

5\. Other fallback mechanisms

```



The exact order may be adjusted per capability.



The important principle is:



> Prefer semantic and stable interfaces over coordinate-based interaction.



\---



\# 30. Coordinate-Based Automation



Coordinate-based interaction should be considered a last-resort mechanism.



Example:



```text

tap(x=427, y=812)

```



is inherently fragile because:



\* screen layout can change;

\* device resolution can differ;

\* orientation can change;

\* accessibility settings can alter layout;

\* application updates can move controls.



Where possible, prefer:



```text

click(element\_id="send\_button")

```



over:



```text

click(x,y)

```



\---



\# 31. Tool Observations



Tools should return observations useful for verification.



Example:



```text

click(send\_button)

```



should ideally return:



```text

action accepted

target identified

target state

screen state

timestamp

```



This does not replace postcondition verification but improves it.



\---



\# 32. Tool Errors



Errors must be structured.



Conceptually:



```text

ToolError {

&#x20;   code

&#x20;   category

&#x20;   message

&#x20;   retryable

&#x20;   recoverable

}

```



Example:



```text

code: PERMISSION\_DENIED

category: AUTHORIZATION

retryable: false

recoverable: true

```



The LLM should not be required to infer all operational semantics from an unstructured error string.



\---



\# 33. Retryability



Tools should explicitly indicate whether an error is retryable.



Possible values:



```text

RETRYABLE

NON\_RETRYABLE

UNKNOWN

```



Unknown should be treated conservatively.



A retryable error still requires the transaction's retry policy to approve the retry.



\---



\# 34. Tool Composition



Complex tasks may require several tools.



Example:



```text

read\_screen

&#x20;   ↓

find\_element

&#x20;   ↓

click

&#x20;   ↓

observe

&#x20;   ↓

verify

```



Tool composition belongs to the execution plan.



A tool must not secretly invoke unrelated tools without declaring such behavior.



\---



\# 35. Hidden Side Effects



Providers must not hide additional externally meaningful side effects.



For example:



A tool described as:



```text

read\_contacts

```



must not silently:



```text

modify\_contacts

upload\_contacts

send\_contacts

```



without explicit declaration and authorization.



This is a core trust requirement.



\---



\# 36. Data Boundaries



Provider calls must declare the data they require.



For example:



```text

local\_llm:

&#x20;   input = local task context



cloud\_llm:

&#x20;   input = task context

&#x20;   network = required

```



Sensitive or private data must not be sent to remote providers unless explicitly permitted by policy.



\---



\# 37. Local/Remote Data Policy



The provider architecture must support policies such as:



```text

LOCAL\_ONLY

LOCAL\_PREFERRED

REMOTE\_ALLOWED

REMOTE\_REQUIRED

```



Example:



```text

private\_document\_analysis

&#x20;   → LOCAL\_ONLY

```



while:



```text

general\_information\_request

&#x20;   → LOCAL\_PREFERRED

```



may permit remote fallback.



\---



\# 38. Provider Configuration



Provider configuration must be separated from agent logic.



Examples:



```text

model name

endpoint

credentials

timeouts

resource limits

privacy mode

```



Changing a provider configuration must not require rewriting the execution engine.



\---



\# 39. Credentials



Credentials must never be embedded in:



\* LLM prompts;

\* transaction plans;

\* tool descriptions;

\* ordinary logs;

\* model-generated content.



Credential handling belongs to a dedicated security mechanism.



\---



\# 40. Logging



Provider/tool logs should record:



```text

timestamp

provider\_id

tool\_id

transaction\_id

call\_id

status

error\_code

duration

```



Logs must not unnecessarily contain sensitive payloads.



\---



\# 41. Deterministic Tool Identity



A tool identifier must uniquely identify a logical operation.



Example:



```text

android.read\_screen

android.click

android.type\_text

llm.generate

storage.read

```



Changing the implementation must not change the logical meaning of the tool.



\---



\# 42. Versioning



Providers and tools must be versioned independently.



Example:



```text

Provider:

&#x20;   android.accessibility v1



Tool:

&#x20;   android.click v1

```



Breaking changes require a version increment.



\---



\# 43. Backward Compatibility



The execution layer should remain compatible with older providers when their contract remains valid.



Provider-specific extensions must not break the core tool contract.



\---



\# 44. Provider Registration



Providers should be explicitly registered.



Conceptually:



```text

Provider Registry

&#x20;├── llm.ollama

&#x20;├── android.accessibility

&#x20;├── android.api

&#x20;└── storage.local

```



The registry should provide:



```text

discover

enable

disable

health

capabilities

```



\---



\# 45. Disabled Providers



A disabled provider must not be selected for execution.



Disabling a provider should not corrupt existing transaction records.



An active transaction using a disabled provider must enter an appropriate recovery or provider-switching path.



\---



\# 46. Provider Isolation



A provider failure should not automatically crash the entire agent.



Conceptually:



```text

Provider failure

&#x20;      ↓

contain failure

&#x20;      ↓

classify

&#x20;      ↓

retry / switch / recover / abort

```



This is especially important for external network providers.



\---



\# 47. Tool Invocation and Transactions



Every side-effecting tool call must belong to an execution transaction.



Conceptually:



```text

Transaction

&#x20;   ↓

ToolCall

&#x20;   ↓

Provider

&#x20;   ↓

External System

```



Read-only diagnostic tools may have more flexible invocation rules, but transaction association is still preferred.



\---



\# 48. No Direct External Control From Planning



The planner may produce:



```text

tool = android.click

target = send\_button

```



but must not directly execute it.



Instead:



```text

Planner

&#x20;  ↓

Tool Proposal

&#x20;  ↓

Policy

&#x20;  ↓

Transaction

&#x20;  ↓

Tool Executor

&#x20;  ↓

Provider

```



This maintains the separation between reasoning and execution.



\---



\# 49. Tool Proposal



A proposed tool invocation should contain:



```text

tool\_id

arguments

reason

expected\_result

risk\_level

```



The execution system may reject or modify the proposal if policy requires it.



\---



\# 50. Tool Result Verification



A successful tool response does not automatically mean the user's objective was achieved.



Example:



```text

Tool:

&#x20;   click(send)

Result:

&#x20;   SUCCESS

```



This means:



> the tool successfully performed the click.



It does not necessarily mean:



> the message was successfully sent.



The transaction's postcondition must still be verified.



\---



\# 51. Recovery Integration



When a tool returns:



```text

UNKNOWN

TIMEOUT

PARTIAL

```



the transaction should normally enter an observation/recovery path.



Example:



```text

Tool timeout

&#x20;   ↓

transaction = RECOVERY\_REQUIRED

&#x20;   ↓

observe external state

&#x20;   ↓

determine actual result

```



This directly integrates with the Execution Transaction Model.



\---



\# 52. Provider Fallback



Fallback should be explicit.



Example:



```text

Primary:

&#x20;   android.api



Fallback:

&#x20;   android.accessibility



Fallback:

&#x20;   ui\_automation

```



The fallback chain must be defined by capability and policy.



It must not be improvised during execution.



\---



\# 53. Fallback Safety



A fallback implementation must not silently change the semantics of the requested action.



For example:



```text

"read application state"

```



must not become:



```text

"guess application state from an unrelated visual signal"

```



without explicitly marking the confidence and verification limitations.



\---



\# 54. Capability Confidence



Providers may report confidence or quality information where appropriate.



Example:



```text

screen recognition:

&#x20;   confidence = 0.96

```



Confidence is an input to decision-making, not proof of correctness.



Critical actions must use stronger verification than model confidence alone.



\---



\# 55. Provider Resource Constraints



Providers may declare resource requirements:



```text

CPU

GPU

RAM

storage

network

battery

permissions

```



The planner may use these constraints when selecting a provider.



Example:



```text

large local model

&#x20;   ↓

insufficient RAM

&#x20;   ↓

provider unavailable

```



\---



\# 56. Hardware Awareness



Provider selection must integrate with the device capability model.



Example:



```text

microphone = false

```



must prevent selection of a speech-input provider requiring a microphone.



Likewise:



```text

network = false

```



must prevent selection of a cloud-only provider.



\---



\# 57. Provider Lifecycle



Providers may have lifecycle states:



```text

REGISTERED

INITIALIZING

READY

DEGRADED

STOPPING

STOPPED

FAILED

```



The exact implementation is future work.



\---



\# 58. Provider Shutdown



Provider shutdown must be safe.



Stopping a provider must not leave an active transaction in an undefined state.



If a provider is stopped during execution:



```text

transaction

&#x20;   ↓

provider unavailable

&#x20;   ↓

observe/recover/switch/abort

```



\---



\# 59. Security Boundary



The provider/tool layer is a security boundary between:



```text

agent reasoning

```



and:



```text

external side effects

```



No LLM-generated instruction may bypass this boundary.



\---



\# 60. Fundamental Invariants



The following invariants apply to the provider/tool architecture.



\### Invariant 1



Every tool has a defined contract.



\### Invariant 2



Every side-effecting tool call belongs to a transaction.



\### Invariant 3



Provider availability is explicit.



\### Invariant 4



Required permissions are explicit.



\### Invariant 5



Side effects are explicit.



\### Invariant 6



Idempotency is explicit or conservatively assumed unknown.



\### Invariant 7



Tool success does not equal task success.



\### Invariant 8



Timeout does not imply safe retry.



\### Invariant 9



Provider switching must preserve task semantics.



\### Invariant 10



The LLM cannot bypass the execution/policy boundary.



\---



\# 61. MVP Requirements



For the MVP, the provider/tool system must provide at minimum:



1\. provider abstraction;

2\. tool abstraction;

3\. capability discovery;

4\. provider availability;

5\. tool availability;

6\. structured tool input;

7\. structured tool output;

8\. permission declaration;

9\. risk declaration;

10\. side-effect declaration;

11\. idempotency declaration;

12\. timeout;

13\. structured errors;

14\. retryability information;

15\. transaction association;

16\. local/remote provider distinction;

17\. provider fallback mechanism;

18\. hardware capability awareness;

19\. provider isolation;

20\. LLM/executor separation.



\---



\# 62. Explicit Non-Goals for v0.1



This contract does not yet define:



\* concrete Kotlin interfaces;

\* concrete Android classes;

\* dependency injection framework;

\* provider marketplace;

\* plugin installation mechanism;

\* cloud credential implementation;

\* detailed permission/security policy;

\* database schema;

\* network protocol;

\* cryptographic provider identity;

\* distributed provider execution.



These belong to later contracts.



\---



\# 63. Architectural Summary



The intended architecture is:



```text

&#x20;                    ┌───────────────┐

&#x20;                    │      LLM      │

&#x20;                    └───────┬───────┘

&#x20;                            │

&#x20;                      Tool Proposal

&#x20;                            │

&#x20;                            ▼

&#x20;                   ┌─────────────────┐

&#x20;                   │ Policy / Guard  │

&#x20;                   └────────┬────────┘

&#x20;                            │

&#x20;                            ▼

&#x20;                   ┌─────────────────┐

&#x20;                   │   Transaction   │

&#x20;                   │    Executor     │

&#x20;                   └────────┬────────┘

&#x20;                            │

&#x20;                        Tool Call

&#x20;                            │

&#x20;                            ▼

&#x20;                   ┌─────────────────┐

&#x20;                   │  Provider Layer │

&#x20;                   └────────┬────────┘

&#x20;                            │

&#x20;             ┌──────────────┼──────────────┐

&#x20;             ▼              ▼              ▼

&#x20;        Android          Local LLM       Remote

&#x20;        Provider         Provider        Provider

&#x20;             │              │              │

&#x20;             └──────────────┼──────────────┘

&#x20;                            ▼

&#x20;                    External System

```



The critical architectural rule is:



> \*\*The model proposes; the transaction system authorizes and executes.\*\*



And:



> \*\*Providers implement capabilities; they do not define the agent's objective.\*\*



\---



\# 64. Final Contract Statement



The Independent Android AI Agent must remain independent of individual model, Android-control and service implementations.



Providers and tools therefore form an abstraction layer that allows the system to:



\* operate locally;

\* operate offline where possible;

\* switch providers;

\* tolerate provider failures;

\* select capabilities dynamically;

\* enforce permissions;

\* control side effects;

\* integrate Android through multiple mechanisms;

\* preserve transaction safety;

\* evolve without rewriting the core agent.



The most important architectural boundary is:



```text

LLM reasoning

&#x20;     ≠

tool authorization

&#x20;     ≠

tool execution

&#x20;     ≠

result verification

```



These responsibilities must remain separable.



\---



\*\*Status:\*\* `v0.1 — Architecture Contract Draft`



\*\*Next stage:\*\* only after this document is accepted and archived, proceed to the next architectural contract.



