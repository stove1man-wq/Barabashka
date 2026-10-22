using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Погода через Open-Meteo (бесплатно, без ключа): поиск города и прогноз на неделю.
/// Город по умолчанию хранится на устройстве. Текст ответа написан так, чтобы его было удобно и читать, и слушать.
/// </summary>
public static class AgentWeather
{
    private const string CityKey = "weather.city";
    private const string LatKey = "weather.lat";
    private const string LonKey = "weather.lon";

    [Serializable] public class GeoReply { public GeoItem[] results; }
    [Serializable] public class GeoItem { public string name; public double latitude; public double longitude; public string country; }
    [Serializable] public class ForecastReply { public CurrentBlock current; public DailyBlock daily; }
    [Serializable] public class CurrentBlock { public float temperature_2m; public float apparent_temperature; public int weather_code; public float wind_speed_10m; }
    [Serializable]
    public class DailyBlock
    {
        public string[] time;
        public int[] weather_code;
        public float[] temperature_2m_max;
        public float[] temperature_2m_min;
        public float[] precipitation_probability_max;
    }

    [Serializable] public class NominatimReply { public NominatimItem[] items; }
    [Serializable] public class NominatimItem { public string lat; public string lon; public string name; public string display_name; }

    // Ответ MET Norway (api.met.no): нужные поля.
    [Serializable] public class MetReply { public MetProps properties; }
    [Serializable] public class MetProps { public MetEntry[] timeseries; }
    [Serializable] public class MetEntry { public string time; public MetData data; }
    [Serializable] public class MetData { public MetInstant instant; public MetPeriod next_1_hours; public MetPeriod next_6_hours; }
    [Serializable] public class MetInstant { public MetDetails details; }
    [Serializable] public class MetDetails { public float air_temperature; public float wind_speed; }
    [Serializable] public class MetPeriod { public MetSummary summary; public MetPrecip details; }
    [Serializable] public class MetSummary { public string symbol_code; }
    [Serializable] public class MetPrecip { public float precipitation_amount; }

    private const string UserAgent = "Barabashka/1.0 github.com/stove1man-wq/Barabashka";

    public sealed class City
    {
        public string Name;
        public double Lat;
        public double Lon;
    }

    private static readonly string[] MonthsGenitive =
    {
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря"
    };

