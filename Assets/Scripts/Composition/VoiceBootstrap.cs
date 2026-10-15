using UnityEngine;

namespace Barabashka.Composition
{
    /// <summary>
    /// Voice integration is intentionally disabled.
    /// The existing Unity interface is the baseline; a new voice module will be designed separately.
    /// This placeholder must not initialize speech recognizers, download models, request microphone access,
    /// or create background objects during application startup.
    /// </summary>
    public sealed class VoiceBootstrap : MonoBehaviour
    {
        // Deliberately empty. Voice functionality will be implemented as a new stage.
    }
}
