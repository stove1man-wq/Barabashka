using Barabashka.Contracts;
using Barabashka.Core.Settings;
using UnityEngine;

public static class AppBootstrap
{
    // Страницы настроек регистрируются до загрузки сцены, чтобы интерфейс видел их сразу.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSettingsPages()
    {
        SettingsRegistry.Register(new VoicePage());
        SettingsRegistry.Register(new ModelsPage());
        SettingsRegistry.Register(new TaxiPage());
        SettingsRegistry.Register(new SecurityPage());
        SettingsRegistry.Register(new ChatPage());
        SettingsRegistry.Register(new GeneralPage());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeApplication()
    {
        // Программный поиск или создание корневого объекта агента на сцене
        GameObject agentObj = GameObject.Find("[AI_AGENT]");
        if (agentObj == null)
        {
            // В сцене уже может лежать объект AI_AGENT (с AgentManager и AgentCommandRouter): используем его,
            // иначе появятся два агента, и голос будет работать с тем, у которого нет интерфейса.
            agentObj = GameObject.Find("AI_AGENT");
        }
        if (agentObj == null)
        {
            agentObj = new GameObject("[AI_AGENT]");
        }

        // Программное добавление всех необходимых компонентов на объект
        if (agentObj.GetComponent<AgentCommandRouter>() == null) agentObj.AddComponent<AgentCommandRouter>();
        if (agentObj.GetComponent<AgentUIManager>() == null) agentObj.AddComponent<AgentUIManager>();
        if (agentObj.GetComponent<AvatarVisualizer>() == null) agentObj.AddComponent<AvatarVisualizer>();
        // Менеджер добавляем последним, чтобы его Awake() корректно подхватил созданные выше скрипты
        if (agentObj.GetComponent<AgentManager>() == null) agentObj.AddComponent<AgentManager>();

        Debug.Log("[Bootstrap] Программная сборка объекта [AI_AGENT] и подключение компонентов успешно выполнены!");
    }
}