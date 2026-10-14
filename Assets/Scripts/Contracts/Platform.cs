using System;
using System.Collections.Generic;

namespace Barabashka.Contracts
{
    /// <summary>Сетевой слой. Если его нет, приложение работает офлайн.</summary>
    public interface INetworkService
    {
        bool IsOnline { get; }
        event Action<bool> OnOnlineChanged;
    }

    /// <summary>Манифест языкового пакета (читается из manifest.json в папке пакета).</summary>
    [Serializable]
    public sealed class LanguagePackManifest
    {
        public string LanguageCode;         // "ru"
        public string DisplayName;          // "Русский"
        public string Version;              // "1.0.0"
        public string License;              // лицензия пакета и входящих моделей
        public string IntentsFile;          // "intents.json" - фразы для правил
        public string ResponsesFile;        // "responses.json" - шаблоны ответов
        public string RecognizerModelPath;  // необязательно: модель распознавания под язык
        public string VoiceId;              // необязательно: голос озвучки под язык
    }

    public interface ILanguagePackProvider
    {
        IReadOnlyList<LanguagePackManifest> Installed { get; }
        bool TryGet(string languageCode, out LanguagePackManifest manifest);
    }

    public enum CapabilityTier { Basic = 0, Standard = 1, Advanced = 2 }

    /// <summary>
    /// Профиль уровня: какие реализации включены. Это единственное, что отличает
    /// базовый, средний и высокий уровень (и, позже, тарифы).
    /// Значения - идентификаторы реализаций (Id у IBrain, ISpeechRecognizer, ITextToSpeech).
    /// </summary>
    [Serializable]
    public sealed class CapabilityProfile
    {
        public CapabilityTier Tier = CapabilityTier.Basic;
        public string BrainId = "isolated";
        public string RecognizerId = "null";
        public string TtsId = "null";
        public string[] ToolNames = new string[0];
        public bool AllowNetwork = false;
        public string DefaultLanguage = "auto";
    }
}
