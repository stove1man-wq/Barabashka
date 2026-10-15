# Independent Android AI Agent
## Model Adapter Contract v0.1

**Document type:** Architecture Contract  
**Version:** 0.1  
**Status:** Draft for architectural approval  
**Project:** Independent Android AI Agent

---

## 1. Purpose

Model Adapter Contract defines how AI/LLM models interact with the Agent without making the Agent dependent on a specific model, provider, runtime, or deployment location.

The fundamental architectural principle is:

> **The Agent Core owns the Goal. The Model assists the Agent.**

The model may help with:

- language understanding;
- ambiguity detection;
- planning suggestions;
- strategy suggestions;
- reasoning over available structured information;
- result summarization.

The model must not directly own:

- permissions;
- Safety Policy;
- capability authority;
- execution authority;
- verification authority;
- device control.

---

# 2. Model Independence

The Agent must be able to operate with different model providers.

Examples:

```text
Local model
 ├─ Ollama
 ├─ Android local inference
 └─ embedded model

Cloud model
 ├─ Provider A
 ├─ Provider B
 └─ future providers

Specialized model
 ├─ speech
 ├─ vision
 ├─ embeddings
 └─ other task-specific models
```

Agent Core must not contain provider-specific model logic.

Architecture:

```text
Agent Core
     ↓
Model Adapter Interface
     ↓
Model Provider
```

---

# 3. Local-First Model Philosophy

The project follows:

> **Local-first, not local-only.**

The Agent should prefer local models where they are sufficient and appropriate.

Cloud models may be used when:

- local model capability is insufficient;
- user permits network usage;
- task complexity justifies it;
- privacy policy permits it;
- required model capability is unavailable locally.

No single model provider should be a mandatory architectural dependency.

---

# 4. Model Adapter

Conceptual interface:

```text
ModelAdapter
 ├─ model_id
 ├─ provider_id
 ├─ version
 ├─ capabilities
 ├─ availability
 ├─ privacy_class
 ├─ context_limits
 ├─ input_schema
 ├─ output_schema
 ├─ generate()
 ├─ stream()
 ├─ structured_output()
 ├─ cancel()
 └─ health()
```

The exact implementation may differ by runtime.

---

# 5. Model Identity

Every model invocation should be attributable to:

```text
model_id
provider_id
model_version
adapter_version
```

Example:

```text
model_id = "local.llama"
provider_id = "ollama"
model_version = "x.y"
adapter_version = "0.1"
```

This supports:

- reproducibility;
- debugging;
- experience evaluation;
- audit;
- model replacement;
- compatibility management.

---

# 6. Model Capabilities

A model should declare capabilities rather than merely a name.

Possible capabilities:

```text
TEXT_GENERATION
STRUCTURED_OUTPUT
TOOL_REASONING
PLANNING
CLASSIFICATION
SUMMARIZATION
VISION
SPEECH
EMBEDDING
MULTILINGUAL
LONG_CONTEXT
STREAMING
```

Model capability does not imply Agent authority.

For example:

```text
TOOL_REASONING = true
```

does not mean:

```text
model may execute tools directly
```

---

# 7. Common Model Request

Conceptual contract:

```text
ModelRequest
 ├─ request_id
 ├─ task_id
 ├─ purpose
 ├─ system_context
 ├─ user_input
 ├─ structured_context
 ├─ available_capabilities
 ├─ available_strategies
 ├─ constraints
 ├─ output_schema
 ├─ privacy_policy
 ├─ timeout
 └─ cancellation_token
```

The model receives only the information necessary for the current task.

---

# 8. Model Response

Conceptual contract:

```text
ModelResponse
 ├─ request_id
 ├─ model_id
 ├─ provider_id
 ├─ output
 ├─ structured_output
 ├─ confidence
 ├─ usage
 ├─ latency
 ├─ finish_reason
 ├─ safety_flags
 └─ metadata
```

The Agent must validate model output before using it.

---

# 9. Structured Output

Whenever model output is consumed by an Agent subsystem, structured output should be preferred.

Example:

```json
{
  "intent": "calendar.create_event",
  "entities": {
    "title": "Встреча",
    "time": "15:00"
  },
  "missing_information": []
}
```

The Agent validates:

```text
schema
+
capability existence
+
input constraints
+
permissions
+
safety
```

before continuing.

