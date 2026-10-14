using System;
using Barabashka.Contracts;

namespace Barabashka.App
{
    /// <summary>Канал ввода "микрофон": превращает события распознавателя в реплики.</summary>
    public sealed class VoiceInputChannel : IInputChannel
    {
        private readonly ISpeechRecognizer _recognizer;

        public VoiceInputChannel(ISpeechRecognizer recognizer)
        {
            if (recognizer == null) throw new ArgumentNullException("recognizer");
            _recognizer = recognizer;
            _recognizer.OnPartial += Raise;
            _recognizer.OnFinal += Raise;
        }

        public string Id { get { return "voice"; } }
        public event Action<Utterance> OnUtterance;

        public void Start() { _recognizer.StartListening(); }
        public void Stop() { _recognizer.StopListening(); }

        private void Raise(Utterance utterance)
        {
            Action<Utterance> handler = OnUtterance;
            if (handler != null) handler(utterance);
        }
    }
}
