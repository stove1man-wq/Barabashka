using Barabashka.Contracts;

namespace Barabashka.Core.Settings
{
    public sealed class ChatPage : ISettingsPage
    {
        public string Id => "chat";
        public string Title => "Чат";
        public string IconId => "chat";
        public int Order => 40;
        public string[] Aliases => new[] { "чат", "история", "корзина", "сообщения" };

        public void Build(ISettingsView view)
        {
            view.Section("История чата");
            view.Hint("Как долго хранить переписку на устройстве.");
            view.Choice(
                "Хранить историю",
                new[] { "Не хранить", "Неделю", "Всегда" },
                () => (int)AppSettings.ChatHistory,
                v => { AppSettings.ChatHistory = (ChatKeep)v; return AppSettings.ChatKeepTitle(AppSettings.ChatHistory); });

            view.Section("Смахнутые сообщения");
            view.Hint("Куда деваются сообщения, которые вы смахиваете влево.");
            view.Toggle(
                "В корзину на неделю",
                () => AppSettings.ChatTrash,
                v => { AppSettings.ChatTrash = v; return AppSettings.ChatTrashTitle(v); });
        }

        public bool TryVoice(string normalized, out string reply)
        {
            reply = null; return false;
        }
    }
}