using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Core
{
    /// <summary>
    /// Поиск адреса на карте (OpenStreetMap Nominatim): текст адреса -> координаты. Сеть необязательна:
    /// без неё (или если адрес не нашёлся) возвращается null, и такси открывается без готового маршрута.
    /// Если задан город по умолчанию (погода), он подставляется в запрос и смещает поиск к городу.
    /// Наружу уходит только текст адреса, без имени и телефона.
    /// </summary>
    public static class AgentGeocoder
    {
        private const string UserAgent = "Barabashka/1.0 github.com/stove1man-wq/Barabashka";

        [Serializable] public sealed class Item { public string lat; public string lon; public string display_name; }
        [Serializable] public sealed class Reply { public Item[] items; }

        /// <summary>Находит адрес; null, если не нашёлся или нет сети. Ошибки сети не выбрасываются.</summary>
        public static async Task<TaxiPlace> FindAsync(string query, CancellationToken ct)
        {
            string clean = (query ?? string.Empty).Trim();
            if (clean.Length < 3) return null;

            string search = clean;
            string bias = string.Empty;
            AgentWeather.City city = AgentWeather.DefaultCity();
            if (city != null)
            {
                if (clean.IndexOf(city.Name, StringComparison.OrdinalIgnoreCase) < 0) search = clean + ", " + city.Name;
                const double span = 0.6;
                bias = "&viewbox=" + Num(city.Lon - span) + "," + Num(city.Lat + span) + "," + Num(city.Lon + span) + "," + Num(city.Lat - span);
            }

            try
            {
                string url = "https://nominatim.openstreetmap.org/search?q=" + Uri.EscapeDataString(search) +
                             "&format=json&limit=1&accept-language=ru" + bias;
                string json = await SimpleHttp.GetAsync(url, 10, ct, UserAgent);
                Reply reply = JsonUtility.FromJson<Reply>("{\"items\":" + json + "}");
                if (reply == null || reply.items == null || reply.items.Length == 0) return null;

                Item item = reply.items[0];
                double lat, lon;
                if (!double.TryParse(item.lat, NumberStyles.Float, CultureInfo.InvariantCulture, out lat) ||
                    !double.TryParse(item.lon, NumberStyles.Float, CultureInfo.InvariantCulture, out lon)) return null;
                return new TaxiPlace { Title = ShortTitle(item.display_name, clean), Lat = lat, Lon = lon };
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Geocoder] " + ex.Message);
                return null;
            }
        }

        // «Тверская улица, 1, Москва, Россия» -> первые три части.
        private static string ShortTitle(string displayName, string fallback)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return fallback;
            string[] parts = displayName.Split(',');
            int take = Math.Min(3, parts.Length);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < take; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(parts[i].Trim());
            }
            return sb.ToString();
        }

        private static string Num(double value)
        {
            return value.ToString("0.0000", CultureInfo.InvariantCulture);
        }
    }
}
