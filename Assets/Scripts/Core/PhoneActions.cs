using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Действия на телефоне, которым не нужны особые разрешения: фонарик, громкость, заряд, поиск и сайты, карты, набор номера, настройки.
/// На Android используется системный API через AndroidJavaObject; на компьютере нужные действия открываются в браузере, остальные сообщают, что работают только на телефоне.
/// </summary>
public static class PhoneActions
{
    private static readonly Dictionary<string, string> Sites = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "youtube", "https://www.youtube.com" },
        { "vk", "https://vk.com" },
        { "google", "https://www.google.com" },
        { "yandex", "https://yandex.ru" },
        { "wikipedia", "https://ru.wikipedia.org" },
        { "mail", "https://mail.google.com" },
        { "maps", "https://yandex.ru/maps" },
        { "github", "https://github.com" },
        { "ozon", "https://www.ozon.ru" },
        { "wildberries", "https://www.wildberries.ru" }
    };

    /// <summary>Название сайта по тому, как его называют голосом («ютуб», «вк», «гитхаб»); null, если сайт неизвестен.</summary>
    public static string SiteForSpoken(string spoken)
    {
        foreach (string word in (spoken ?? string.Empty).ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.StartsWith("ютуб", StringComparison.Ordinal) || word.StartsWith("ютюб", StringComparison.Ordinal) || word.StartsWith("youtube", StringComparison.Ordinal)) return "youtube";
            if (word == "вк" || word.StartsWith("вконтакт", StringComparison.Ordinal)) return "vk";
            if (word.StartsWith("гугл", StringComparison.Ordinal) || word == "google") return "google";
            if (word.StartsWith("яндекс", StringComparison.Ordinal)) return "yandex";
            if (word.StartsWith("википеди", StringComparison.Ordinal)) return "wikipedia";
            if (word.StartsWith("почт", StringComparison.Ordinal) || word == "gmail") return "mail";
            if (word.StartsWith("карт", StringComparison.Ordinal)) return "maps";
            if (word.StartsWith("гитхаб", StringComparison.Ordinal) || word == "github") return "github";
            if (word == "озон" || word == "ozon") return "ozon";
            if (word == "вб" || word == "wb" || word.StartsWith("вайлдбер", StringComparison.Ordinal) || word.StartsWith("вайлбер", StringComparison.Ordinal) ||
                word.StartsWith("валбер", StringComparison.Ordinal) || word.StartsWith("валдбер", StringComparison.Ordinal) ||
                word.StartsWith("вилдбер", StringComparison.Ordinal) || word.StartsWith("wildber", StringComparison.Ordinal)) return "wildberries";
        }
        return null;
    }

    // ---- подбор приложения по названию (работает без Android, поэтому можно проверить отдельно) ----

    public sealed class AppEntry
    {
        public string Label;
        public string Package;
    }

    // Как приложение называют голосом -> как оно называется в списке приложений.
    private static readonly string[][] AppAliases =
    {
        new[] { "телеграм", "telegram" },
        // Мессенджер MAX (VK): в списке приложений подписан латиницей «MAX», пакет ru.oneme.app.
        new[] { "макс", "max", "ru.oneme.app" }, new[] { "max", "max", "ru.oneme.app" },
        // Маркетплейсы: в списке приложений они подписаны латиницей, а говорят по-русски и по-разному.
        new[] { "озон", "ozon" }, new[] { "вайлдбер", "wildberries", "wb" }, new[] { "вайлбер", "wildberries", "wb" },
        new[] { "валдбер", "wildberries", "wb" }, new[] { "валбер", "wildberries", "wb" }, new[] { "вилдбер", "wildberries", "wb" },
        new[] { "вб", "wildberries", "wb" }, new[] { "вайлд", "wildberries", "wb" },
        new[] { "ватсап", "whatsapp" }, new[] { "вотсап", "whatsapp" }, new[] { "вацап", "whatsapp" },
        new[] { "вайбер", "viber" }, new[] { "ютуб", "youtube" }, new[] { "ютюб", "youtube" }, new[] { "хром", "chrome" },
        new[] { "браузер", "chrome", "browser", "firefox", "яндекс браузер", "opera", "internet" },
        new[] { "камер", "camera", "камера" }, new[] { "галере", "gallery", "галерея", "photos", "фото" },
        new[] { "фото", "photos", "фото", "gallery" }, new[] { "календар", "calendar", "календарь" },
        new[] { "час", "clock", "часы" }, new[] { "будильник", "clock", "часы" }, new[] { "калькулятор", "calculator", "калькулятор" },
        new[] { "настройк", "settings", "настройки" }, new[] { "контакт", "contacts", "контакты" },
        new[] { "сообщен", "messages", "сообщения" }, new[] { "смс", "messages", "сообщения" },
        new[] { "почт", "gmail", "почта", "mail" }, new[] { "карт", "maps", "карты" }, new[] { "навигатор", "navigator", "навигатор", "maps" },
        new[] { "музык", "music", "музыка", "spotify" }, new[] { "плеймаркет", "play store", "play маркет" },
        new[] { "плей", "play store", "play маркет" }, new[] { "файл", "files", "файлы" }, new[] { "заметк", "keep", "notes", "заметки" },
        new[] { "диктофон", "recorder", "диктофон" }, new[] { "вконтакт", "vk", "вконтакте" }, new[] { "вк", "vk", "вконтакте" },
        new[] { "инстаграм", "instagram" }, new[] { "тикток", "tiktok" }, new[] { "такси", "taxi", "такси", "go" },
        new[] { "сбер", "сбербанк", "sber" }, new[] { "гугл", "google" },
        // Просто «яндекс» обрабатывает OpenApp отдельно (иначе открывался бы любой «Yandex …», например такси «Go»).
        // Яндекс Go (бывшее Яндекс Такси) находим и по названию, и по пакету ru.yandex.taxi: подпись у разных версий отличается.
        new[] { "яндекс го", "яндекс go", "yandex go", "ru.yandex.taxi" }, new[] { "яндекс такси", "яндекс go", "yandex go", "ru.yandex.taxi" },
        new[] { "яндекс гоу", "яндекс go", "yandex go", "ru.yandex.taxi" }
    };

    private static string NormalizeName(string value)
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

    private static string Trim(string word)
    {
        if (word.Length >= 7) return word.Substring(0, word.Length - 2);
        if (word.Length >= 5) return word.Substring(0, word.Length - 1);
        return word;
    }

    /// <summary>Выбирает приложение, которое имел в виду человек («телеграм», «камеру», «калькулятор»). null, если ничего не подошло.</summary>
    public static AppEntry MatchApp(string spokenName, IList<AppEntry> apps)
    {
        string query = NormalizeName(spokenName);
        if (query.Length == 0 || apps == null) return null;

        var terms = new List<string> { query, Trim(query) };
        foreach (string[] alias in AppAliases)
            if (query.StartsWith(alias[0], StringComparison.Ordinal))
                for (int i = 1; i < alias.Length; i++) terms.Add(alias[i]);

        AppEntry best = null;
        int bestScore = 0;
        foreach (AppEntry app in apps)
        {
            string label = NormalizeName(app.Label);
            int score = 0;
            // Совпадение по имени пакета (ru.yandex.taxi): надёжнее подписи.
            if (!string.IsNullOrEmpty(app.Package))
                foreach (string term in terms)
                    if (term.IndexOf('.') > 0 && string.Equals(app.Package, term, StringComparison.OrdinalIgnoreCase)) score = 100;
            if (label.Length == 0 && score == 0) continue;
            foreach (string term in terms)
            {
                if (term.Length < 2 || term.IndexOf('.') > 0) continue;
                if (label == term) score = Math.Max(score, 100);
                else if (label.StartsWith(term, StringComparison.Ordinal)) score = Math.Max(score, 70);
                else if (term.Length >= 4 && label.Contains(" " + term)) score = Math.Max(score, 50);
                else if (term.Length >= 4 && label.Contains(term)) score = Math.Max(score, 40);
            }
            if (score == 0) continue;
            score = score * 100 - Math.Min(99, label.Length);   // при равенстве выбираем более короткое название
            if (score > bestScore) { bestScore = score; best = app; }
        }
        return best;
    }

    /// <summary>Запустить уже найденное приложение (для других блоков, например такси).</summary>
    public static string Launch(AppEntry app)
    {
        return app == null ? "Приложение не найдено." : LaunchApp(app);
    }

    /// <summary>Открыть приложение по названию; если такого приложения нет, но это известный сайт - открыть сайт.</summary>
    public static string OpenApp(string spokenName)
    {
        string name = (spokenName ?? string.Empty).Trim();
        if (name.Length == 0) return "Не понял, какое приложение открыть.";

        List<AppEntry> apps = ListApps();

        // Просто «яндекс»: само приложение «Яндекс» или Яндекс Браузер, иначе сайт. Такси и другие сервисы - только по названию.
        string normalized = NormalizeName(name);
        if (normalized == "яндекс" || normalized == "яндексе" || normalized == "яндекса" || normalized == "yandex")
        {
            foreach (string wanted in new[] { "яндекс", "yandex", "яндекс браузер", "yandex browser" })
                foreach (AppEntry candidate in apps)
                    if (NormalizeName(candidate.Label) == wanted) return LaunchApp(candidate);
            return OpenSite("yandex");
        }

        AppEntry app = MatchApp(name, apps);
        if (app != null) return LaunchApp(app);

        // «Яндекс Го», «Яндекс Музыка»: название длиннее, чем просто «яндекс». Если приложения нет, это не повод открывать сайт яндекса.
        if (normalized.StartsWith("яндекс ", StringComparison.Ordinal) || normalized.StartsWith("yandex ", StringComparison.Ordinal))
        {
            var related = new List<string>();
            foreach (AppEntry candidate in apps)
                if (NormalizeName(candidate.Label).Contains("яндекс") || NormalizeName(candidate.Label).Contains("yandex") ||
                    (candidate.Package ?? string.Empty).Contains("yandex"))
                    related.Add(candidate.Label + " [" + candidate.Package + "]");
            Debug.Log("[Phone] Приложение «" + name + "» не найдено. Яндекс-приложения на телефоне: " + string.Join("; ", related.ToArray()));
            return "Не нашёл приложение «" + name + "» на телефоне.";
        }

        string site = SiteForSpoken(name);
        if (site != null) return OpenSite(site);

        if (apps.Count == 0) return "Приложения запускаются только на телефоне.";
        return "Не нашёл приложение «" + name + "» на телефоне.";
    }

    public static string OpenSite(string name)
    {
        string url;
        if (!Sites.TryGetValue((name ?? string.Empty).Trim(), out url))
            return "Не знаю такой сайт. Могу открыть: youtube, vk, google, yandex, wikipedia, mail, maps.";
        Application.OpenURL(url);
        return "Открываю " + name.Trim() + ".";
    }

    public static string WebSearch(string query)
    {
        string q = (query ?? string.Empty).Trim();
        if (q.Length == 0) return "Не понял, что искать.";
        Application.OpenURL("https://yandex.ru/search/?text=" + Uri.EscapeDataString(q));
        return "Ищу в интернете: " + q;
    }

    public static string Route(string place)
    {
        string p = (place ?? string.Empty).Trim();
        if (p.Length == 0) return "Не понял, какое место показать на карте.";
#if UNITY_ANDROID && !UNITY_EDITOR
        Application.OpenURL("geo:0,0?q=" + Uri.EscapeDataString(p));
#else
        Application.OpenURL("https://yandex.ru/maps/?text=" + Uri.EscapeDataString(p));
#endif
        return "Показываю на карте: " + p;
    }

    public static string Dial(string number)
    {
        var digits = new StringBuilder();
        string raw = number ?? string.Empty;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (char.IsDigit(c) || (c == '+' && digits.Length == 0)) digits.Append(c);
        }
        if (digits.Length < 5 || digits.Length > 16) return "Не разобрал номер телефона. Назовите номер цифрами подряд.";
        Application.OpenURL("tel:" + digits);
        return "Открываю набор номера " + digits + ". Нажмите вызов, чтобы позвонить.";
    }

    public static string Battery()
    {
        float level = SystemInfo.batteryLevel;
        if (level < 0f) return "Не удалось узнать заряд: на этом устройстве он недоступен.";
        string state = SystemInfo.batteryStatus == BatteryStatus.Charging ? ", идёт зарядка" : string.Empty;
        return "Заряд батареи: " + Mathf.RoundToInt(level * 100f) + "%" + state + ".";
    }

    public static string Alarm(DateTime due, bool showUi)
    {
        return AlarmPlatform(due.Hour, due.Minute, showUi)
            ? "Будильник на " + due.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) +
              (due.Date > DateTime.Now.Date.AddDays(1) ? " (сработает при ближайшем наступлении этого времени)." : ".")
            : "Не удалось поставить будильник.";
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private const int StreamMusic = 3;
    private const int FlagShowUi = 1;
    private const int FlagNewTask = 0x10000000;

    private static AndroidJavaObject CurrentActivity()
    {
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            return player.GetStatic<AndroidJavaObject>("currentActivity");
    }

    public static string Flashlight(bool on)
    {
        try
        {
            using (AndroidJavaObject activity = CurrentActivity())
            using (AndroidJavaObject manager = activity.Call<AndroidJavaObject>("getSystemService", "camera"))
            {
                string[] ids = manager.Call<string[]>("getCameraIdList");
                if (ids == null || ids.Length == 0) return "Камера не найдена.";
                Exception last = null;
                foreach (string id in ids)
                {
                    try
                    {
                        manager.Call("setTorchMode", id, on);
                        return on ? "Фонарик включён." : "Фонарик выключен.";
                    }
                    catch (Exception ex) { last = ex; }
                }
                return "Не удалось управлять фонариком: " + (last != null ? last.Message : "нет вспышки");
            }
        }
        catch (Exception ex)
        {
            return "Не удалось управлять фонариком: " + ex.Message;
        }
    }

    public static string Volume(string action, int percent)
    {
        try
        {
            using (AndroidJavaObject activity = CurrentActivity())
            using (AndroidJavaObject audio = activity.Call<AndroidJavaObject>("getSystemService", "audio"))
            {
                int max = audio.Call<int>("getStreamMaxVolume", StreamMusic);
                int current = audio.Call<int>("getStreamVolume", StreamMusic);
                int step = Math.Max(1, (int)Math.Round(max * 0.15));
                int target = current;
                switch ((action ?? string.Empty).ToLowerInvariant())
                {
                    case "up": target = Math.Min(max, current + step); break;
                    case "down": target = Math.Max(0, current - step); break;
                    case "max": target = max; break;
                    case "mute": target = 0; break;
                    case "set":
                        if (percent < 0 || percent > 100) return "Назовите громкость от 0 до 100 процентов.";
                        target = (int)Math.Round(max * percent / 100.0);
                        break;
                    default: return "Не понял, что сделать с громкостью.";
                }
                audio.Call("setStreamVolume", StreamMusic, target, FlagShowUi);
                return "Громкость: " + (max > 0 ? (int)Math.Round(target * 100.0 / max) : 0) + "%.";
            }
        }
        catch (Exception ex)
        {
            return "Не удалось изменить громкость: " + ex.Message;
        }
    }

    private static bool AlarmPlatform(int hour, int minute, bool showUi)
    {
        try
        {
            using (AndroidJavaObject activity = CurrentActivity())
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SET_ALARM"))
            {
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.alarm.HOUR", hour);
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.alarm.MINUTES", minute);
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.alarm.MESSAGE", "Барабашка");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.alarm.SKIP_UI", !showUi);
                intent.Call<AndroidJavaObject>("addFlags", FlagNewTask);
                activity.Call("startActivity", intent);
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Phone] Будильник: " + ex.Message);
            return false;
        }
    }

    public static string ShowAlarms()
    {
        try
        {
            using (AndroidJavaObject activity = CurrentActivity())
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SHOW_ALARMS"))
            {
                intent.Call<AndroidJavaObject>("addFlags", FlagNewTask);
                activity.Call("startActivity", intent);
            }
            return "Открываю будильники: там можно выключить или удалить нужный.";
        }
        catch (Exception ex)
        {
            return "Не удалось открыть будильники: " + ex.Message;
        }
    }

    public static List<AppEntry> ListApps()
    {
        var result = new List<AppEntry>();
        try
        {
            using (AndroidJavaObject activity = CurrentActivity())
            using (AndroidJavaObject pm = activity.Call<AndroidJavaObject>("getPackageManager"))
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.MAIN"))
            {
                intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.LAUNCHER");
                using (AndroidJavaObject list = pm.Call<AndroidJavaObject>("queryIntentActivities", intent, 0))
                {
                    int count = list.Call<int>("size");
                    for (int i = 0; i < count; i++)
                    {
                        using (AndroidJavaObject info = list.Call<AndroidJavaObject>("get", i))
                        using (AndroidJavaObject activityInfo = info.Get<AndroidJavaObject>("activityInfo"))
                        using (AndroidJavaObject labelChars = info.Call<AndroidJavaObject>("loadLabel", pm))
                        {
                            result.Add(new AppEntry
                            {
                                Package = activityInfo.Get<string>("packageName"),
                                Label = labelChars.Call<string>("toString")
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Phone] Список приложений: " + ex.Message);
        }
        return result;
    }

    private static string LaunchApp(AppEntry app)
    {
        try
        {
            using (AndroidJavaObject activity = CurrentActivity())
            using (AndroidJavaObject pm = activity.Call<AndroidJavaObject>("getPackageManager"))
            using (AndroidJavaObject launch = pm.Call<AndroidJavaObject>("getLaunchIntentForPackage", app.Package))
            {
                if (launch == null) return "Не удалось открыть «" + app.Label + "».";
                launch.Call<AndroidJavaObject>("addFlags", FlagNewTask);
                activity.Call("startActivity", launch);
            }
            return "Открываю " + app.Label + ".";
        }
        catch (Exception ex)
        {
            return "Не удалось открыть «" + app.Label + "»: " + ex.Message;
        }
    }

    public static string PhoneSettings(string section)
    {
        string action;
        switch ((section ?? string.Empty).ToLowerInvariant())
        {
            case "wifi": action = "android.settings.WIFI_SETTINGS"; break;
            case "bluetooth": action = "android.settings.BLUETOOTH_SETTINGS"; break;
            default: action = "android.settings.SETTINGS"; break;
        }
        try
        {
            using (AndroidJavaObject activity = CurrentActivity())
            using (var intent = new AndroidJavaObject("android.content.Intent", action))
            using (AndroidJavaObject flagged = intent.Call<AndroidJavaObject>("addFlags", FlagNewTask))
            {
                activity.Call("startActivity", intent);
            }
            return "Открываю настройки телефона.";
        }
        catch (Exception ex)
        {
            return "Не удалось открыть настройки: " + ex.Message;
        }
    }
#else
    private static bool AlarmPlatform(int hour, int minute, bool showUi) { return false; }
    public static string ShowAlarms() { return "Будильники открываются только на телефоне."; }
    public static List<AppEntry> ListApps() { return new List<AppEntry>(); }
    private static string LaunchApp(AppEntry app) { return "Приложения запускаются только на телефоне."; }
    public static string Flashlight(bool on) { return "Фонарик есть только на телефоне."; }
    public static string Volume(string action, int percent) { return "Громкость телефона можно менять только на самом телефоне."; }
    public static string PhoneSettings(string section) { return "Настройки телефона открываются только на телефоне."; }
#endif
}
