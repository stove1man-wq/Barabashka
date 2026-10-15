# Decisions

Статусы: **Accepted** — обязательно к исполнению; **Proposed** — предложение, не правило, пока владелец не поставит Accepted; **Closed** — потеряло актуальность.

## D-001 — Baseline model for the current prototype
- **Status:** Accepted
- **Decision:** Continue testing with the locally installed Ollama model `llama3.1:8b`; postpone troubleshooting larger models.
- **Rationale:** This model has already returned a successful response through the local API.

## D-002 — Android Emulator endpoint
- **Status:** Accepted (connectivity reported working by the owner, 2026-10-12)
- **Decision:** The Unity Android build targets Ollama on the development PC through `http://10.0.2.2:11434/api/generate`.
- **Rationale:** In the standard Android Emulator, `10.0.2.2` is the special alias for the host machine.
- **Note:** The owner reports the APK in the emulator reaches Ollama and works. No logs are stored in the repository, and what fixed the earlier hang (cleartext HTTP setting, rebuild, other) is not recorded. Owner to add it to `DEVELOPMENT_LOG.md`.

## D-003 — Keep Ollama network exposure restricted
- **Status:** Accepted
- **Decision:** Do not expose Ollama to the public internet as a connectivity workaround.
- **Rationale:** The development API should be reachable only from the intended development environment/network.

## D-004 — Preserve the known-good APK while diagnosing
- **Status:** Closed (2026-10-12)
- **Original decision:** Do not rebuild or change Unity settings until evidence indicates a source/build mismatch or a code/manifest issue.
- **Why closed:** The connectivity problem is resolved, so the diagnostic freeze no longer applies. It also contradicted the development log, which prescribed a rebuild after changing the cleartext HTTP setting. Replaced by the rule in `AGENTS.md` §8: every APK build must be identifiable.

