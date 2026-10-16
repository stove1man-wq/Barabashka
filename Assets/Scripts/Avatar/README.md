# Barabashka avatar prototype

This branch adds a first runtime-generated visual prototype at
`Assets/Scripts/Avatar/BarabashkaAvatarPrototype.cs`.

## Scope

- Creates a stylized glowing blue/violet spirit from Unity primitives.
- Adds large eyes, a signature luminous curl, a mouth, arms, and small glow specks.
- Animates idle floating, blinking, head movement, and simple state-specific reactions.
- Exposes `SetState(AvatarState)` so a future event bridge can drive the character.

## Safe integration

This is intentionally additive. It does **not** replace `AvatarVisualizer`, change existing scenes, or connect to production agent events. Attach the component to an empty GameObject in a disposable test scene to review the appearance before deciding whether to integrate it.

Example state call:

```csharp
avatar.SetState(BarabashkaAvatarPrototype.AvatarState.Listening);
avatar.SetState(BarabashkaAvatarPrototype.AvatarState.Thinking);
avatar.SetState(BarabashkaAvatarPrototype.AvatarState.Speaking);
```

## Known prototype limitations

- This is a procedural placeholder, not the final artist-authored rigged 3D asset.
- Speaking mouth motion is illustrative; it is not yet synchronized to phonemes or audio amplitude.
- No project scenes, prefabs, materials, or agent logic are changed.
- Unity compilation and Android performance have not yet been verified in the local Unity editor.

## Next implementation step

Review this prototype in Unity, then replace primitive geometry with a modeled/rigged character while preserving the public state interface. Connect state changes through an event bridge and implement the three adaptive UI modes separately.
