namespace Barabashka.Voice
{
    /// <summary>
    /// State of the new voice interaction flow. This contract has no dependency on
    /// any recognizer, model, microphone implementation, or speech synthesizer.
    /// </summary>
    public enum VoiceSessionState
    {
        Idle = 0,
        Listening = 1,
        Processing = 2,
        Speaking = 3,
        Stopped = 4,
        Faulted = 5
    }
}
