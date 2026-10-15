# Android Integration Architecture v0.1

- **Status:** Prototype implemented; Android-to-host connectivity reported working by the owner (2026-10-12)
- **Version:** 0.1
- **Updated:** 2026-10-12
- **Platform:** Unity 6000.6.5f1, C# (D-005)
- **Scope:** First practical path from the Unity prototype to an Ollama model hosted on the development PC.

## 1. Purpose

Prove a narrow text request/response path from the Unity Android app to a model hosted by Ollama on the development PC. Autonomous planning, device control, and broad permissions are out of scope for this milestone.

## 2. Current implementation and observed state

- Unity project (local path): `C:\Users\max\Documents\Projects\IndependentAgentUnitySpike\IndependentAgentUnitySpike` (planned: `unity/` in this repository, D-009)
- UI/model-client prototype script: `Assets/Scripts/AgentPrototypeUI.cs`
- Model: `llama3.1:8b`
- Android API endpoint configured in the script:
  ```csharp
  private const string OllamaUrl = "http://10.0.2.2:11434/api/generate";
  ```
- The APK was built and launched in Android Emulator; the UI is visible. Vulkan was removed from the graphics API list and OpenGLES3 retained after Vulkan caused a black screen.
- Windows-local Ollama API is verified (2026-10-10): `localhost:11434/api/tags` and `127.0.0.1:11434/api/tags` return model data, and a non-streaming generation request returned `OLLAMA_OK`.
- **Reported by the owner (2026-10-12):** the app in the emulator reaches Ollama and works. No logs are stored; the cause of the earlier hang is not recorded. Cleartext HTTP in Unity is controlled by **Allow downloads over HTTP** in Player Settings (logcat on 2026-10-10 showed Unity rejecting insecure HTTP).

## 3. Request path

```text
Unity UI in Android Emulator
  |
  | HTTP POST /api/generate
  | http://10.0.2.2:11434/api/generate
  v
Android Emulator host alias (10.0.2.2)
  |
  v
Windows host networking / firewall
  |
  v
Ollama API on port 11434
  |
  v
llama3.1:8b
```

For the standard Android Emulator, `10.0.2.2` is the host machine alias. For a physical phone use `adb reverse tcp:11434 tcp:11434` with `http://localhost:11434`, or the PC's LAN address (see D-008). The earlier step-by-step diagnostic sequence is kept in `DEVELOPMENT_LOG.md` as history.

## 4. Responsibilities

- **Unity UI:** collect a prompt, show progress and result, allow user-requested cancellation.
- **Model client:** own endpoint configuration, request serialization, HTTP transport, timeouts, response parsing, and error reporting (D-007: separate from the UI).
- **Ollama:** host the selected model and return generated text.
- **Future agent coordinator (Core):** interpret model output and request narrowly defined tools only through explicit, validated interfaces.
- **Future Android bridge:** expose only approved capabilities, with permissions and user confirmation where appropriate.

## 5. Safety and trust boundaries

- Model output is untrusted text, not executable authority.
- Do not execute shell commands, arbitrary code, or Android actions directly from generated text.
- Future tools must use a small allowlist, typed parameters, validation, and clear success/failure results.
- Ask for user confirmation before consequential or destructive actions.
- Keep credentials and private data out of logs.
- Do not expose the Ollama endpoint to the public internet as a shortcut for connectivity.
- Distinguish "model generated a suggestion" from "the system performed an action."

## 6. Acceptance criteria for the connectivity milestone

- [x] APK builds and launches in Android Emulator.
- [x] Unity UI is visible in the emulator.
- [x] Unity source is configured to call `http://10.0.2.2:11434/api/generate`.
- [x] Windows-local Ollama API responds.
- [x] The emulator can reach Ollama on the host at port 11434 (owner report, 2026-10-12).
- [x] The Android app receives and displays a real model response (owner report, 2026-10-12).
- [ ] The connection setup is restricted to the intended development environment (not verified; see D-008).

## 7. Out of scope

- Fully autonomous operation.
- Arbitrary device control.
- General-purpose tool execution.
- On-device inference selection or optimization.
- Production security model and app distribution.
- Claims of persistent memory or multi-step task completion without an implementation and tests.
