using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Минимальный разбор плоского JSON-объекта {"ключ": "значение", ...} из ответа языковой модели.
/// Берёт первый объект из текста (модель может добавить слова вокруг). Вложенные значения хранятся как есть (строкой).
/// </summary>
public static class MiniJson
{
    public static Dictionary<string, string> ParseFlatObject(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        int start = text.IndexOf('{');
        if (start < 0) return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int i = start + 1;
        while (i < text.Length)
        {
            SkipSpaces(text, ref i);
            if (i >= text.Length) return null;
            if (text[i] == '}') return result;
            if (text[i] == ',') { i++; continue; }
            if (text[i] != '"') return null;

            string key = ReadString(text, ref i);
            if (key == null) return null;
            SkipSpaces(text, ref i);
            if (i >= text.Length || text[i] != ':') return null;
            i++;
            SkipSpaces(text, ref i);
            if (i >= text.Length) return null;

            string value;
            char c = text[i];
            if (c == '"')
            {
                value = ReadString(text, ref i);
                if (value == null) return null;
            }
            else if (c == '{' || c == '[')
            {
                value = ReadRaw(text, ref i);
                if (value == null) return null;
            }
            else
            {
                int s = i;
                while (i < text.Length && text[i] != ',' && text[i] != '}') i++;
                value = text.Substring(s, i - s).Trim();
                if (value == "null") value = string.Empty;
            }
            result[key] = value;
        }
        return null;
    }

    public static string Get(Dictionary<string, string> map, string key)
    {
        string value;
        if (map != null && map.TryGetValue(key, out value) && value != null) return value.Trim();
        return string.Empty;
    }

    /// <summary>Строка в кавычках JSON с экранированием.</summary>
    public static string Quote(string value)
    {
        var sb = new StringBuilder((value == null ? 0 : value.Length) + 2);
        sb.Append('"');
        if (value != null)
        {
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static void SkipSpaces(string text, ref int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
    }

    // text[i] == '"'. Возвращает разобранную строку и ставит i после закрывающей кавычки; null, если строка не закрыта.
    private static string ReadString(string text, ref int i)
    {
        var sb = new StringBuilder();
        i++;
        while (i < text.Length)
        {
            char c = text[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            if (i >= text.Length) return null;

            char e = text[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    int code;
                    if (i + 4 <= text.Length &&
                        int.TryParse(text.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                    {
                        sb.Append((char)code);
                        i += 4;
                    }
                    break;
                default: sb.Append(e); break;
            }
        }
        return null;
    }

    // text[i] == '{' или '['. Возвращает текст вложенного значения целиком и ставит i после него.
    private static string ReadRaw(string text, ref int i)
    {
        int start = i;
        int depth = 0;
        bool inString = false;
        while (i < text.Length)
        {
            char c = text[i++];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') inString = true;
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']')
            {
                depth--;
                if (depth == 0) return text.Substring(start, i - start);
            }
        }
        return null;
    }
}