## D-005 — Target platform: Unity (C#)
- **Status:** Accepted (owner decision, 2026-10-12)
- **Decision:** The application is developed in Unity 6000.6.5f1 with C#. Android Studio is used for the Android Emulator, `adb` and diagnostics only.
- **Supersedes:** the native Kotlin / Jetpack Compose / Ktor / Filament-SceneView specification in `android_shell_architecture_v0.1.md` (sections 2 and 5), `android_modules_mapping_v0.1.md`, `android_network_bridge_v0.1.md`, `android_3d_renderer_v0.1.md`, and section 6 of `777.txt`. These files stay in the repository with a status banner. System-role requirements in the shell document (assistant role, overlay, foreground service, emergency STOP in the UI) remain requirements, not implementation choices.
- **Consequences:**
  1. Layers: Core (pure C# assembly without `UnityEngine`, unit-testable) / Model adapter / Unity shell (UI, avatar) / Android bridge (native plugin in `Assets/Plugins/Android`).
  2. Capabilities that need Android system APIs (calendar, contacts, intents, runtime permissions) go through the Android bridge. Native code lives only there.
  3. `AvatarRenderAdapter` (see `avatar_layer_architecture_v0.1.md`) is implemented in Unity. Filament/SceneView is dropped.
  4. The Android `requestPermissions` call named in the short safety contract is made through the Unity API or the plugin; check the documentation for the exact Unity version.
- **Open risks (to verify, not assumed):**
  - calling Calendar and Intent APIs from Unity, and the runtime-permission flow, — spike M1 in `NEXT_STEPS.md`;
  - foreground service, overlay (`SYSTEM_ALERT_WINDOW`) and the assistant role with a Unity activity — feasibility unknown. Fallback if infeasible: a thin native shell hosting Unity as a library. Do not start this work before M1 is closed.
- **Record:** the architecture review (`docs/ARCHITECT_REVIEW.md`) recommended a native Kotlin app. The owner chose Unity; the review's analysis stays as the record of the trade-offs.

## D-006 — Document status and source-of-truth rules
- **Status:** Items 1, 2 and 4 Accepted (needed to remove the contradictions the owner asked to remove, 2026-10-12). Item 3 Proposed: owner to confirm.
- **Decision:**
  1. Priority: `PROJECT_STATUS.md` (facts) → `DECISIONS.md` → `NEXT_STEPS.md` → `docs/architecture/GLOSSARY.md` (terms) → contracts (target design).
  2. Contract statuses: `Draft` → `Approved` → `Superseded`. A contract counts as Approved only with an owner note in this file.
  3. *(Proposed)* Contracts change only when implementation reveals a defect or a decision is recorded here. No new architecture documents until Slice 1 (`NEXT_STEPS.md`, M2) is verified.
  4. Overlapping documents have one owner (see `docs/DOCUMENT_MAP.md`). For safety and permissions the normative document is `independent_android_ai_agent_safety_permission_contract_v0.1.md`; the short `safety_permission_contract_v0.1.md` is an overview. Term conflicts are resolved by `GLOSSARY.md`.

## D-007 — Model access only through an adapter
- **Status:** Proposed
- **Decision:** UI code never contains the model endpoint or provider details. The prototype gets a small model-client interface now, with Ollama as one implementation, matching the Model Adapter Contract.
- **Rationale:** The project principle "Model Independence" and the local-first direction: the PC-hosted Ollama is a development tool, and an on-device or cloud provider must be addable without touching the agent core.

## D-008 — Refinement of D-003 for connectivity testing
- **Status:** Proposed
- **Decision:** For the standard Android Emulator, keep Ollama bound to loopback: the alias `10.0.2.2` forwards to the host's loopback, so `0.0.0.0` is not required. For a physical phone, prefer `adb reverse tcp:11434 tcp:11434` with `http://localhost:11434` over opening the port to the local network. Fall back to a LAN address only with a firewall rule limited to the local subnet.
- **Rationale:** Smaller exposure for the same result. Verify on the actual setup before relying on it.

## D-009 — Repository layout: Unity project inside this repository
- **Status:** Proposed
- **Decision:** The Unity project lives in `unity/` in this repository (one workspace root for both agents and for documentation). `Library/`, `Temp/`, `Logs/`, `Builds/`, `UserSettings/` and `*.apk` are excluded by the root `.gitignore`. Large binary assets (e.g. avatar models) will need Git LFS; decide when they appear.
- **Rationale:** At the moment the only working artifact, the Unity project, is outside version control, and the rule "update GitHub at each step" cannot be met for code. One root also removes the question of which folder's `AGENTS.md` the agent reads.
- **Alternative:** a separate repository for the Unity project and this one for documentation (two roots, two sets of rules).

## D-010 — Roles: architect and executors
- **Status:** Accepted (owner statement, 2026-10-12)
- **Decision:** The architect is a senior model in a separate chat (Claude). Cline and Continue in VS Code are executors under `AGENTS.md`. The architect designs, writes task cards and reviews reports; executors implement and verify. Architectural decisions are recorded as `Proposed` and accepted only by the owner. An executor does not make architectural choices; when a task is unclear or conflicts with the rules it asks one question.
- **Consequences:** `.clinerules` ("IT architect" role) conflicts with this and should be removed or reduced to a pointer to `AGENTS.md`. The architect sees only GitHub and what the owner pastes, so each step ends with a commit and push (confirmed by the owner) and a report in the `AGENTS.md` §12 format. Details: `docs/agent/ROLE_ARCHITECT.md`.

## D-011 — Link between the VS Code agent and Unity
- **Status:** Proposed
- **Decision:** Three feedback levels (`docs/agent/UNITY_BRIDGE.md`): L0 files plus the owner pasting Console errors (now); L1 `Editor.log` reading and EditMode tests in batchmode from the terminal (milestone M0.2); L2 the official Unity MCP (beta) only after M0, read-only first, by the owner's separate decision.
- **Rationale:** Milestones M0–M2 are plain C# (Core) and tests, which L0 and L1 cover. L2 gives the agent control of the editor, needs Unity Cloud and an AI subscription (terms unverified) and is not needed for the core.
- **Open:** exact batchmode flags and Unity Test Framework setup for 6000.6.5f1 are unverified; Cline/Continue compatibility with the Unity MCP relay is unverified.
