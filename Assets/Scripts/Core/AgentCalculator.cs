using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// A04: точные вычисления без языковой модели.
/// Понимает: проценты («15 процентов от 2300»), сложение, вычитание, умножение, деление,
/// квадрат, куб, степень и корень. Формулировки: «сколько будет 12 умножить на 7»,
/// «умножь 12 на 7», «раздели 100 на 4», «вычти 3 из 10», «сложи 5 и 8», «корень из 144».
/// Числа можно говорить словами.
/// </summary>
public static class AgentCalculator
{
    private const string Num = @"(\d+(?:[.,]\d+)?)";
    private const RegexOptions Opt = RegexOptions.CultureInvariant;

    private static readonly Regex Percent = new Regex(
        Num + @"\s*(?:процент\w*|%)\s*от\s*" + Num, Opt);

    // «умножь 12 на 7», «раздели 100 на 4», «вычти 3 из 10», «сложи 5 и 8», «прибавь 5 к 8»
    private static readonly Regex Multiply = new Regex(@"(?:умножь|помножь|умножить|помножить)\s+" + Num + @"\s+на\s+" + Num, Opt);
    private static readonly Regex Divide = new Regex(@"(?:раздели|подели|разделить|поделить)\s+" + Num + @"\s+на\s+" + Num, Opt);
    private static readonly Regex Subtract = new Regex(@"(?:вычти|вычесть|отними|отнять)\s+" + Num + @"\s+из\s+" + Num, Opt);
    private static readonly Regex Add = new Regex(@"(?:сложи|сложить|прибавь|прибавить)\s+" + Num + @"\s+(?:и|к)\s+" + Num, Opt);

    private static readonly Regex Square = new Regex(Num + @"\s+в\s+квадрате", Opt);
    private static readonly Regex Cube = new Regex(Num + @"\s+в\s+кубе", Opt);
    private static readonly Regex Power = new Regex(Num + @"\s+в\s+степени\s+" + Num, Opt);
    private static readonly Regex Root = new Regex(@"корень\s+(?:квадратный\s+)?из\s+" + Num, Opt);

    // Слова-операции понятны и без «сколько будет»; значки (+ - * /) требуют вводного слова.
    private static readonly Regex WordBinary = new Regex(
        Num + @"\s*(плюс|минус|умножить на|помножить на|разделить на|поделить на)\s*" + Num, Opt);
    private static readonly Regex SymbolBinary = new Regex(
        Num + @"\s*(\+|-|\*|×|/|÷)\s*" + Num, Opt);

    private static readonly string[] Triggers =
    {
        "сколько", "посчитай", "посчитать", "вычисли", "реши", "чему равно", "калькулятор", "будет"
    };

    /// <summary>Возвращает true и готовый ответ, если фраза — вычисление.</summary>
    public static bool TryAnswer(string userText, out string answer)
    {
        answer = null;
        if (string.IsNullOrWhiteSpace(userText)) return false;

        string text = Prepare(userText);
        Match m;

        m = Percent.Match(text);
        if (m.Success)
        {
            double p = Parse(m.Groups[1].Value);
            double of = Parse(m.Groups[2].Value);
            answer = Format(p) + " процентов от " + Format(of) + " равно " + Format(of * p / 100.0) + ".";
            return true;
        }

        m = Multiply.Match(text);
        if (m.Success) return Binary(m, 1, 2, "умножить на", (a, b) => a * b, out answer);

        m = Divide.Match(text);
        if (m.Success) return Binary(m, 1, 2, "разделить на", (a, b) => a / b, out answer);

        m = Subtract.Match(text);
        if (m.Success)
        {
            // «вычти 3 из 10»: первое число вычитается из второго
            double sub = Parse(m.Groups[1].Value);
            double source = Parse(m.Groups[2].Value);
            answer = Format(source) + " минус " + Format(sub) + " равно " + Format(source - sub) + ".";
            return true;
        }

        m = Add.Match(text);
        if (m.Success) return Binary(m, 1, 2, "плюс", (a, b) => a + b, out answer);

        m = Power.Match(text);
        if (m.Success)
        {
            double a = Parse(m.Groups[1].Value);
            double b = Parse(m.Groups[2].Value);
            return Unary(Math.Pow(a, b), Format(a) + " в степени " + Format(b), out answer);
        }

        m = Square.Match(text);
        if (m.Success)
        {
            double a = Parse(m.Groups[1].Value);
            return Unary(a * a, Format(a) + " в квадрате", out answer);
        }

        m = Cube.Match(text);
        if (m.Success)
        {
            double a = Parse(m.Groups[1].Value);
            return Unary(a * a * a, Format(a) + " в кубе", out answer);
        }

        m = Root.Match(text);
        if (m.Success)
        {
            double a = Parse(m.Groups[1].Value);
            return Unary(Math.Sqrt(a), "корень из " + Format(a), out answer);
        }

        m = WordBinary.Match(text);
        if (m.Success)
        {
            double a = Parse(m.Groups[1].Value);
            double b = Parse(m.Groups[3].Value);
            return ByOperator(m.Groups[2].Value, a, b, out answer);
        }

        if (HasTrigger(text))
        {
            m = SymbolBinary.Match(text);
            if (m.Success)
            {
                double a = Parse(m.Groups[1].Value);
                double b = Parse(m.Groups[3].Value);
                return ByOperator(m.Groups[2].Value, a, b, out answer);
            }
        }

        return false;
    }

