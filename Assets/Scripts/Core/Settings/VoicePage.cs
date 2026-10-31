using Barabashka.Contracts;

namespace Barabashka.Core.Settings
{
    /// <summary>
    /// Страница «Озвучивание ответов».
    /// </summary>
    public sealed class VoicePage : ISettingsPage
    {
        public string Id => "voice";
        public string Title => "Озвучка";
        public string IconId => "volume";
        public int Order => 10;
        public string[] Aliases => new[] { "голос", "озвучка", "озвучивание", "речь" };

        public void Build(ISettingsView view)
        {
            view.Section("Озвучивание ответов");
            view.Hint("На голосовые вопросы агент отвечает голосом. Напоминания и таймеры озвучиваются всегда.");
            view.Toggle(
                "Озвучивать ответы",
                () => AppSettings.SpeakReplies,
                v => { AppSettings.SpeakReplies = v; return v ? "Озвучка включена" : "Озвучка выключена" }); 
        }

        public bool TryVoice(string normalized, out string reply)
        {
            reply = null;
            if (string.IsNullOrEmpty(normalized))
                return false;

            // Команды включения/выключения озвучки
            if (normalized.Contains("включи озвучку") || normalized.Contains("включить озвучку"))
            {
                reply = "Озвучка включена";
                return true;
            }
            
            if (normalized.Contains("выключи озвучку") || normalized.Contains("выключить озвучку"))
            {
                reply = "Озвучка выключена";
                return true;
            }
            
            return false;
        }
    }
}