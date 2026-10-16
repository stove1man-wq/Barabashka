using System.Collections.Generic;
using System.Text;

/// <summary>
/// Превращает числа, записанные русскими словами, в цифры: «двадцать пять» -> «25».
/// Нужно потому, что распознавание речи часто выдаёт числа словами.
/// Работает с уже нормализованным текстом (строчные буквы, слова через пробел).
/// </summary>
public static class RuNumbers
{
    private static readonly Dictionary<string, int> Small = new Dictionary<string, int>
    {
        // единицы и «-надцать»
        { "ноль", 0 }, { "нуль", 0 },
        { "один", 1 }, { "одна", 1 }, { "одну", 1 }, { "одно", 1 },
        { "два", 2 }, { "две", 2 }, { "двух", 2 }, { "двум", 2 }, { "двумя", 2 },
        { "три", 3 }, { "трех", 3 }, { "трём", 3 }, { "трем", 3 }, { "тремя", 3 }, { "трёх", 3 },
        { "четыре", 4 }, { "четырех", 4 }, { "четырёх", 4 }, { "четырем", 4 }, { "четырьмя", 4 },
        { "пять", 5 }, { "пяти", 5 }, { "шесть", 6 }, { "шести", 6 }, { "семь", 7 }, { "семи", 7 },
        { "восемь", 8 }, { "восьми", 8 }, { "девять", 9 }, { "девяти", 9 }, { "десять", 10 }, { "десяти", 10 },
        { "одиннадцать", 11 }, { "двенадцать", 12 }, { "тринадцать", 13 }, { "четырнадцать", 14 },
        { "пятнадцать", 15 }, { "шестнадцать", 16 }, { "семнадцать", 17 }, { "восемнадцать", 18 },
        { "девятнадцать", 19 },
        // десятки
        { "двадцать", 20 }, { "двадцати", 20 }, { "тридцать", 30 }, { "тридцати", 30 },
        { "сорок", 40 }, { "сорока", 40 }, { "пятьдесят", 50 }, { "пятидесяти", 50 },
        { "шестьдесят", 60 }, { "шестидесяти", 60 }, { "семьдесят", 70 }, { "семидесяти", 70 },
        { "восемьдесят", 80 }, { "восьмидесяти", 80 }, { "девяносто", 90 }, { "девяноста", 90 },
        // сотни
        { "сто", 100 }, { "ста", 100 }, { "двести", 200 }, { "двухсот", 200 },
        { "триста", 300 }, { "трехсот", 300 }, { "трёхсот", 300 },
        { "четыреста", 400 }, { "четырехсот", 400 }, { "четырёхсот", 400 },
        { "пятьсот", 500 }, { "пятисот", 500 }, { "шестьсот", 600 }, { "шестисот", 600 },
        { "семьсот", 700 }, { "семисот", 700 }, { "восемьсот", 800 }, { "восьмисот", 800 },
        { "девятьсот", 900 }, { "девятисот", 900 }
    };

    private static readonly HashSet<string> Thousand = new HashSet<string>
    {
        "тысяча", "тысячи", "тысяч", "тысячу"
    };

    private static readonly HashSet<string> Million = new HashSet<string>
    {
        "миллион", "миллиона", "миллионов"
    };

    /// <summary>Заменяет числа-слова цифрами. Остальной текст не меняется.</summary>
    public static string Replace(string normalized)
    {
        if (string.IsNullOrEmpty(normalized)) return string.Empty;

        string[] tokens = normalized.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();

        long total = 0;
        long current = 0;
        bool inNumber = false;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            int value;

            if (Small.TryGetValue(token, out value))
            {
                current += value;
                inNumber = true;
            }
            else if (inNumber && Thousand.Contains(token))
            {
                total += (current == 0 ? 1 : current) * 1000;
                current = 0;
            }
            else if (inNumber && Million.Contains(token))
            {
                total += (current == 0 ? 1 : current) * 1000000;
                current = 0;
            }
            else
            {
                if (inNumber)
                {
                    Append(result, (total + current).ToString());
                    total = 0;
                    current = 0;
                    inNumber = false;
                }
                Append(result, token);
            }
        }

        if (inNumber) Append(result, (total + current).ToString());
        return result.ToString();
    }

    private static void Append(StringBuilder sb, string word)
    {
        if (sb.Length > 0) sb.Append(' ');
        sb.Append(word);
    }
}
