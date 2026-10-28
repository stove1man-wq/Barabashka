using System.Collections.Generic;
using System.Text;
using Barabashka.Contracts;

/// <summary>
/// Фразы про картинки и видео: «покажи фото», «выбери видео», «добавь ролик»; при открытом просмотре: «дальше», «назад», «закрой»,
/// а для видео ещё «пауза», «продолжи», «перемотай на 10 секунд вперёд», «быстрее».
/// «Открой галерею» сюда не относится: это запуск приложения «Галерея» (см. PhoneActions).
/// </summary>
public static class MediaParser
{
    private static readonly string[] PickVerbs = { "покаж", "выбер", "выбр", "добав", "загруз", "прикреп", "вставь", "включ", "запуст" };
    private static readonly string[] PictureNouns = { "фото", "картинк", "изображен", "снимк" };
    private static readonly string[] VideoNouns = { "видео", "ролик", "фильм", "клип" };
    private static readonly string[] PauseWords = { "пауз", "стоп", "останов", "замри" };
    private static readonly string[] PlayWords = { "продолж", "воспроизв", "играй", "поехали" };
    private static readonly string[] SeekWords = { "перемот", "промот", "вперед", "назад" };
    private static readonly string[] FasterWords = { "быстрее", "ускор" };
    private static readonly string[] SlowerWords = { "медленнее", "замедл" };

    private static readonly string[] NextWords = { "дальше", "следующ", "вперед", "далее" };
    private static readonly string[] PrevWords = { "назад", "предыдущ", "обратно" };
    private static readonly string[] CloseWords = { "закрой", "закрыть", "свернуть", "сверни", "выход", "хватит" };
    // Полный экран: «на весь экран», «полный экран», «разверни»; выход: «выйти из полного экрана», «закрой полный экран».
    private static readonly string[] ScreenStems = { "экран" };
    private static readonly string[] FullStems = { "полн", "весь", "всю", "все" };
    private static readonly string[] ExitStems = { "выйд", "выйт", "выход", "выключ", "отключ", "убер", "закро", "закры", "сверн", "обратн" };

    /// <summary>Выбор картинки или видео.</summary>
    public static bool TryPick(string lower, out string command)
    {
        command = null;
        string[] w = Words(lower);
        if (w.Length == 0 || w.Length > 6) return false;
        if (HasStem(w, PickVerbs) && HasStem(w, VideoNouns))
        {
            command = Tool("media_pick", "kind", "video");
            return true;
        }
        if (!HasStem(w, PickVerbs) || !HasStem(w, PictureNouns)) return false;
        command = Tool("media_pick", "kind", "image");
        return true;
    }

    /// <summary>Управление просмотром; работает только пока просмотр открыт, чтобы «назад» и «дальше» не перехватывались зря.</summary>
    public static bool TryControl(string lower, out string command)
    {
        command = null;
        IMediaViewer viewer = MediaServices.Viewer;
        if (viewer == null || !viewer.IsOpen) return false;

        string[] w = Words(lower);

        // Команды видео работают, только когда открыто видео.
        IMediaPlayback playback = viewer as IMediaPlayback;
        if (playback != null && playback.HasVideo && TryPlayback(w, out command)) return true;

        // Полный экран работает только для видео.
        if (playback != null && playback.HasVideo && TryFullscreen(w, out command)) return true;

        if (w.Length == 0 || w.Length > 3) return false;

        string action = HasStem(w, NextWords) ? "next" : HasStem(w, PrevWords) ? "prev" : HasStem(w, CloseWords) ? "close" : null;
        if (action == null) return false;
        command = Tool("media_control", "action", action);
        return true;
    }

    // Полный экран: до 6 слов. Выход проверяется внутри, чтобы «закрой полный экран» не включал его.
    private static bool TryFullscreen(string[] w, out string command)
    {
        command = null;
        if (w.Length == 0 || w.Length > 6) return false;

        bool expand = w.Length <= 3 && HasStem(w, new[] { "разверни" });
        bool named = HasStem(w, new[] { "полноэкран" }) || (HasStem(w, ScreenStems) && HasStem(w, FullStems));
        if (!expand && !named) return false;

        bool exit = named && HasStem(w, ExitStems);
        command = Tool("media_control", "action", exit ? "exit_fullscreen" : "fullscreen");
        return true;
    }

    // Пауза, продолжение, перемотка, скорость.
    private static bool TryPlayback(string[] w, out string command)
    {
        command = null;
        if (w.Length == 0 || w.Length > 8) return false;

        int seconds;
        if (HasStem(w, SeekWords) && TrySeconds(w, out seconds))
        {
            bool back = HasStem(w, new[] { "назад" });
            command = "TOOL|media_control|" + AgentToolRegistry.BuildQuery(new[]
            {
                new KeyValuePair<string, string>("action", "seek"),
                new KeyValuePair<string, string>("seconds", (back ? -seconds : seconds).ToString(System.Globalization.CultureInfo.InvariantCulture))
            });
            return true;
        }

        if (w.Length > 3) return false;
        string action = HasStem(w, PauseWords) ? "pause" : HasStem(w, PlayWords) ? "play"
            : HasStem(w, FasterWords) ? "faster" : HasStem(w, SlowerWords) ? "slower" : null;
        if (action == null) return false;
        command = Tool("media_control", "action", action);
        return true;
    }

    // Секунды из фразы: «на 10 секунд», «на минуту», «перемотай вперёд» (по умолчанию 10).
    // Одно слово «назад» или «вперёд» без «перемотай» и числа сюда не попадает: это «предыдущее» и «следующее».
    private static bool TrySeconds(string[] w, out int seconds)
    {
        seconds = 10;
        bool explicitSeek = HasStem(w, new[] { "перемот", "промот" });
        bool found = false;
        for (int i = 0; i < w.Length; i++)
        {
            int n;
            if (int.TryParse(w[i], out n) && n > 0 && n <= 3600)
            {
                bool minutes = i + 1 < w.Length && w[i + 1].StartsWith("минут", System.StringComparison.Ordinal);
                seconds = minutes ? n * 60 : n;
                found = true;
                break;
            }
            switch (w[i])
            {
                case "пять": seconds = 5; found = true; break;
                case "десять": seconds = 10; found = true; break;
                case "пятнадцать": seconds = 15; found = true; break;
                case "двадцать": seconds = 20; found = true; break;
                case "тридцать": seconds = 30; found = true; break;
                case "сорок": seconds = 40; found = true; break;
                case "минуту": case "минута": seconds = 60; found = true; break;
                case "полминуты": seconds = 30; found = true; break;
            }
            if (found) break;
        }
        return found || explicitSeek;
    }

    private static string Tool(string name, string key, string value)
    {
        return "TOOL|" + name + "|" + AgentToolRegistry.BuildQuery(new[] { new KeyValuePair<string, string>(key, value) });
    }

    private static bool HasStem(string[] words, string[] stems)
    {
        foreach (string word in words)
            foreach (string stem in stems)
                if (word.StartsWith(stem, System.StringComparison.Ordinal)) return true;
        return false;
    }

    private static string[] Words(string text)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        foreach (char c in (text ?? string.Empty).ToLowerInvariant())
        {
            char ch = c == 'ё' ? 'е' : c;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0) { list.Add(sb.ToString()); sb.Length = 0; }
        }
        if (sb.Length > 0) list.Add(sb.ToString());
        return list.ToArray();
    }
}
