using System;
using System.Collections.Generic;
using System.Globalization;
using Barabashka.Contracts;

namespace Barabashka.TaxiProviders
{
    /// <summary>
    /// Яндекс Go. Официальная ссылка маршрута принимает только координаты точки назначения и открывает построенный маршрут;
    /// заказ подтверждает человек. Если приложения нет, ссылка ведёт в магазин (см. документацию Яндекса «Продвинутое формирование ссылок на заказ»).
    /// </summary>
    public sealed class YandexGoProvider : ITaxiProvider
    {
        private const string TrackingId = "1178268795219780156";

        public string Id { get { return "yandex-go"; } }
        public string DisplayName { get { return "Яндекс Go"; } }
        public string[] AppNames { get { return new[] { "яндекс го", "yandex go", "яндекс такси", "ru.yandex.taxi" }; } }
        public bool SupportsRoute { get { return true; } }
        public string RoutePackage { get { return "ru.yandex.taxi"; } }

        public string BuildRouteUrl(TaxiPlace to)
        {
            if (to == null) return null;
            return "https://3.redirect.appmetrica.yandex.com/route?end-lat=" + Number(to.Lat) +
                   "&end-lon=" + Number(to.Lon) + "&lang=ru&appmetrica_tracking_id=" + TrackingId;
        }

        private static string Number(double value)
        {
            return value.ToString("0.000000", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Оператор без своей ссылки маршрута: открывается приложение, адрес человек вводит сам (мы кладём его в буфер).</summary>
    public sealed class AppOnlyTaxiProvider : ITaxiProvider
    {
        private readonly string _id;
        private readonly string _name;
        private readonly string[] _appNames;

        public AppOnlyTaxiProvider(string id, string displayName, params string[] appNames)
        {
            _id = id;
            _name = displayName;
            _appNames = appNames;
        }

        public string Id { get { return _id; } }
        public string DisplayName { get { return _name; } }
        public string[] AppNames { get { return _appNames; } }
        public bool SupportsRoute { get { return false; } }
        public string RoutePackage { get { return string.Empty; } }
        public string BuildRouteUrl(TaxiPlace to) { return null; }
    }

    /// <summary>Операторы по порядку предпочтения для режима «авто» (первый из установленных на телефоне).</summary>
    public static class TaxiProviderRegistry
    {
        private static readonly List<ITaxiProvider> Providers = new List<ITaxiProvider>
        {
            new YandexGoProvider(),
            new AppOnlyTaxiProvider("citymobil", "Ситимобил", "ситимобил", "citymobil"),
            new AppOnlyTaxiProvider("maxim", "Максим", "максим", "maxim"),
            new AppOnlyTaxiProvider("indrive", "inDrive", "индрайв", "indrive", "indriver")
        };

        public static IReadOnlyList<ITaxiProvider> All { get { return Providers; } }

        public static ITaxiProvider GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (ITaxiProvider p in Providers)
                if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }
    }
}
