# Barabashka avatar prototype

This branch adds an isolated first runtime-generated visual prototype and its mode/event adapters under `Assets/Scripts/Avatar/`.

## Components

- `BarabashkaAvatarPrototype.cs`: procedural placeholder spirit, idle motion, blinking, and state-specific reactions.
- `AdaptiveAvatarLayout.cs`: configurable smooth transitions between Icon, Voice, and Workspace presentation modes. Positions/scales are Inspector settings and must be calibrated in the target scene.
- `BarabashkaAvatarEventBridge.cs`: adapter methods for agent/voice events and mode switches. It does not subscribe to project events automatically.

## Safe integration

This is additive. It does **not** replace `AvatarVisualizer`, modify existing scenes, or connect itself to production agent events. Add the components to a disposable test scene first and wire references in the Inspector.

Example calls:

```csharp
avatar.SetState(BarabashkaAvatarPrototype.AvatarState.Listening);
avatar.SetState(BarabashkaAvatarPrototype.AvatarState.Thinking);
avatar.SetState(BarabashkaAvatarPrototype.AvatarState.Speaking);
layout.SetMode(AdaptiveAvatarLayout.PresentationMode.Workspace);
```

## Known limitations

- The model is procedural placeholder geometry, not the final artist-authored rigged 3D asset.
- Speaking mouth motion is illustrative and is not synchronized to phonemes or audio amplitude.
- The layout controller uses local Transform coordinates; it is not yet a Canvas/RectTransform layout and its positions must be tuned in the actual scene.
- Event bridge methods must be connected to verified event points in the existing voice/agent modules. Do not invoke them on a timer or fake task progress.
- No scene or existing production logic has been changed.
- Unity compilation and Android performance have not yet been verified in the local Unity editor.

## Next step

Open a disposable Unity test scene, attach the components, assign references, and review appearance and transitions. Then replace primitive geometry with a proper rigged asset and connect real event sources.
