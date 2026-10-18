using UnityEngine;

/// <summary>
/// Настройки языковой модели (Ollama). Хранятся в PlayerPrefs; меняются в чате командой «/llm».
/// </summary>
public static class LlmSettings
{
    private const string EnabledKey = "llm.enabled";
    private const string UrlKey = "llm.url";
    private const string ModelKey = "llm.model";

    public const string DefaultModel = "llama3.1:8b";

    public static string DefaultUrl
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Адрес компьютера хозяина эмулятора Android Studio. На телефоне задаётся командой «/llm адрес …».
            return "http://10.0.2.2:11434";
#else
            return "http://127.0.0.1:11434";
#endif
        }
    }

    public static bool Enabled
    {
        get { return PlayerPrefs.GetInt(EnabledKey, 1) != 0; }
        set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static string Url
    {
        get
        {
            string value = PlayerPrefs.GetString(UrlKey, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? DefaultUrl : value.Trim().TrimEnd('/');
        }
        set { PlayerPrefs.SetString(UrlKey, (value ?? string.Empty).Trim().TrimEnd('/')); PlayerPrefs.Save(); }
    }

    public static string Model
    {
        get
        {
            string value = PlayerPrefs.GetString(ModelKey, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? DefaultModel : value.Trim();
        }
        set { PlayerPrefs.SetString(ModelKey, (value ?? string.Empty).Trim()); PlayerPrefs.Save(); }
    }
}
