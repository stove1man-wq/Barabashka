using System;
using System.Collections.Generic;

/// <summary>
/// Встроенный список городов с координатами: погода по ним находится без интернета и без сервисов поиска.
/// Названия в любом падеже («в Казани», «в Нижнем Новгороде») сопоставляются по основе слов.
/// </summary>
public static class AgentCities
{
    private struct Entry
    {
        public string Title;       // как показывать
        public string[][] Aliases; // варианты названия, разбитые на слова (нижний регистр, без «ё», дефисы как пробелы)
        public double Lat, Lon;
    }

    private static readonly List<Entry> Entries = Build();

    private static List<Entry> Build()
    {
        var list = new List<Entry>();
        // Формат: «Название|вариант1|вариант2» (варианты необязательны), широта, долгота.
        Add(list, "Москва", 55.7558, 37.6173);
        Add(list, "Санкт-Петербург|петербург|питер|спб", 59.9343, 30.3351);
        Add(list, "Новосибирск", 55.0084, 82.9357);
        Add(list, "Екатеринбург", 56.8389, 60.6057);
        Add(list, "Казань", 55.7887, 49.1221);
        Add(list, "Нижний Новгород", 56.2965, 43.9361);
        Add(list, "Челябинск", 55.1644, 61.4368);
        Add(list, "Самара", 53.1959, 50.1002);
        Add(list, "Омск", 54.9885, 73.3242);
        Add(list, "Ростов-на-Дону|ростов", 47.2357, 39.7015);
        Add(list, "Уфа|уф", 54.7388, 55.9721);
        Add(list, "Красноярск", 56.0153, 92.8932);
        Add(list, "Воронеж", 51.6720, 39.1843);
        Add(list, "Пермь", 58.0105, 56.2502);
        Add(list, "Волгоград", 48.7080, 44.5133);
        Add(list, "Краснодар", 45.0355, 38.9753);
        Add(list, "Саратов", 51.5336, 46.0343);
        Add(list, "Тюмень", 57.1522, 65.5272);
        Add(list, "Тольятти", 53.5303, 49.3461);
        Add(list, "Ижевск", 56.8527, 53.2115);
        Add(list, "Барнаул", 53.3548, 83.7698);
        Add(list, "Ульяновск", 54.3282, 48.3866);
        Add(list, "Иркутск", 52.2870, 104.3050);
        Add(list, "Хабаровск", 48.4802, 135.0719);
        Add(list, "Ярославль", 57.6261, 39.8845);
        Add(list, "Владивосток", 43.1155, 131.8855);
        Add(list, "Махачкала", 42.9849, 47.5047);
        Add(list, "Томск", 56.4846, 84.9476);
        Add(list, "Оренбург", 51.7682, 55.0970);
        Add(list, "Кемерово", 55.3547, 86.0873);
        Add(list, "Новокузнецк", 53.7596, 87.1216);
        Add(list, "Рязань", 54.6269, 39.6916);
        Add(list, "Астрахань", 46.3497, 48.0408);
        Add(list, "Набережные Челны|челны", 55.7436, 52.3958);
        Add(list, "Пенза", 53.1959, 45.0183);
        Add(list, "Липецк", 52.6031, 39.5708);
        Add(list, "Киров", 58.6036, 49.6680);
        Add(list, "Чебоксары", 56.1439, 47.2489);
        Add(list, "Тула", 54.1961, 37.6182);
        Add(list, "Калининград", 54.7104, 20.4522);
        Add(list, "Курск", 51.7304, 36.1926);
        Add(list, "Улан-Удэ", 51.8335, 107.5841);
        Add(list, "Ставрополь", 45.0428, 41.9734);
        Add(list, "Сочи", 43.5855, 39.7231);
        Add(list, "Тверь", 56.8587, 35.9176);
        Add(list, "Магнитогорск", 53.4072, 58.9792);
        Add(list, "Иваново", 57.0004, 40.9739);
        Add(list, "Брянск", 53.2434, 34.3640);
        Add(list, "Белгород", 50.5954, 36.5873);
        Add(list, "Сургут", 61.2540, 73.3962);
        Add(list, "Владимир", 56.1290, 40.4070);
        Add(list, "Архангельск", 64.5401, 40.5433);
        Add(list, "Чита", 52.0340, 113.4994);
        Add(list, "Калуга", 54.5138, 36.2612);
        Add(list, "Смоленск", 54.7826, 32.0453);
        Add(list, "Волжский", 48.7858, 44.7797);
        Add(list, "Курган", 55.4410, 65.3411);
        Add(list, "Орел|орл", 52.9651, 36.0785);
        Add(list, "Череповец", 59.1374, 37.9090);
        Add(list, "Владикавказ", 43.0241, 44.6813);
        Add(list, "Мурманск", 68.9585, 33.0827);
        Add(list, "Вологда", 59.2181, 39.8886);
        Add(list, "Саранск", 54.1874, 45.1839);
        Add(list, "Тамбов", 52.7213, 41.4523);
        Add(list, "Стерлитамак", 53.6300, 55.9500);
        Add(list, "Грозный", 43.3180, 45.6981);
        Add(list, "Якутск", 62.0355, 129.6755);
        Add(list, "Кострома", 57.7679, 40.9269);
        Add(list, "Комсомольск-на-Амуре", 50.5499, 137.0079);
        Add(list, "Петрозаводск", 61.7849, 34.3469);
        Add(list, "Таганрог", 47.2362, 38.8969);
        Add(list, "Нижневартовск", 60.9344, 76.5531);
        Add(list, "Йошкар-Ола", 56.6316, 47.8862);
        Add(list, "Братск", 56.1325, 101.6142);
        Add(list, "Новороссийск", 44.7235, 37.7687);
        Add(list, "Дзержинск", 56.2380, 43.4600);
        Add(list, "Шахты", 47.7085, 40.2160);
        Add(list, "Нальчик", 43.4981, 43.6189);
        Add(list, "Орск", 51.2049, 58.5668);
        Add(list, "Сыктывкар", 61.6688, 50.8358);
        Add(list, "Нижнекамск", 55.6313, 51.8140);
        Add(list, "Ангарск", 52.5448, 103.8884);
        Add(list, "Старый Оскол", 51.2967, 37.8417);
        Add(list, "Великий Новгород|новгород", 58.5213, 31.2755);
        Add(list, "Балашиха", 55.7963, 37.9382);
        Add(list, "Благовещенск", 50.2796, 127.5405);
        Add(list, "Прокопьевск", 53.9061, 86.7194);
        Add(list, "Бийск", 52.5393, 85.2072);
        Add(list, "Химки", 55.8970, 37.4297);
        Add(list, "Псков", 57.8136, 28.3496);
        Add(list, "Энгельс", 51.4827, 46.1124);
        Add(list, "Рыбинск", 58.0446, 38.8426);
        Add(list, "Балаково", 52.0278, 47.8007);
        Add(list, "Северодвинск", 64.5636, 39.8296);
        Add(list, "Армавир", 44.9892, 41.1234);
        Add(list, "Подольск", 55.4242, 37.5547);
        Add(list, "Королев", 55.9142, 37.8545);
        Add(list, "Сызрань", 53.1559, 48.4744);
        Add(list, "Норильск", 69.3558, 88.1893);
        Add(list, "Златоуст", 55.1717, 59.6726);
        Add(list, "Каменск-Уральский", 56.4185, 61.9329);
        Add(list, "Мытищи", 55.9105, 37.7364);
        Add(list, "Люберцы", 55.6772, 37.8930);
        Add(list, "Волгодонск", 47.5136, 42.1519);
        Add(list, "Новочеркасск", 47.4220, 40.0937);
        Add(list, "Абакан", 53.7156, 91.4292);
        Add(list, "Находка", 42.8240, 132.8928);
        Add(list, "Уссурийск", 43.7973, 131.9519);
        Add(list, "Петропавловск-Камчатский|камчатский", 53.0452, 158.6483);
        Add(list, "Южно-Сахалинск", 46.9591, 142.7380);
        Add(list, "Магадан", 59.5638, 150.8035);
        Add(list, "Симферополь", 44.9521, 34.1024);
        Add(list, "Севастополь", 44.6167, 33.5254);
        Add(list, "Ялта", 44.4952, 34.1663);
        Add(list, "Минск", 53.9006, 27.5590);
        Add(list, "Киев", 50.4501, 30.5234);
        Add(list, "Алматы|алма ата", 43.2389, 76.8897);
        Add(list, "Астана", 51.1694, 71.4491);
        Add(list, "Ташкент", 41.2995, 69.2401);
        Add(list, "Баку", 40.4093, 49.8671);
        Add(list, "Ереван", 40.1792, 44.4991);
        Add(list, "Тбилиси", 41.7151, 44.8271);
        Add(list, "Бишкек", 42.8746, 74.5698);
        Add(list, "Кишинев", 47.0105, 28.8638);
        Add(list, "Лондон", 51.5074, -0.1278);
        Add(list, "Париж", 48.8566, 2.3522);
        Add(list, "Берлин", 52.5200, 13.4050);
        Add(list, "Нью-Йорк", 40.7128, -74.0060);
        Add(list, "Пекин", 39.9042, 116.4074);
        Add(list, "Токио", 35.6762, 139.6503);
        Add(list, "Стамбул", 41.0082, 28.9784);
        Add(list, "Дубай", 25.2048, 55.2708);
        Add(list, "Рим", 41.9028, 12.4964);
        Add(list, "Мадрид", 40.4168, -3.7038);
        Add(list, "Прага", 50.0755, 14.4378);
        Add(list, "Анталья", 36.8969, 30.7133);
        return list;
    }