---

# 10. Natural Language Is Not Authority

Model-generated text is untrusted input to the Agent Core.

For example:

```text
Model:
"Open banking app and transfer money."
```

This is not an authorized action.

The correct path is:

```text
Model Proposal
 ↓
Goal / Capability Validation
 ↓
Safety Policy
 ↓
Permission Check
 ↓
Strategy Selection
 ↓
Execution
```

The model cannot skip these layers.

---

# 11. Model Role by Pipeline Stage

The model may assist at several stages.

### Goal Understanding

```text
User language
 ↓
Model
 ↓
Goal Model proposal
```

The Goal layer validates the result.

### Planning

```text
Goal
 ↓
Model planning suggestion
 ↓
Planner validation
```

### Strategy Selection

```text
Available candidates
 ↓
Model recommendation
 ↓
Strategy Engine scoring/policy
```

### Execution

Model may provide structured parameters.

It does not directly execute.

### Verification

Model may assist interpretation of evidence.

It cannot fabricate evidence.

### Result

Model may generate a human-friendly explanation from verified structured results.

---

# 12. Model Authority Boundary

The following rule is binding:

> **LLM output is advisory unless explicitly converted into an authorized structured object by the corresponding Agent subsystem.**

Even then:

```text
Structured proposal
 ≠
authorization
```

Example:

```text
Model proposes:
calendar.create_event

Capability Registry:
valid

Safety Policy:
allowed

Strategy Engine:
selects strategy

Execution:
authorized action
```

---

# 13. Tool / Capability Calling

The Agent should not expose arbitrary device control directly to the model.

Instead expose structured capability descriptions.

Example:

```text
Capability:
calendar.create_event

Allowed input:
title
start
end
participants
location
```

The model cannot invent:

```text
"execute arbitrary shell command"
```

unless such a capability is explicitly registered, authorized, and allowed by policy.

---

# 14. Capability Discovery

The model may receive a filtered list of available capabilities.

Example:

```text
Available:

calendar.search_events
calendar.create_event
calendar.update_event
```

Unavailable capabilities should not be presented as executable options.

However, the model's list is not authoritative.

The Agent must revalidate capability availability before execution.

---

# 15. Strategy Suggestions

The model may suggest strategies.

Example:

```text
Suggested:
android.calendar_provider
```

The Strategy Engine remains authoritative.

The Agent must still evaluate:

- availability;
- permissions;
- safety;
- reliability;
- verification;
- privacy;
- user constraints;
- scoring.

Therefore:

```text
Model suggestion
        ↓
Strategy Engine
        ↓
validated selection
```

---

# 16. Model Routing

The Agent may support multiple models.

Conceptual routing:

```text
Task
 ↓
Model Requirement
 ↓
Model Registry
 ↓
Candidate Models
 ↓
Policy / Privacy / Capability Filtering
 ↓
Model Selection
 ↓
Model Adapter
```

Possible selection factors:

- task complexity;
- latency;
- privacy;
- local availability;
- context size;
- structured output support;
- vision/speech support;
- resource cost;
- network availability.

---

# 17. Model Profiles

Initial conceptual profiles:

```text
LOCAL_FAST
LOCAL_REASONING
CLOUD_GENERAL
CLOUD_REASONING
VISION
SPEECH
EMBEDDING
```

Profiles are logical roles, not hard-coded provider names.

This allows model replacement without changing Agent Core.

---

# 18. Model Selection vs Strategy Selection

These are different decisions.

### Model Selection

```text
Which model should help reason about the task?
```

### Strategy Selection

```text
Which mechanism should perform the task?
```

Example:

```text
Model:
local reasoning model

Strategy:
Android Calendar Provider

Provider:
Android Calendar
```

The model and execution strategy remain independent.

---

# 19. Model Failure

Model failures must be explicit.

Possible states:

```text
MODEL_UNAVAILABLE
MODEL_TIMEOUT
MODEL_OVERLOADED
MODEL_INVALID_OUTPUT
MODEL_SCHEMA_ERROR
MODEL_CONTEXT_LIMIT
MODEL_NETWORK_ERROR
MODEL_AUTH_ERROR
MODEL_POLICY_BLOCKED
MODEL_CANCELLED
MODEL_UNKNOWN
```

A model failure does not automatically mean Task failure.

Possible response:

