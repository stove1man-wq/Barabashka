using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Телефонная книга: поиск контакта по тому, как его назвали голосом («маме», «Ивану Петрову»).
/// Подбор имён не зависит от платформы (его можно проверить на компьютере); чтение контактов - только Android.
/// </summary>
public static class ContactBook
{
    public sealed class Contact
    {
        public string Name;
        public string Number;
    }

    public sealed class MatchResult
    {
        /// <summary>Единственный подходящий контакт или null.</summary>
        public Contact Found;
        /// <summary>Сколько слов фразы заняло имя (остальное - например, текст сообщения).</summary>
        public int WordsUsed;
        /// <summary>Подходящие разные люди, если однозначно выбрать нельзя.</summary>
        public List<string> Ambiguous = new List<string>();
    }

    private static List<Contact> _cache;
    private static float _cacheUntil;

    public static string Normalize(string value)
    {
        var sb = new StringBuilder();
        bool space = true;
        foreach (char c in (value ?? string.Empty).ToLowerInvariant())
        {
            char ch = c == 'ё' ? 'е' : c;
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); space = false; }
            else if (!space) { sb.Append(' '); space = true; }
        }
        return sb.ToString().Trim();
    }

    /// <summary>Слова совпали с точностью до падежного окончания: «маме» = «мама», «Ивану» = «Иван», «Сергею» = «Сергей».</summary>
    public static bool SameWord(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return false;
        if (a == b) return true;
        int min = Math.Min(a.Length, b.Length);
        if (min < 3 || Math.Abs(a.Length - b.Length) > 3) return false;
        int common = 0;
        while (common < min && a[common] == b[common]) common++;
        int need = min >= 4 ? min - 1 : 2;
        return common >= need;
    }

    private static bool NameMatches(string name, string[] queryWords)
    {
        string[] nameWords = Normalize(name).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string q in queryWords)
        {
            bool hit = false;
            foreach (string n in nameWords)
                if (SameWord(q, n)) { hit = true; break; }
            if (!hit) return false;
        }
        return nameWords.Length > 0;
    }

    /// <summary>
    /// Ищет контакт в начале фразы: пробует взять 3, 2, 1 слово. Так «маме привет» даёт контакт «Мама» (1 слово) и текст «привет».
    /// </summary>
    public static MatchResult Find(IList<string> words, IList<Contact> contacts)
    {
        var result = new MatchResult();
        if (words == null || contacts == null) return result;

        for (int take = Math.Min(3, words.Count); take >= 1; take--)
        {
            var query = new string[take];
            for (int i = 0; i < take; i++) query[i] = Normalize(words[i]);

            // Подходящие контакты, сгруппированные по имени (у одного человека может быть несколько номеров).
            var byName = new List<string>();
            var numbers = new Dictionary<string, List<Contact>>();
            foreach (Contact c in contacts)
            {
                if (!NameMatches(c.Name, query)) continue;
                string key = Normalize(c.Name);
                if (!numbers.ContainsKey(key)) { numbers[key] = new List<Contact>(); byName.Add(key); }
                numbers[key].Add(c);
            }
            if (byName.Count == 0) continue;

            // Если нашлись «Мама» и «Мама работа», выбираем самое короткое имя.
            int fewest = int.MaxValue;
            foreach (string key in byName) fewest = Math.Min(fewest, key.Split(' ').Length);
            var best = new List<string>();
            foreach (string key in byName) if (key.Split(' ').Length == fewest) best.Add(key);

            result.WordsUsed = take;
            if (best.Count == 1)
            {
                result.Found = numbers[best[0]][0];
            }
            else
            {
                foreach (string key in best) result.Ambiguous.Add(numbers[key][0].Name);
            }
            return result;
        }
        return result;
    }

    // ---- чтение контактов телефона ----

    public static bool Available
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public static void ResetCache() { _cache = null; }

    /// <summary>Все контакты с номерами (кэш на минуту). Нужно разрешение READ_CONTACTS.</summary>
    public static List<Contact> LoadAll()
    {
        if (_cache != null && Time.unscaledTime < _cacheUntil) return _cache;
        List<Contact> list = ReadPlatform();
        _cache = list;
        _cacheUntil = Time.unscaledTime + 60f;
        return list;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static List<Contact> ReadPlatform()
    {
        var result = new List<Contact>();
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (var uriClass = new AndroidJavaClass("android.net.Uri"))
            using (AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", "content://com.android.contacts/data/phones"))
            {
                // Без null в аргументах: JNI в Unity плохо выбирает перегрузку, если передать null.
                string[] projection = { "display_name", "data1" };
                using (AndroidJavaObject cursor = resolver.Call<AndroidJavaObject>("query", uri, projection, "data1 IS NOT NULL", new string[0], "display_name"))
                {
                    if (cursor == null) return result;
                    int nameCol = cursor.Call<int>("getColumnIndex", "display_name");
                    int numCol = cursor.Call<int>("getColumnIndex", "data1");
                    while (cursor.Call<bool>("moveToNext"))
                    {
                        string name = cursor.Call<string>("getString", nameCol);
                        string number = cursor.Call<string>("getString", numCol);
                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(number))
                            result.Add(new Contact { Name = name, Number = number });
                    }
                    cursor.Call("close");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Contacts] " + ex.Message);
        }
        return result;
    }
#else
    private static List<Contact> ReadPlatform() { return new List<Contact>(); }
#endif
}
