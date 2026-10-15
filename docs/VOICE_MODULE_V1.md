# Voice module v1 — clean implementation

## Baseline

- Preserve the existing Unity interface.
- Treat the previous voice implementation as out of scope; do not repair, reuse, or depend on it.
- New voice functionality lives behind provider-neutral contracts in `Assets/Scripts/Voice`.
- No automatic microphone access, model downloads, or background startup work.

## Contracts added

- `VoiceSessionState`: UI-facing lifecycle states.
- `VoiceInputResult`: finalized or partial transcription data.
- `IVoiceInputProvider`: asynchronous start/stop, transcription events, and errors.
- `IVoiceOutputProvider`: asynchronous speech and cancellation.

These are contracts only. They intentionally contain no engine, model, native plugin, or platform-specific code.

## Runtime flow

1. The user explicitly activates voice input in the existing UI.
2. A platform provider requests microphone/speech permission only at that point.
3. The provider emits transcription results; the application submits final text to the agent.
4. The agent response is passed to a separately selected output provider.
5. Stop/cancel tears down the active operation and returns the session to Idle/Stopped.

## Implementation order

1. Choose the first target platform and use its supported speech APIs.
2. Implement and test one input provider.
3. Connect final transcription to the existing agent/UI through an adapter.
4. Implement speech output separately.
5. Add explicit start/stop and permission/error feedback to the existing UI.
6. Verify on the target device before adding another platform.

## Constraints

- Keep UI, agent logic, and platform voice providers separate.
- No dependency on the previous voice pipeline.
- No silent permission requests or automatic listening at app launch.
- Keep provider selection explicit; do not silently fall back to a different engine.