```text
Model A unavailable
 ↓
Model B
```

or:

```text
model unavailable
 ↓
deterministic local logic
```

where feasible.

---

# 20. Model Fallback

Fallback may occur between:

- models;
- providers;
- local/cloud;
- specialized/general models.

Example:

```text
Cloud reasoning model
 ↓
network unavailable
 ↓
local reasoning model
```

Before fallback, the Agent should reconsider:

- privacy;
- context availability;
- capability requirements;
- model quality;
- task risk.

Fallback must not silently violate user constraints.

---

# 21. Privacy Boundary

Model routing must respect privacy policy.

Possible data classes:

```text
PUBLIC
NON_SENSITIVE
PRIVATE
SENSITIVE
HIGHLY_SENSITIVE
```

Each model/provider declares allowed data classes.

Example:

```text
Private medical document
        ↓
cloud model not permitted
        ↓
local model
```

The model layer must not decide its own privacy permissions.

---

# 22. Context Minimization

Only necessary context should be sent to a model.

Avoid sending:

- entire device state;
- unrelated conversations;
- unrelated files;
- complete calendar;
- complete contact database;
- unnecessary personal information.

Preferred:

```text
Goal
+
relevant context
+
required capability information
```

This supports:

> **Privacy by Architecture.**

---

# 23. Context Assembly

Context should be assembled by the Agent, not by arbitrary model-generated instructions.

Conceptual:

```text
Task Context Builder
 ├─ Goal
 ├─ Constraints
 ├─ relevant state
 ├─ capability information
 ├─ strategy candidates
 ├─ verification requirements
 └─ safety constraints
        ↓
Model Adapter
```

The model cannot expand its own context scope without Agent authorization.

---

# 24. Context Window Management

The Agent should track:

```text
max_context_tokens
current_context_size
reserved_output_tokens
available_budget
```

If context exceeds limits:

```text
compress
summarize
retrieve relevant context
switch model
re-plan
```

The model should not silently receive truncated critical information.

---

# 25. Streaming

Streaming may be supported for:

- voice interaction;
- text generation;
- user-facing progress;
- long responses.

Streaming output must still pass through validation before causing any external side effect.

Partial model output must never trigger partial execution accidentally.

---

# 26. Cancellation

Model requests must support cancellation where possible.

Flow:

```text
STOP
 ↓
Task cancellation
 ↓
cancel model request
 ↓
invalidate model output
```

If a late model response arrives after cancellation:

```text
discard as stale
```

It must not initiate new execution.

---

# 27. Model Output Staleness

Every model response should be bound to:

```text
task_id
plan_version
request_id
cancellation_token
```

Before applying output:

```text
task current?
plan current?
request current?
cancelled?
```

If not:

```text
IGNORE RESPONSE
```

This is analogous to stale-plan protection in Execution.

---

# 28. Model Confidence

Model confidence may be recorded, but must not be treated as factual truth.

Example:

```text
model_confidence = 0.93
```

does not mean:

```text
goal interpretation is objectively correct
```

Confidence is one signal.

Critical decisions must rely on:

```text
structured validation
+
policy
+
observable state
```

---

# 29. Ambiguity

The model may identify ambiguity.

Example:

```text
"Создай встречу с Александром."
```

Potential ambiguity:

```text
multiple contacts named Александр
```

The model should return structured ambiguity:

```text
needs_clarification = true
ambiguity_type = "multiple_matching_contacts"
```

The Agent then decides whether clarification is necessary.

The model must not silently choose a potentially wrong target when policy requires clarification.

---

# 30. Hallucination Boundary

Model-generated facts must be treated as unverified unless supported by authoritative sources.

Examples:

```text
Model says:
"User has a meeting at 15:00."

```

This is not evidence.

Correct:

```text
Calendar query
 ↓
actual event
```

The model may interpret evidence but cannot create evidence.

---

# 31. Prompt Injection Protection

External content supplied to the model may contain malicious instructions.

Examples:

- websites;
- emails;
- documents;
- messages;
- application text.

Such content must be explicitly marked as:

```text
UNTRUSTED_DATA
```

and must not override:

- system policy;
- Goal;
- user constraints;
- permissions;
- Safety Policy;
- capability authority.

---

# 32. Model Isolation

Model prompts should clearly separate:

