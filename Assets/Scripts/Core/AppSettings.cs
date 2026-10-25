using UnityEngine;

/// <summary>Что агент делает с действиями вне приложения (звонок, сообщение).</summary>
public enum ActionLevel
{
    /// <summary>Открывает готовый звонок или черновик, дальше человек нажимает сам.</summary>
    Manual = 0,
    /// <summary>Спрашивает «да/нет» голосом или текстом и после «да» делает сам.</summary>
    Confirm = 1,
    /// <summary>Полный цикл: делает сразу, как только попросили.</summary>
    Auto = 2
}

/// <summary>Сколько хранить историю чата на устройстве.</summary>
public enum ChatKeep
{
    Never = 0,
    Week = 1,
    Forever = 2
}

/// <summary>Общие настройки приложения (хранятся в PlayerPrefs). Настройки модели лежат в LlmSettings и LlmProfiles.</summary>
public static class AppSettings
{
    private const string ConfiguredKey = "app.configured";
    private const string SpeakKey = "app.speak";
    private const string ActionsKey = "app.actions";

    /// <summary>Пользователь уже нажимал «Готово» в окне настроек: кнопка «Настрой меня» на стартовом экране больше не нужна.</summary>
    public static bool Configured
    {
        get { return PlayerPrefs.GetInt(ConfiguredKey, 0) != 0; }
        set { PlayerPrefs.SetInt(ConfiguredKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static ActionLevel Actions
    {
        get
        {
            int value = PlayerPrefs.GetInt(ActionsKey, 0);
            return value == 2 ? ActionLevel.Auto : value == 1 ? ActionLevel.Confirm : ActionLevel.Manual;
        }
        set { PlayerPrefs.SetInt(ActionsKey, (int)value); PlayerPrefs.Save(); }
    }

    private const string ChatKeepKey = "app.chatkeep";
    private const string ChatTrashKey = "app.chattrash";

    /// <summary>Хранение истории чата между запусками (по умолчанию неделя).</summary>
    public static ChatKeep ChatHistory
    {
        get
        {
            int v = PlayerPrefs.GetInt(ChatKeepKey, 1);
            return v == 2 ? ChatKeep.Forever : v == 0 ? ChatKeep.Never : ChatKeep.Week;
        }
        set { PlayerPrefs.SetInt(ChatKeepKey, (int)value); PlayerPrefs.Save(); }
    }

    /// <summary>Смахнутые сообщения: true - в корзину на неделю, false - сразу насовсем.</summary>
    public static bool ChatTrash
    {
        get { return PlayerPrefs.GetInt(ChatTrashKey, 1) != 0; }
        set { PlayerPrefs.SetInt(ChatTrashKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static string ChatKeepTitle(ChatKeep keep)
    {
        switch (keep)
        {
            case ChatKeep.Forever: return "История чата: хранить всегда";
            case ChatKeep.Never: return "История чата: не хранить";
            default: return "История чата: хранить неделю";
        }
    }

    public static string ChatTrashTitle(bool trash)
    {
        return trash ? "Смахнутое: в корзину на неделю" : "Смахнутое: удалять насовсем";
    }

    public static string ActionsTitle(ActionLevel level)
    {
        switch (level)
        {
            case ActionLevel.Auto: return "Полный цикл: делаю сразу";
            case ActionLevel.Confirm: return "Спрашиваю «да/нет» и делаю сам";
            default: return "Вручную: открываю готовое, нажимаете вы";
        }
    }

    /// <summary>Озвучивать ответы на голосовые вопросы. Напоминания и таймеры озвучиваются всегда.</summary>
    public static bool SpeakReplies
    {
        get { return PlayerPrefs.GetInt(SpeakKey, 1) != 0; }
        set { PlayerPrefs.SetInt(SpeakKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
