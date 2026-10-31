using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Один вариант подключения к языковой модели: локальная Ollama или любой OpenAI-совместимый сервис.</summary>
[Serializable]
public class LlmProfile
{
    public string name;
    public string kind;     // "ollama", "openai", "gigachat" или "device" (модель на самом устройстве, model - путь к файлу)
    public string url;      // для openai - адрес до /v1, например https://openrouter.ai/api/v1
    public string model;
    public string apiKey;
    public string project;  // ID каталога Яндекса для YandexGPT

    public bool IsDevice
    {
        get { return string.Equals(kind, "device", StringComparison.OrdinalIgnoreCase); }
    }

    public bool IsOpenAi
    {
        get { return string.Equals(kind, "openai", StringComparison.OrdinalIgnoreCase); }
    }

    public bool IsGigaChat { get { return string.Equals(kind, "gigachat", StringComparison.OrdinalIgnoreCase); } }
    public bool IsYandexGpt { get { return IsOpenAi && !string.IsNullOrEmpty(project); } }
}

/// <summary>
/// Список профилей моделей. Порядок попыток: сначала Ollama (из LlmSettings), затем добавленные профили по порядку.
/// Хранится в PlayerPrefs на устройстве. Ключи API никогда не пишутся в журнал и показываются только в виде маски.
/// </summary>
public static class LlmProfiles
{
    public const string OllamaName = "ollama";
    private const string StoreKey = "llm.profiles";

    private static readonly Regex NamePattern = new Regex(@"^[A-Za-z0-9_-]{2,20}$", RegexOptions.CultureInvariant);

    [Serializable]
    public class Store
    {
        public List<LlmProfile> items = new List<LlmProfile>();
    }

    public struct Preset
    {
        public string Kind, Url, Model;
    }

    /// <summary>Готовые адреса популярных сервисов: достаточно «/llm добавить имя КЛЮЧ». Модели можно заменить своими.</summary>
    public static bool TryGetPreset(string name, out Preset preset)
    {
        switch ((name ?? string.Empty).ToLowerInvariant())
        {
            case "openrouter":
                preset = new Preset { Kind = "openai", Url = "https://openrouter.ai/api/v1", Model = "nvidia/nemotron-3-ultra-550b-a55b:free" };
                return true;
            case "groq":
                preset = new Preset { Kind = "openai", Url = "https://api.groq.com/openai/v1", Model = "llama-3.3-70b-versatile" };
                return true;
            case "yandex":
                preset = new Preset { Kind = "openai", Url = "https://llm.api.cloud.yandex.net/v1", Model = "gpt://{project}/yandexgpt/latest" };
                return true;
            case "gigachat":
                preset = new Preset { Kind = "gigachat", Url = "https://gigachat.devices.sberbank.ru/api/v1", Model = "GigaChat" };
                return true;
            default:
                preset = default(Preset);
                return false;
        }
    }

    public static bool IsValidName(string name)
    {
        return !string.IsNullOrEmpty(name) && NamePattern.IsMatch(name);
    }

    private static string SecretName(string profile)
    {
        return "llm." + (profile ?? string.Empty).ToLowerInvariant();
    }

    public static List<LlmProfile> Extra()
    {
        try
        {
            string json = PlayerPrefs.GetString(StoreKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                Store store = JsonUtility.FromJson<Store>(json);
                if (store != null && store.items != null)
                {
                    bool migrate = false;
                    foreach (LlmProfile p in store.items)
                    {
                        // Ключи старых версий лежали в открытом виде: переносим в защищённое хранилище.
                        if (!string.IsNullOrEmpty(p.apiKey)) migrate = true;
                        else p.apiKey = SecretStore.Get(SecretName(p.name));
                    }
                    if (migrate) Save(store.items);
                    return store.items;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LLM] Не удалось прочитать профили моделей: " + ex.Message);
        }
        return new List<LlmProfile>();
    }

    // Ключи уходят в SecretStore, а в списке профилей остаются пустыми. Если защищённое хранение не сработало,
    // ключ остаётся в списке (модели продолжат работать), об этом пишется предупреждение.
    private static void Save(List<LlmProfile> items)
    {
        var stored = new List<LlmProfile>();
        foreach (LlmProfile p in items)
        {
            var copy = new LlmProfile { name = p.name, kind = p.kind, url = p.url, model = p.model, apiKey = string.Empty, project = p.project };
            if (!string.IsNullOrEmpty(p.apiKey))
            {
                if (!SecretStore.Set(SecretName(p.name), p.apiKey))
                {
                    Debug.LogWarning("[LLM] Не удалось защитить ключ профиля «" + p.name + "», он сохранён без шифрования.");
                    copy.apiKey = p.apiKey;
                }
            }
            else
            {
                SecretStore.Remove(SecretName(p.name));
            }
            stored.Add(copy);
        }
        PlayerPrefs.SetString(StoreKey, JsonUtility.ToJson(new Store { items = stored }));
        PlayerPrefs.Save();
    }

    public static LlmProfile OllamaProfile()
    {
        return new LlmProfile { name = OllamaName, kind = "ollama", url = LlmSettings.Url, model = LlmSettings.Model, apiKey = string.Empty };
    }

    /// <summary>Профили в порядке попыток.</summary>
    public static List<LlmProfile> Chain()
    {
        var list = new List<LlmProfile>();
        if (LlmSettings.OllamaUsable) list.Add(OllamaProfile());
        LlmProfile device = null;
        var domestic = new List<LlmProfile>();
        var foreign = new List<LlmProfile>();
        foreach (LlmProfile p in Extra())
        {
            if (p.IsDevice) { device = p; continue; }   // модель в телефоне медленнее остальных: обращаемся к ней в последнюю очередь
            // YandexGPT без ID каталога работать не может: не тратим на неё время.
            if ((p.model ?? string.Empty).Contains("{project}") && string.IsNullOrEmpty(p.project)) continue;
            if (IsDomestic(p)) domestic.Add(p); else foreign.Add(p);
        }
        list.AddRange(domestic);   // российские облачные работают без VPN
        list.AddRange(foreign);
        if (device != null) list.Add(device);
        return list;
    }

    /// <summary>Российская облачная модель (GigaChat, YandexGPT): не требует VPN.</summary>
    public static bool IsDomestic(LlmProfile p)
    {
        if (p == null) return false;
        string url = (p.url ?? string.Empty).ToLowerInvariant();
        return p.IsGigaChat || url.Contains("yandex") || url.Contains("sberbank");
    }

    public static void AddOrReplace(LlmProfile profile)
    {
        List<LlmProfile> items = Extra();
        for (int i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i].name, profile.name, StringComparison.OrdinalIgnoreCase))
            {
                items[i] = profile;
                Save(items);
                return;
            }
        }
        items.Add(profile);
        Save(items);
    }

    public static bool Remove(string name)
    {
        List<LlmProfile> items = Extra();
        int removed = items.RemoveAll(p => string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            SecretStore.Remove(SecretName(name));
            Save(items);
        }
        return removed > 0;
    }

    public static bool SetKey(string name, string key)
    {
        List<LlmProfile> items = Extra();
        foreach (LlmProfile p in items)
        {
            if (!string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)) continue;
            p.apiKey = key ?? string.Empty;
            Save(items);
            return true;
        }
        return false;
    }

    public static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "ключа нет";
        return key.Length <= 8 ? "ключ задан" : "ключ " + key.Substring(0, 4) + "…" + key.Substring(key.Length - 2);
    }
}
