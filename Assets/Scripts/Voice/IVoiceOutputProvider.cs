using System;
using System.Threading;
using System.Threading.Tasks;

namespace Barabashka.Voice
{
    /// <summary>
    /// Speech output abstraction. Implementations may use a platform-native
    /// synthesizer or another explicitly selected provider.
    /// </summary>
    public interface IVoiceOutputProvider : IDisposable
    {
        Task SpeakAsync(string text, string language, CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
    }
}