    private static void Add(List<Entry> list, string spec, double lat, double lon)
    {
        string[] names = spec.Split('|');
        var aliases = new List<string[]>();
        foreach (string name in names) aliases.Add(Words(name));
        list.Add(new Entry { Title = names[0], Aliases = aliases.ToArray(), Lat = lat, Lon = lon });
    }

    private static string[] Words(string value)
    {
        string v = (value ?? string.Empty).ToLowerInvariant().Replace('ё', 'е').Replace('-', ' ');
        return v.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }

    // Основа слова: окончание падежа отбрасывается («москве» -> «моск», «нижнем» -> «нижн»).
    private static string Stem(string word)
    {
        if (word.Length >= 5 && (word.EndsWith("ий", StringComparison.Ordinal) || word.EndsWith("ый", StringComparison.Ordinal) ||
                                 word.EndsWith("ой", StringComparison.Ordinal)))
            return word.Substring(0, word.Length - 2);
        if (word.Length >= 7) return word.Substring(0, word.Length - 2);
        if (word.Length >= 4) return word.Substring(0, word.Length - 1);
        return word;
    }

    /// <summary>Ищет город в списке по названию в любом падеже. Возвращает null, если такого нет.</summary>
    public static AgentWeather.City Find(string query)
    {
        string[] q = Words(query);
        if (q.Length == 0) return null;

        int bestScore = 0;
        Entry best = default(Entry);
        foreach (Entry entry in Entries)
        {
            foreach (string[] alias in entry.Aliases)
            {
                if (alias.Length != q.Length) continue;
                int score = 0;
                bool match = true;
                for (int i = 0; i < alias.Length; i++)
                {
                    string stem = Stem(alias[i]);
                    if (q[i].StartsWith(stem, StringComparison.Ordinal)) score += stem.Length;
                    else { match = false; break; }
                }
                if (match && score > bestScore)
                {
                    bestScore = score;
                    best = entry;
                }
            }
        }
        if (bestScore < 2) return null;
        return new AgentWeather.City { Name = best.Title, Lat = best.Lat, Lon = best.Lon };
    }
}