    private static bool Binary(Match m, int ia, int ib, string word, Func<double, double, double> op, out string answer)
    {
        double a = Parse(m.Groups[ia].Value);
        double b = Parse(m.Groups[ib].Value);
        if (word == "разделить на" && Math.Abs(b) < 1e-12)
        {
            answer = "На ноль делить нельзя.";
            return true;
        }
        answer = Format(a) + " " + word + " " + Format(b) + " равно " + Format(op(a, b)) + ".";
        return true;
    }

    private static bool Unary(double value, string description, out string answer)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            answer = "Не могу посчитать такое значение.";
            return true;
        }
        answer = description + " равно " + Format(value) + ".";
        return true;
    }

    private static bool ByOperator(string op, double a, double b, out string answer)
    {
        switch (op)
        {
            case "плюс":
            case "+":
                answer = Format(a) + " плюс " + Format(b) + " равно " + Format(a + b) + ".";
                return true;
            case "минус":
            case "-":
                answer = Format(a) + " минус " + Format(b) + " равно " + Format(a - b) + ".";
                return true;
            case "умножить на":
            case "помножить на":
            case "*":
            case "×":
                answer = Format(a) + " умножить на " + Format(b) + " равно " + Format(a * b) + ".";
                return true;
            default:
                if (Math.Abs(b) < 1e-12)
                {
                    answer = "На ноль делить нельзя.";
                    return true;
                }
                answer = Format(a) + " разделить на " + Format(b) + " равно " + Format(a / b) + ".";
                return true;
        }
    }

    private static bool HasTrigger(string text)
    {
        foreach (string t in Triggers)
            if (text.Contains(t)) return true;
        return false;
    }

    // Нижний регистр, числа словами -> цифры, знаки операций отделены пробелами.
    private static string Prepare(string input)
    {
        string lower = input.ToLowerInvariant();
        var sb = new StringBuilder(lower.Length + 8);

        for (int i = 0; i < lower.Length; i++)
        {
            char c = lower[i];
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if ((c == ',' || c == '.') && i > 0 && i + 1 < lower.Length &&
                     char.IsDigit(lower[i - 1]) && char.IsDigit(lower[i + 1]))
            {
                sb.Append(c); // десятичная дробь: 2,5
            }
            else if (c == '+' || c == '*' || c == '×' || c == '/' || c == '÷')
            {
                sb.Append(' ').Append(c).Append(' ');
            }
            else if (c == '-')
            {
                // минус между числами; дефис внутри слова не трогаем
                bool between = i > 0 && i + 1 < lower.Length &&
                               (char.IsDigit(lower[i - 1]) || lower[i - 1] == ' ') &&
                               (char.IsDigit(lower[i + 1]) || lower[i + 1] == ' ');
                sb.Append(between ? " - " : " ");
            }
            else if (c == '%')
            {
                sb.Append(" процентов ");
            }
            else
            {
                sb.Append(' ');
            }
        }

        string spaced = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        return RuNumbers.Replace(spaced);
    }

    private static double Parse(string value)
    {
        return double.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }

    private static string Format(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 1e-9 && Math.Abs(value) < 1e15)
            return ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

        return Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
