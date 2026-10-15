using System;
using System.Threading;
using System.Threading.Tasks;

namespace Barabashka.Voice
{
    /// <summary>
    /// Platform-specific implementations are added separately. The UI and agent
    /// depend on this contract rather than on a speech engine.
    /// </summary>
    public interface IVoiceInputProvider : IDisposable
    {
        event Action<VoiceInputResult> ResultReceived;
        event Action<string> ErrorOccurred;

        Task StartAsync(string language, CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }
}