    public static City DefaultCity()
    {
        string name = PlayerPrefs.GetString(CityKey, string.Empty);
        if (string.IsNullOrEmpty(name)) return null;
        double lat, lon;
        if (!double.TryParse(PlayerPrefs.GetString(LatKey, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out lat) ||
            !double.TryParse(PlayerPrefs.GetString(LonKey, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out lon))
            return null;
        return new City { Name = name, Lat = lat, Lon = lon };
    }

    public static void SaveDefaultCity(City city)
    {
        PlayerPrefs.SetString(CityKey, city.Name);
        PlayerPrefs.SetString(LatKey, city.Lat.ToString("R", CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(LonKey, city.Lon.ToString("R", CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Ищет город. Из речи приходят падежные формы («в Москве»), поэтому пробуем слово как есть и без последних букв:
    /// поиск Open-Meteo нечёткий и находит город по началу названия.
    /// </summary>
    public static async Task<City> FindCityAsync(string query, CancellationToken ct)
    {
        // 1) встроенный список (работает без сети), 2) OpenStreetMap Nominatim, 3) Open-Meteo.
        City known = AgentCities.Find(query);
        if (known != null) return known;

        string failure = null;
        try
        {
            City osm = await FindCityNominatimAsync(query, ct);
            if (osm != null) return osm;
        }
        catch (HttpFailure ex) { failure = ex.Message; }

        try
        {
            return await FindCityOpenMeteoAsync(query, ct);
        }
        catch (HttpFailure ex)
        {
            throw new HttpFailure(failure != null ? failure : ex.Message);
        }
    }

    private static async Task<City> FindCityNominatimAsync(string query, CancellationToken ct)
    {
        string clean = (query ?? string.Empty).Trim();
        if (clean.Length < 2) return null;
        string url = "https://nominatim.openstreetmap.org/search?q=" + Uri.EscapeDataString(clean) +
                     "&format=json&limit=1&accept-language=ru";
        string json = await SimpleHttp.GetAsync(url, 10, ct, UserAgent);
        NominatimReply reply = JsonUtility.FromJson<NominatimReply>("{\"items\":" + json + "}");
        if (reply == null || reply.items == null || reply.items.Length == 0) return null;
        NominatimItem item = reply.items[0];
        double lat, lon;
        if (!double.TryParse(item.lat, NumberStyles.Float, CultureInfo.InvariantCulture, out lat) ||
            !double.TryParse(item.lon, NumberStyles.Float, CultureInfo.InvariantCulture, out lon)) return null;
        string name = !string.IsNullOrEmpty(item.name) ? item.name
            : (item.display_name ?? clean).Split(',')[0].Trim();
        return new City { Name = name, Lat = lat, Lon = lon };
    }

    private static async Task<City> FindCityOpenMeteoAsync(string query, CancellationToken ct)
    {
        string clean = (query ?? string.Empty).Trim();
        var attempts = new List<string>();
        if (clean.Length >= 2) attempts.Add(clean);
        if (clean.Length >= 5) attempts.Add(clean.Substring(0, clean.Length - 1));
        if (clean.Length >= 6) attempts.Add(clean.Substring(0, clean.Length - 2));

        foreach (string attempt in attempts)
        {
            string url = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(attempt) +
                         "&count=1&language=ru&format=json";
            string json = await SimpleHttp.GetAsync(url, 10, ct);
            GeoReply reply = JsonUtility.FromJson<GeoReply>(json);
            if (reply != null && reply.results != null && reply.results.Length > 0 && !string.IsNullOrEmpty(reply.results[0].name))
            {
                GeoItem item = reply.results[0];
                return new City { Name = item.name, Lat = item.latitude, Lon = item.longitude };
            }
        }
        return null;
    }

    /// <param name="day">today, tomorrow, day_after или week.</param>
    public static async Task<string> DescribeAsync(City city, string day, CancellationToken ct)
    {
        // Open-Meteo из России может быть недоступен без VPN, поэтому сначала MET Norway, затем Open-Meteo.
        ForecastReply forecast = null;
        string metFailure = null;
        try { forecast = await FetchMetAsync(city, ct); }
        catch (HttpFailure ex) { metFailure = ex.Message; }

        if (forecast == null)
        {
            try { forecast = await FetchOpenMeteoAsync(city, ct); }
            catch (HttpFailure ex)
            {
                throw new HttpFailure("погодные сервисы недоступны" + (metFailure != null ? " (" + metFailure + "; " + ex.Message + ")" : " (" + ex.Message + ")"));
            }
        }

        if (forecast == null || forecast.daily == null || forecast.daily.time == null || forecast.daily.time.Length == 0)
            return "Не удалось получить прогноз погоды.";

        DailyBlock d = forecast.daily;
        string dayKey = (day ?? "today").ToLowerInvariant();

        if (dayKey == "week")
        {
            var sb = new StringBuilder("Погода в городе " + city.Name + " на неделю:\n");
            for (int i = 0; i < d.time.Length; i++) sb.Append(DayLine(d, i)).Append('\n');
            return sb.ToString().TrimEnd();
        }

        if (dayKey == "tomorrow") return "Завтра в городе " + city.Name + ": " + DaySummary(d, 1) + ".";
        if (dayKey == "day_after") return "Послезавтра в городе " + city.Name + ": " + DaySummary(d, 2) + ".";

        CurrentBlock c = forecast.current;
        string now = c == null ? string.Empty
            : "Сейчас в городе " + city.Name + " " + Degrees(c.temperature_2m) +
              (float.IsNaN(c.apparent_temperature) ? string.Empty : ", ощущается как " + Num(c.apparent_temperature)) +
              ", " + Condition(c.weather_code) + ", ветер " + Mps(c.wind_speed_10m) + ". ";
        return now + "Сегодня " + DaySummary(d, 0) + ".";
    }

    private static async Task<ForecastReply> FetchOpenMeteoAsync(City city, CancellationToken ct)
    {
        string url = "https://api.open-meteo.com/v1/forecast?latitude=" + city.Lat.ToString("R", CultureInfo.InvariantCulture) +
                     "&longitude=" + city.Lon.ToString("R", CultureInfo.InvariantCulture) +
                     "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m" +
                     "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
                     "&wind_speed_unit=ms&timezone=auto&forecast_days=7";
        string json = await SimpleHttp.GetAsync(url, 8, ct);
        return JsonUtility.FromJson<ForecastReply>(json);
    }

    private static async Task<ForecastReply> FetchMetAsync(City city, CancellationToken ct)
    {
        string url = "https://api.met.no/weatherapi/locationforecast/2.0/compact?lat=" +
                     city.Lat.ToString("0.####", CultureInfo.InvariantCulture) +
                     "&lon=" + city.Lon.ToString("0.####", CultureInfo.InvariantCulture);
        string json = await SimpleHttp.GetAsync(url, 10, ct, UserAgent);
        return ConvertMet(JsonUtility.FromJson<MetReply>(json));
    }

    // Приводит данные MET Norway к тому же виду, что у Open-Meteo, чтобы текст ответа строился одним кодом.
    private static ForecastReply ConvertMet(MetReply met)
    {
        if (met == null || met.properties == null || met.properties.timeseries == null || met.properties.timeseries.Length == 0) return null;
        MetEntry[] series = met.properties.timeseries;

        var forecast = new ForecastReply { current = new CurrentBlock(), daily = new DailyBlock() };

        MetEntry first = series[0];
        if (first.data != null && first.data.instant != null && first.data.instant.details != null)
        {
            forecast.current.temperature_2m = first.data.instant.details.air_temperature;
            forecast.current.apparent_temperature = float.NaN; // MET не даёт «ощущаемую» температуру
            forecast.current.wind_speed_10m = first.data.instant.details.wind_speed;
            forecast.current.weather_code = WmoFromSymbol(SymbolOf(first.data));
        }

        var days = new List<string>();
        var codes = new List<int>();
        var tmax = new List<float>();
        var tmin = new List<float>();
        var codeDistance = new List<double>();

        foreach (MetEntry entry in series)
        {
            DateTime utc;
            if (entry == null || entry.data == null || entry.data.instant == null || entry.data.instant.details == null ||
                !DateTime.TryParse(entry.time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc))
                continue;
            DateTime local = utc.ToLocalTime();
            string key = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            int index = days.IndexOf(key);
            if (index < 0)
            {
                if (days.Count >= 7) break;
                days.Add(key);
                codes.Add(-1);
                tmax.Add(float.MinValue);
                tmin.Add(float.MaxValue);
                codeDistance.Add(double.MaxValue);
                index = days.Count - 1;
            }

            float t = entry.data.instant.details.air_temperature;
            if (t > tmax[index]) tmax[index] = t;
            if (t < tmin[index]) tmin[index] = t;

            // Описание дня берём по записи, ближайшей к полудню.
            string symbol = SymbolOf(entry.data);
            if (!string.IsNullOrEmpty(symbol))
            {
                double distance = Math.Abs(local.Hour + local.Minute / 60.0 - 13.0);
                if (distance < codeDistance[index])
                {
                    codeDistance[index] = distance;
                    codes[index] = WmoFromSymbol(symbol);
                }
            }
        }
        if (days.Count == 0) return null;

        forecast.daily.time = days.ToArray();
        forecast.daily.weather_code = codes.ToArray();
        forecast.daily.temperature_2m_max = tmax.ToArray();
        forecast.daily.temperature_2m_min = tmin.ToArray();
        return forecast;
    }

    private static string SymbolOf(MetData data)
    {
        if (data == null) return null;
        if (data.next_1_hours != null && data.next_1_hours.summary != null && !string.IsNullOrEmpty(data.next_1_hours.summary.symbol_code))
            return data.next_1_hours.summary.symbol_code;
        if (data.next_6_hours != null && data.next_6_hours.summary != null && !string.IsNullOrEmpty(data.next_6_hours.summary.symbol_code))
            return data.next_6_hours.summary.symbol_code;
        return null;
    }

    // Символы MET («lightrain_day», «partlycloudy_night») -> коды WMO, которые понимает Condition().
    private static int WmoFromSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return -1;
        string s = symbol;
        int underscore = s.IndexOf('_');
        if (underscore > 0) s = s.Substring(0, underscore);

        if (s.Contains("thunder")) return 95;
        if (s.Contains("sleet")) return 1000;
        bool showers = s.EndsWith("showers", StringComparison.Ordinal);
        if (s.Contains("snow")) return s.StartsWith("heavy", StringComparison.Ordinal) ? 75 : s.StartsWith("light", StringComparison.Ordinal) ? 71 : 73;
        if (s.Contains("rain"))
        {
            if (s.StartsWith("heavy", StringComparison.Ordinal)) return showers ? 82 : 65;
            if (s.StartsWith("light", StringComparison.Ordinal)) return 61;
            return showers ? 80 : 63;
        }
        switch (s)
        {
            case "clearsky": return 0;
            case "fair": return 1;
            case "partlycloudy": return 2;
            case "cloudy": return 3;
            case "fog": return 45;
            default: return -1;
        }
    }

    private static string DaySummary(DailyBlock d, int i)
    {
        if (i >= d.time.Length) return "прогноза нет";
        string text = Range(d.temperature_2m_min, d.temperature_2m_max, i) + ", " + Condition(Code(d, i));
        if (d.precipitation_probability_max != null && i < d.precipitation_probability_max.Length)
        {
            int p = (int)Math.Round(d.precipitation_probability_max[i]);
            if (p >= 20) text += ", вероятность осадков " + p + " " + Plural(p, "процент", "процента", "процентов");
        }
        return text;
    }

    private static string DayLine(DailyBlock d, int i)
    {
        DateTime date;
        string label = DateTime.TryParseExact(d.time[i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            ? date.Day + " " + MonthsGenitive[date.Month - 1] : d.time[i];
        return label + ": " + Range(d.temperature_2m_min, d.temperature_2m_max, i) + ", " + Condition(Code(d, i));
    }

    private static int Code(DailyBlock d, int i)
    {
        return d.weather_code != null && i < d.weather_code.Length ? d.weather_code[i] : -1;
    }

    private static string Range(float[] min, float[] max, int i)
    {
        if (min == null || max == null || i >= min.Length || i >= max.Length) return "температура неизвестна";
        int lo = (int)Math.Round(min[i]);
        int hi = (int)Math.Round(max[i]);
        return "от " + SignedNumber(lo) + " до " + SignedNumber(hi) + " " + Plural(Math.Abs(hi), "градуса", "градусов", "градусов");
    }

    private static string Degrees(double t)
    {
        int r = (int)Math.Round(t);
        return SignedNumber(r) + " " + Plural(Math.Abs(r), "градус", "градуса", "градусов");
    }

    private static string Num(double t)
    {
        return SignedNumber((int)Math.Round(t));
    }

    private static string SignedNumber(int n)
    {
        if (n > 0) return "плюс " + n;
        if (n < 0) return "минус " + Math.Abs(n);
        return "ноль";
    }

    private static string Mps(double v)
    {
        int n = (int)Math.Round(v);
        return n + " " + Plural(n, "метр", "метра", "метров") + " в секунду";
    }

    private static string Plural(int n, string one, string few, string many)
    {
        int mod100 = n % 100;
        int mod10 = n % 10;
        if (mod100 >= 11 && mod100 <= 14) return many;
        if (mod10 == 1) return one;
        if (mod10 >= 2 && mod10 <= 4) return few;
        return many;
    }

    // Коды погоды WMO в Open-Meteo.
    private static string Condition(int code)
    {
        switch (code)
        {
            case 0: return "ясно";
            case 1: return "преимущественно ясно";
            case 2: return "переменная облачность";
            case 3: return "пасмурно";
            case 45: case 48: return "туман";
            case 51: case 53: case 55: case 56: case 57: return "морось";
            case 61: case 63: case 65: case 66: case 67: return "дождь";
            case 71: case 73: case 75: case 77: return "снег";
            case 80: case 81: case 82: return "ливень";
            case 85: case 86: return "снегопад";
            case 95: case 96: case 99: return "гроза";
            case 1000: return "дождь со снегом";
            default: return "погода без подробностей";
        }
    }
}
