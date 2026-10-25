using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Небольшой разборщик JSON любой вложенности (ответы Telegram приходят вложенными объектами).
/// Объект -> Dictionary&lt;string,object&gt;, массив -> List&lt;object&gt;, число -> long или double, строка, bool, null.
/// </summary>
public static class Json
{
    public static object Parse(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        try
        {
            int i = 0;
            object value = ReadValue(text, ref i);
            return value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static Dictionary<string, object> Obj(object source, string key)
    {
        return Get(source, key) as Dictionary<string, object>;
    }

    public static List<object> List(object source, string key)
    {
        return Get(source, key) as List<object>;
    }

    public static string Str(object source, string key)
    {
        object v = Get(source, key);
        return v == null ? string.Empty : Convert.ToString(v, CultureInfo.InvariantCulture);
    }

    public static long Long(object source, string key)
    {
        object v = Get(source, key);
        if (v is long) return (long)v;
        if (v is double) return (long)(double)v;
        long parsed;
        return v != null && long.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out parsed) ? parsed : 0;
    }

    public static bool Bool(object source, string key)
    {
        object v = Get(source, key);
        return v is bool && (bool)v;
    }

    private static object Get(object source, string key)
    {
        var map = source as Dictionary<string, object>;
        object value;
        return map != null && map.TryGetValue(key, out value) ? value : null;
    }

    private static void Skip(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    private static object ReadValue(string s, ref int i)
    {
        Skip(s, ref i);
        char c = s[i];
        if (c == '{')
        {
            var map = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return map; }
            while (true)
            {
                Skip(s, ref i);
                string key = ReadString(s, ref i);
                Skip(s, ref i);
                if (s[i] != ':') throw new FormatException();
                i++;
                map[key] = ReadValue(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return map; }
                throw new FormatException();
            }
        }
        if (c == '[')
        {
            var list = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ReadValue(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new FormatException();
            }
        }
        if (c == '"') return ReadString(s, ref i);
        if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }

        int start = i;
        bool real = false;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
        {
            if (s[i] == '.' || s[i] == 'e' || s[i] == 'E') real = true;
            i++;
        }
        if (i == start) throw new FormatException();
        string number = s.Substring(start, i - start);
        if (!real)
        {
            long l;
            if (long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out l)) return l;
        }
        return double.Parse(number, CultureInfo.InvariantCulture);
    }

    private static string ReadString(string s, ref int i)
    {
        if (s[i] != '"') throw new FormatException();
        i++;
        var sb = new StringBuilder();
        while (true)
        {
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 4;
                    break;
                default: sb.Append(e); break;
            }
        }
    }
}
