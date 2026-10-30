using Barabashka.Contracts;
using Barabashka.TaxiProviders;

namespace Barabashka.Core.Settings
{
    /// <summary>Страница «Такси»: оператор по умолчанию и адреса дома и работы.</summary>
    public sealed class TaxiPage : ISettingsPage
    {
        public string Id => "taxi";
        public string Title => "Такси";
        public string IconId => "taxi";
        public int Order => 25;
        public string[] Aliases => new[] { "такси", "яндекс го", "адреса", "дом", "работа" };

        public void Build(ISettingsView view)
        {
            view.Section("Оператор такси");
            view.Hint("Какое приложение открывать. «Авто» берёт первое найденное на телефоне. Заказ всегда подтверждаете вы.");
            string[] options = new string[TaxiProviderRegistry.All.Count + 1];
            options[0] = "Оператор: авто";
            for (int i = 0; i < TaxiProviderRegistry.All.Count; i++) options[i + 1] = "Оператор: " + TaxiProviderRegistry.All[i].DisplayName;
            view.Choice(
                "Оператор",
                options,
                () => OperatorIndex(),
                v => SetOperator(v));

            view.Section("Адреса");
            view.Hint("Для команд «такси домой» и «такси на работу». Координаты находятся при первом вызове (нужна сеть), дальше хранятся на телефоне.");
            view.Input("Дом", "Тверская 1, Москва", false,
                () => TaxiSettings.GetAddress(TaxiSettings.Home),
                v => SetAddress(TaxiSettings.Home, v));
            view.Input("Работа", "Ленина 5, Москва", false,
                () => TaxiSettings.GetAddress(TaxiSettings.Work),
                v => SetAddress(TaxiSettings.Work, v));
        }

        public bool TryVoice(string normalized, out string reply)
        {
            reply = null;
            return false; // голосовое управление страницей - вместе с остальными страницами
        }

        private static int OperatorIndex()
        {
            string current = TaxiSettings.Operator;
            for (int i = 0; i < TaxiProviderRegistry.All.Count; i++)
                if (TaxiProviderRegistry.All[i].Id == current) return i + 1;
            return 0;
        }

        private static string SetOperator(int index)
        {
            if (index <= 0 || index > TaxiProviderRegistry.All.Count)
            {
                TaxiSettings.Operator = TaxiSettings.Auto;
                return "Оператор такси: авто";
            }
            ITaxiProvider provider = TaxiProviderRegistry.All[index - 1];
            TaxiSettings.Operator = provider.Id;
            return "Оператор такси: " + provider.DisplayName;
        }

        private static string SetAddress(string slot, string value)
        {
            string text = (value ?? string.Empty).Trim();
            TaxiSettings.SetAddress(slot, text);
            string title = TaxiSettings.SlotTitle(slot);
            return text.Length == 0 ? "Адрес удалён: " + title.ToLowerInvariant() : title + ": " + text;
        }
    }
}
