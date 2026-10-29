using Barabashka.Contracts;
using Barabashka.Core.Settings;
using UnityEngine;

/// <summary>
/// Единая точка инициализации приложения: регистрация страниц настроек и прочих одноразовых действий при старте.
/// Повесьте этот компонент на любой GameObject в стартовой сцене (или создавайте через RuntimeInitializeOnLoadMethod).
/// </summary>
public class AppBootstrap : MonoBehaviour
{
    private static bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInit()
    {
        // Создаём невидимый GameObject для хранения компонента, если его ещё нет в сцене.
        if (!_initialized && FindObjectOfType<AppBootstrap>() == null)
        {
            var go = new GameObject("[AppBootstrap]");
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<AppBootstrap>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (_initialized) return;
        _initialized = true;
        RegisterSettingsPages();
    }

    private static void RegisterSettingsPages()
    {
        SettingsRegistry.Register(new VoicePage());
        SettingsRegistry.Register(new ModelsPage());
        SettingsRegistry.Register(new SecurityPage());
        SettingsRegistry.Register(new ChatPage());
        SettingsRegistry.Register(new GeneralPage());
        Debug.Log($"[AppBootstrap] Зарегистрировано страниц настроек: {SettingsRegistry.Pages.Count}");
    }
}