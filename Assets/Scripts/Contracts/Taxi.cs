namespace Barabashka.Contracts
{
    /// <summary>Точка на карте: название для человека и координаты.</summary>
    public sealed class TaxiPlace
    {
        public string Title;
        public double Lat;
        public double Lon;
    }

    /// <summary>
    /// Оператор такси (приложение на телефоне). Агент только готовит поездку: открывает приложение с маршрутом,
    /// а «Заказать» нажимает человек (деньги и поездка - всегда решение человека).
    /// Реализации лежат в сборке Barabashka.TaxiProviders; новый оператор = новый класс и строка в реестре.
    /// </summary>
    public interface ITaxiProvider
    {
        /// <summary>Идентификатор: yandex-go, citymobil, maxim, indrive.</summary>
        string Id { get; }

        /// <summary>Название для человека («Яндекс Go»).</summary>
        string DisplayName { get; }

        /// <summary>Названия и имена пакетов, по которым приложение ищется среди установленных.</summary>
        string[] AppNames { get; }

        /// <summary>Умеет принять маршрут ссылкой. Иначе открывается только приложение, адрес вводит человек.</summary>
        bool SupportsRoute { get; }

        /// <summary>Пакет, которому адресуется ссылка маршрута (пусто - любому обработчику ссылки).</summary>
        string RoutePackage { get; }

        /// <summary>Ссылка на готовый маршрут до точки (откуда - текущее место). null, если оператор так не умеет.</summary>
        string BuildRouteUrl(TaxiPlace to);
    }
}
