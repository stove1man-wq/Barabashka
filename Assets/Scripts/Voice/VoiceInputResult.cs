namespace Barabashka.Voice
{
    /// <summary>
    /// One finalized transcription produced by a future voice input provider.
    /// </summary>
    public sealed class VoiceInputResult
    {
        public string Text { get; }
        public string Language { get; }
        public bool IsFinal { get; }

        public VoiceInputResult(string text, string language, bool isFinal = true)
        {
            Text = text ?? string.Empty;
            Language = string.IsNullOrWhiteSpace(language) ? "auto" : language;
            IsFinal = isFinal;
        }
    }
}
