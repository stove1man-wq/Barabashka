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
        string url = "https://api.open-meteo.com/v1/forecast?latitude=" + city.Lat.ToString("R", CultureInfo.InvariantCulture) +
                     "&longitude=" + city.Lon.ToString("R", CultureInfo.InvariantCulture) +
                     "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m" +
                     "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
                     "&wind_speed_unit=ms&timezone=auto&forecast_days=7";
        string json = await SimpleHttp.GetAsync(url, 10, ct);
        ForecastReply forecast = JsonUtility.FromJson<ForecastReply>(json);
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
            : "Сейчас в городе " + city.Name + " " + Degrees(c.temperature_2m) + ", ощущается как " + Num(c.apparent_temperature) +
              ", " + Condition(c.weather_code) + ", ветер " + Mps(c.wind_speed_10m) + ". ";
        return now + "Сегодня " + DaySummary(d, 0) + ".";
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
            default: return "погода без подробностей";
        }
    }
}
