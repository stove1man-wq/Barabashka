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
            reply = null;
            if (string.IsNullOrEmpty(normalized))
                return false;

            // «включи / выключи корзину»
            if (normalized.Contains("корзин"))
            {
                bool off = normalized.Contains("выключ") || normalized.Contains("отключ");
                bool on = normalized.Contains("включ");
                if (on == off) return false;
                AppSettings.ChatTrash = on;
                reply = AppSettings.ChatTrashTitle(on);
                return true;
            }

            // «хранить историю неделю / всегда», «не хранить историю» (порядок как в списке на странице: 0, 1, 2)
            if (normalized.Contains("истори"))
            {
                ChatKeep keep;
                if (normalized.Contains("не хранить")) keep = (ChatKeep)0;
                else if (normalized.Contains("недел")) keep = (ChatKeep)1;
                else if (normalized.Contains("всегда")) keep = (ChatKeep)2;
                else return false;
                AppSettings.ChatHistory = keep;
                reply = AppSettings.ChatKeepTitle(keep);
                return true;
            }

            return false;
        }
    }
}