# Independent Android AI Agent — Architecture Decisions Archive

## Status
This document records the agreed architectural decisions before implementation.

## 1. Design Principles v0.1

1. Goal over Command — the agent optimizes for achieving the user's goal, not merely literal command execution.
2. Result over Process — success means a verified result, not merely a successful action/API call.
3. Multiple Strategies — the agent considers multiple paths to the same goal.
4. Safe Autonomy — autonomy is bounded by local safety policy, permissions, and context.
5. Verification First — significant actions should be independently verified when possible.
6. Adaptive Re-planning — failure of one strategy can trigger another strategy.
7. Experience without Uncontrolled Self-Modification — experience may guide strategy selection but cannot bypass safety or permissions.
8. Local Independence — the agent remains useful without an external server or cloud model where local capabilities permit.
9. Optional Knowledge Network — external knowledge services may extend the agent but do not define it.
10. Model Independence — no specific LLM is foundational to the agent.
11. Capability Extensibility — capabilities can be added without rewriting Agent Core.
12. Human Control — the user can stop, constrain, redirect, or confirm meaningful actions.
13. Privacy by Architecture — minimize unnecessary data transfer.
14. Graceful Degradation — missing network/model/capability should degrade functionality rather than destroy the agent.
15. Evolution without Rewriting the Core — new models, capabilities, strategies and interfaces should plug into stable core contracts.
16. Language Independence — human language is separated from the language-neutral Goal Model.
17. Result Transparency — show operational status and results without exposing private chain-of-thought.
18. Peripheral Presence — the agent can remain subtly present and communicate status or important reminders.
19. Attention Respect — safety and current task have priority over secondary notifications.
20. The Agent Must Remain the Agent — external servers, models and services may amplify the agent but must not turn it into a dependent client.

### Added after Capability Architecture
21. Capability–Strategy Separation — a capability describes WHAT can be achieved; strategies describe HOW; providers describe WHO/WHERE supplies the mechanism.
22. Verification Capability — significant capabilities should expose an independent verification strategy where feasible.

## 2. Goal Achievement Architecture v0.1

Core flow:

User Goal
→ Goal Understanding
→ Explore Strategies
→ Strategy Search / Evaluation
→ Risk / Permission Gate
→ Execute
→ Verify Result
→ Success / Failure
→ Re-plan if needed

The agent is goal-oriented and may choose alternative strategies when the first path fails.

## 3. Interaction State Machine v0.1

Main states:

MINIMAL
→ INTERMEDIATE
→ LISTENING / TEXT INPUT
→ ACTIVE
→ WORKING
→ RESULT / NEED USER / RE-PLAN
→ MINIMAL

During WORKING:
- avatar becomes smaller and moves aside;
- task/work area becomes central;
- STOP AGENT appears simultaneously;
- STOP is a real execution-control signal, not merely a UI action;
- important reminders may appear as peripheral, unobtrusive avatar-adjacent information.

Priority:
Safety / STOP → current task → user request → important reminders → decorative animation.

STOP should interrupt future actions and attempt to cancel the active operation where technically possible. The architecture must distinguish cancellation from rollback of already-completed irreversible actions.

## 4. MVP Architecture v0.2 — Agreed Preliminary Decisions

### Offline autonomy
Decision: LOCAL-FIRST.

Local-first does not mean local-only.
Cloud resources may enhance complex tasks, but internet access is not a foundational dependency of the agent.

### Android control
Decision: SUPPORT BOTH PATHS.

Preferred order:
1. Native Android APIs / system interfaces
2. Official application interfaces / AppFunctions where available
3. Other structured integrations
4. UI automation as a fallback strategy
5. Human-in-the-loop where appropriate

UI automation is a legitimate execution strategy, but generally has higher risk and lower predictability.

Official interfaces are not automatically safe: every capability still passes through local Safety Policy.

### Models
Decision: REPLACEABLE LOCAL AND CLOUD MODELS.

Agent Core must not depend on where a model runs.
Models are accessed through a common Model Adapter.
A model proposes interpretation/plans; it does not directly bypass Safety or Execution layers.

### Development order
Decision: VERTICAL SLICES.

Each development stage should follow a real path from user input to verified result, rather than building all modules in isolation first.

Preferred pattern:
User input → Goal → Plan → Safety → Execute → Verify → Result.

## 5. Capability Architecture v0.1

Capability = a formally described ability to achieve a type of result.

Capability is NOT the same thing as an Android API.

Base conceptual contract:

Capability
- id
- name
- description
- category
- input schema
- output schema
- preconditions
- permissions
- risk level
- strategies
- availability
- verification strategy

### Important separation

WHAT = Capability
HOW = Strategy
WHO/WHERE = Provider

Example:

calendar.create_event
→ Android Calendar Provider
→ AppFunction
→ UI automation
→ human-assisted alternative

### Capability Registry
All capabilities are registered in a common registry. Agent Core does not hard-code knowledge of every capability.

### Availability states
A capability can exist but be unavailable in the current environment.

Possible states:
- AVAILABLE
- UNAVAILABLE
- BLOCKED
- REQUIRES_PERMISSION
- REQUIRES_USER
- TEMPORARILY_UNAVAILABLE
- UNKNOWN

### Risk
Initial conceptual levels:
- R0 — informational
- R1 — low impact
- R2 — user-impacting
- R3 — sensitive / consequential
- R4 — highly sensitive

Exact safety categories will be defined separately.

### Verification
Capability output is not proof of success.
Verification should independently confirm the desired result whenever feasible.

### Versioning
Capability contracts should be versioned so provider/implementation changes do not require Agent Core rewrites.

## 6. Proposed first vertical capability

Candidate first end-to-end capability:
calendar.create_event

Reason:
It exercises Goal Model, Capability Discovery, Strategy Selection, permissions, Execution, Verification, alternative strategy, and STOP.

This is a proposal for the first implementation slice, not yet a coding instruction.

## 7. Current Architecture Map

USER
→ Language / Interaction Layer
→ Goal Model
→ Planner
→ Strategy Engine
→ Capability Registry
→ Capability / Strategy / Provider
→ Safety Policy
→ Execution Engine
→ Verification
→ Result / Re-plan

Supporting components:
- Model Adapter
- Local State
- Experience Store
- Audit Log
- STOP Controller

## 8. Explicitly deferred

Not required before the architectural foundation is stable:
- large capability catalog;
- full self-learning/RL;
- central knowledge network;
- complex reminder ecosystem;
- marketplace;
- universal UI automation;
- multiple UI themes;
- large-scale multi-agent system.

The architecture should remain extensible toward these features.

## 9. Current status

Architecture discussion is still pre-code.
No implementation should begin until the agreed design artifacts and contracts are sufficiently stable.
Next planned artifact:
Capability Matrix MVP v0.1.
