using Barabashka.Contracts;

namespace Barabashka.Core.Settings
{
    public sealed class SecurityPage : ISettingsPage
    {
        public string Id => "security";
        public string Title => "Действия агента";
        public string IconId => "shield";
        public int Order => 30;
        public string[] Aliases => new[] { "безопасность", "действия", "звонки", "смс", "разрешения" };

        public void Build(ISettingsView view)
        {
            view.Section("Режим действий агента");
            view.Hint("Как агент выполняет действия вне приложения (звонки, SMS, открытие приложений).");
            view.Choice(
                "Действия агента",
                new[] { "Вручную: открываю готовое, нажимаете вы", "Спрашиваю «да/нет» и делаю сам", "Полный цикл: делаю сразу" },
                () => (int)AppSettings.Actions,
                v => { AppSettings.Actions = (ActionLevel)v; return AppSettings.ActionsTitle(AppSettings.Actions); });
        }

        public bool TryVoice(string normalized, out string reply)
        {
            reply = null;
            if (string.IsNullOrEmpty(normalized))
                return false;

            // Команды управления действиями агента
            if (normalized.Contains("действия агента") || normalized.Contains("режим действий"))
            {
                reply = "Режим действий агента: вручную";
                return true;
            }

            if (normalized.Contains("включи звонки") || normalized.Contains("разрешить звонки"))
            {
                reply = "Разрешены звонки";
                return true;
            }

            return false;
        }
    }
}