```text
AUTHORITATIVE_AGENT_INSTRUCTIONS
USER_GOAL
STRUCTURED_STATE
UNTRUSTED_EXTERNAL_DATA
```

The model must not be allowed to reinterpret untrusted data as higher-priority instructions.

---

# 33. Deterministic Logic vs LLM

The project should use deterministic logic where deterministic logic is sufficient.

Examples:

```text
permission check → deterministic
capability availability → deterministic
safety policy → deterministic
strategy scoring → deterministic policy layer
task state machine → deterministic
cancellation → deterministic
verification state → policy + evidence
```

LLM is most useful for:

```text
natural language understanding
ambiguity interpretation
complex planning assistance
semantic transformation
human-facing explanation
```

This reduces unnecessary model dependence.

---

# 34. Model Independence Invariant

Agent Core must not contain:

```text
if model == provider_X
```

logic for core behavior.

Provider-specific behavior belongs inside adapters.

Architecture:

```text
Agent Core
    ↓
Model Adapter
    ↓
Provider-specific implementation
```

---

# 35. Model Registry

A Model Registry may contain:

```text
Model
 ├─ model_id
 ├─ provider_id
 ├─ version
 ├─ capabilities
 ├─ context_limit
 ├─ latency_profile
 ├─ resource_cost
 ├─ privacy_class
 ├─ availability
 └─ adapter_id
```

The registry allows dynamic model replacement.

---

# 36. Health and Availability

Model availability states:

```text
AVAILABLE
UNAVAILABLE
LOADING
OVERLOADED
REQUIRES_NETWORK
REQUIRES_AUTH
BLOCKED_BY_POLICY
UNKNOWN
```

Availability should be checked before invocation where practical.

---

# 37. Local Model Lifecycle

For local models:

```text
DISCOVER
 ↓
LOAD
 ↓
HEALTH CHECK
 ↓
READY
 ↓
INFERENCE
 ↓
UNLOAD / KEEP WARM
```

Resource limits should be respected.

The Agent may choose a smaller model when:

- memory is limited;
- battery is low;
- device is thermally constrained;
- latency requirements are strict.

---

# 38. Cloud Model Lifecycle

For cloud models:

```text
availability
 ↓
network check
 ↓
privacy check
 ↓
authentication
 ↓
request
 ↓
response
```

Cloud usage should be explicit in policy and observable where relevant.

---

# 39. Model Cost

Model cost may include:

```text
latency
network
battery
CPU/GPU
RAM
monetary cost
privacy cost
```

Model selection may optimize these factors.

However:

> Model optimization must never override safety or privacy policy.

---

# 40. Model Selection Contract

Conceptual:

```text
ModelCandidate
 ├─ model_id
 ├─ provider_id
 ├─ capability_fit
 ├─ availability
 ├─ privacy_fit
 ├─ latency
 ├─ resource_cost
 ├─ quality_estimate
 └─ score
```

Selection pipeline:

```text
Candidate Models
 ↓
Hard constraints
 ↓
Privacy / Policy
 ↓
Capability compatibility
 ↓
Scoring
 ↓
Selection
```

This mirrors Strategy Selection but remains a separate subsystem.

---

# 41. Model Selection and Experience

Experience may record:

```text
task type
model
environment
quality
latency
failure
structured-output reliability
```

Future model selection can use this information.

Experience cannot override:

- privacy;
- safety;
- permissions;
- availability.

---

# 42. Central Knowledge Integration

Central Knowledge may provide:

- model compatibility;
- known model versions;
- performance observations;
- recommended routing;
- provider changes.

Remote information remains advisory.

The local Model Registry and Policy determine whether a model may actually be used.

---

# 43. Model Replacement

Replacing a model must not require redesigning:

- Goal Model;
- Capability Registry;
- Strategy Engine;
- Safety Policy;
- Execution Engine;
- Verification Engine;
- Task State Machine.

Only the Model Adapter / Registry configuration should normally change.

---

# 44. Multiple Models in One Task

A Task may use more than one model.

Example:

```text
Model A:
Goal understanding

Model B:
planning

Model C:
result explanation
```

MVP should keep orchestration simple.

Any multi-model chain must preserve:

- task identity;
- plan version;
- privacy constraints;
- cancellation;
- output validation.

---

# 45. Model as Planner Assistant

For complex tasks:

