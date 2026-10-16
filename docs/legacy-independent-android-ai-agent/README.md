# independent-android-ai-agent

Documentation, architecture, specifications, decisions, and development history for an independent Android AI agent. The prototype is built in Unity (C#); see D-005.

## Project navigation

- [Agent rules](AGENTS.md) — rules for Cline and Continue.
- [Project status](PROJECT_STATUS.md) — latest verified state.
- [Next steps](NEXT_STEPS.md) — current stage and acceptance criteria.
- [Decisions](DECISIONS.md) — accepted and proposed technical choices.
- [Development log](DEVELOPMENT_LOG.md) — dated work and test results.
- [Android integration architecture v0.1](docs/architecture/android_integration_architecture_v0.1.md) — the implemented prototype and the Android-to-Ollama request path.
- [Glossary](docs/architecture/GLOSSARY.md) — single vocabulary for the contracts.
- [Document map](docs/DOCUMENT_MAP.md) — what each document is, its role and status.
- [Architecture review](docs/ARCHITECT_REVIEW.md) — findings and trade-offs (2026-10-12).
- [VS Code agent setup](docs/agent/SETUP_VSCODE.md) and [Architect role](docs/agent/ROLE_ARCHITECT.md).
- [Agent ↔ Unity link](docs/agent/UNITY_BRIDGE.md).

## Working principle

Separate verified implementation from proposals and open questions. A model response is not evidence that an action was executed. Update the status and development log when milestones are demonstrated and tested; record decisions with their rationale. Change one variable at a time.
