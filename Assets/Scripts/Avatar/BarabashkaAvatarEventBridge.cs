using UnityEngine;

/// <summary>
/// Small adapter between agent/voice events and the avatar prototype.
/// Wire existing systems to these methods only after their real event points
/// have been identified. No polling, fake progress, or direct dependency on ASR/TTS.
/// </summary>
[DisallowMultipleComponent]
public sealed class BarabashkaAvatarEventBridge : MonoBehaviour
{
    [SerializeField] private BarabashkaAvatarPrototype avatar;
    [SerializeField] private AdaptiveAvatarLayout layout;

    public void OnAgentActivated()
    {
        if (layout != null) layout.SetMode(AdaptiveAvatarLayout.PresentationMode.Voice);
        SetState(BarabashkaAvatarPrototype.AvatarState.Idle);
    }

    public void OnListeningStarted() => SetState(BarabashkaAvatarPrototype.AvatarState.Listening);
    public void OnSpeechRecognitionStarted() => SetState(BarabashkaAvatarPrototype.AvatarState.Thinking);
    public void OnThinkingStarted() => SetState(BarabashkaAvatarPrototype.AvatarState.Thinking);
    public void OnTaskStarted() => SetState(BarabashkaAvatarPrototype.AvatarState.Thinking);
    public void OnSpeechPlaybackStarted() => SetState(BarabashkaAvatarPrototype.AvatarState.Speaking);
    public void OnTaskCompleted() => SetState(BarabashkaAvatarPrototype.AvatarState.Success);
    public void OnSpeechPlaybackCompleted() => SetState(BarabashkaAvatarPrototype.AvatarState.Idle);
    public void OnAgentError() => SetState(BarabashkaAvatarPrototype.AvatarState.Error);

    public void ShowIconMode()
    {
        if (layout != null) layout.SetMode(AdaptiveAvatarLayout.PresentationMode.Icon);
    }

    public void ShowVoiceMode()
    {
        if (layout != null) layout.SetMode(AdaptiveAvatarLayout.PresentationMode.Voice);
    }

    public void ShowWorkspaceMode()
    {
        if (layout != null) layout.SetMode(AdaptiveAvatarLayout.PresentationMode.Workspace);
    }

    private void SetState(BarabashkaAvatarPrototype.AvatarState state)
    {
        if (avatar != null) avatar.SetState(state);
    }
}