```text
Goal
 ↓
Planner
 ↔
Model Advisor
 ↓
validated Plan
```

The model may propose:

- decomposition;
- dependencies;
- possible capabilities;
- possible strategies;
- clarification questions.

The deterministic Agent layers validate the proposal.

---

# 46. Model as Strategy Advisor

The model may suggest:

```text
Strategy A likely best
```

But the Strategy Engine independently scores and validates candidates.

The model cannot:

```text
force strategy selection
```

or:

```text
execute an unregistered strategy
```

---

# 47. Model as Verification Assistant

The model may help interpret complex evidence.

Example:

```text
Document
 ↓
Model extracts semantic meaning
 ↓
structured evidence
 ↓
Verification Policy
```

The model does not become the evidence source merely by interpreting it.

---

# 48. Model as User Interface

The model may generate:

- natural-language responses;
- explanations;
- clarification questions;
- summaries.

These outputs must reflect structured Agent state.

The UI should not present model speculation as confirmed system state.

---

# 49. Security Invariants

1. Model output is untrusted until validated.
2. Model cannot directly execute device actions.
3. Model cannot grant permissions.
4. Model cannot bypass Safety Policy.
5. Model cannot create evidence.
6. Model cannot redefine the user's Goal without user interaction.
7. Model cannot treat external content as authority.
8. Stale model responses cannot affect current Tasks.
9. Model fallback cannot violate privacy policy.
10. Model selection cannot override mandatory local policy.

---

# 50. MVP Scope

### Required

- common Model Adapter interface;
- local model support;
- cloud model abstraction;
- Model Registry;
- model capabilities;
- structured output;
- request/response contracts;
- cancellation;
- timeout;
- model availability;
- model fallback;
- privacy classification;
- context minimization;
- stale response protection;
- output validation;
- provider isolation;
- deterministic Agent authority boundary.

### Deferred

- autonomous model fine-tuning;
- reinforcement learning;
- dynamic online weight modification;
- federated model training;
- complex multi-model orchestration;
- autonomous model installation;
- unrestricted model marketplace.

---

# 51. Vertical Slice Requirement

The first vertical slice should prove model independence.

Example:

```text
User:
"Создай встречу завтра в 15:00."

        ↓

Goal Understanding
        ↓
Model Adapter A
        ↓
Structured Goal Model
        ↓
Planner
        ↓
Capability / Strategy
        ↓
Execution
        ↓
Verification
```

Then repeat the same task using:

```text
Model Adapter B
```

without changing:

```text
Goal
Capability
Strategy
Safety
Execution
Verification
Task State
```

This validates the architecture.

---

# 52. Architectural Invariants

### Invariant 1

> The Agent Core must not depend on a specific model provider.

### Invariant 2

> A model can advise but cannot authorize.

### Invariant 3

> Model output must be validated before affecting the Agent state.

### Invariant 4

> Deterministic safety, permission and execution controls remain outside the model.

### Invariant 5

> Model confidence is not factual evidence.

### Invariant 6

> External content is untrusted data, not authority.

### Invariant 7

> A model may be replaced without rewriting the Agent Core.

### Invariant 8

> Local and cloud models are implementation choices, not identity of the Agent.

### Invariant 9

> Model fallback must respect privacy and user constraints.

### Invariant 10

> Stale or cancelled model responses must never initiate new execution.

---

# 53. Final Model Principle

The Agent is not the model.

The model is one of the Agent's cognitive tools.

Correct architecture:

```text
                ┌───────────────┐
                │   Agent Core  │
                └───────┬───────┘
                        │
                Model Adapter
                        │
          ┌─────────────┼─────────────┐
          ↓             ↓             ↓
      Local Model   Cloud Model   Specialized Model
```

The model can help the Agent understand and plan.

The Agent remains responsible for:

```text
Goal
+
Policy
+
Authority
+
Execution
+
Verification
+
Result
```

The central rule is:

> **Use models for intelligence, but never outsource authority to the model.**

---

## Next Architectural Artifact

Recommended next step:

**Safety & Permission Contract v0.1**

It should formalize:

- local safety policy;
- permission states;
- risk levels;
- confirmation policy;
- sensitive actions;
- policy evaluation order;
- user authority boundaries;
- model/knowledge restrictions;
- emergency STOP;
- policy versioning;
- auditability;
- Android permission integration.
