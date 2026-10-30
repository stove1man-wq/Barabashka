using System;
using System.Collections.Generic;
using System.Text;
using Barabashka.Contracts;
using UnityEngine;

/// <summary>
/// Реестр возможностей агента: что он умеет, насколько это значимо, что нужно (разрешения, сеть, телефон) и можно ли это сделать сейчас.
/// Из него агент отвечает «могу / не могу, потому что…», а языковой модели показывает только то, что на этом устройстве вообще возможно.
/// </summary>
public static class CapabilityRegistry
{
    private const string DisabledKey = "cap.disabled";

    private static readonly List<CapabilityInfo> All = new List<CapabilityInfo>();
    private static readonly HashSet<string> Disabled = new HashSet<string>();
    private static bool _android;
    private static bool _loaded;

    private static readonly Dictionary<string, string> PermissionTitles = new Dictionary<string, string>
    {
        { PhoneComms.PermContacts, "контакты" },
        { PhoneComms.PermCall, "звонки" },
        { PhoneComms.PermSms, "отправка SMS" }
    };

    static CapabilityRegistry()
    {
        const NetworkNeed none = NetworkNeed.None;
        const NetworkNeed net = NetworkNeed.Required;

        Add("time", "Время", RiskLevel.Observe, none, false);
        Add("date", "Дата и день недели", RiskLevel.Observe, none, false);
        Add("calc", "Калькулятор", RiskLevel.Observe, none, false);
        Add("task_list", "Показать задачи", RiskLevel.Observe, none, false);
        Add("task_add", "Добавить задачу", RiskLevel.Prepare, none, false);
        Add("task_done", "Отметить задачу выполненной", RiskLevel.Prepare, none, false);
        Add("task_rename", "Переименовать задачу", RiskLevel.Prepare, none, false);
        Add("task_delete", "Удалить задачу", RiskLevel.Act, none, false).Irreversible = true;
        Add("note_add", "Записать заметку", RiskLevel.Prepare, none, false);
        Add("note_list", "Показать заметки", RiskLevel.Observe, none, false);
        Add("note_delete", "Удалить заметку", RiskLevel.Act, none, false).Irreversible = true;
        Add("reminder_add", "Напоминание или таймер", RiskLevel.Prepare, none, false);
        Add("reminder_list", "Показать напоминания", RiskLevel.Observe, none, false);
        Add("reminder_delete", "Удалить напоминание", RiskLevel.Act, none, false);
        Add("weather", "Погода", RiskLevel.Observe, net, false);
        Add("set_city", "Запомнить город", RiskLevel.Prepare, none, false);
        Add("web_search", "Поиск в интернете", RiskLevel.Prepare, net, false);
        Add("open_site", "Открыть сайт", RiskLevel.Prepare, net, false);
        Add("open_url", "Открыть ссылку", RiskLevel.Prepare, net, false);
        Add("marketplace_search", "Поиск на маркетплейсе", RiskLevel.Prepare, net, false);
        Add("marketplace_open", "Открыть товар на маркетплейсе", RiskLevel.Prepare, net, false);
        Add("taxi_call", "Вызвать такси", RiskLevel.Prepare, NetworkNeed.Optional, true);
        Add("taxi_set_place", "Запомнить адрес дома или работы", RiskLevel.Prepare, NetworkNeed.Optional, false);
        Add("media_pick", "Выбрать фото, картинку или видео", RiskLevel.Prepare, none, false);
        Add("media_control", "Управление просмотром и видео", RiskLevel.Observe, none, false);
        Add("route", "Показать место на карте", RiskLevel.Prepare, net, false);
        Add("battery", "Заряд батареи", RiskLevel.Observe, none, false);
        Add("flashlight", "Фонарик", RiskLevel.Prepare, none, true);
        Add("volume", "Громкость", RiskLevel.Prepare, none, true);
        Add("phone_settings", "Настройки телефона", RiskLevel.Prepare, none, true);
        Add("open_app", "Открыть приложение", RiskLevel.Prepare, none, true);
        Add("alarm_set", "Поставить будильник", RiskLevel.Prepare, none, true).SelfGated = true;
        Add("alarm_show", "Показать будильники", RiskLevel.Observe, none, true);
        Add("dial", "Набрать номер", RiskLevel.Prepare, none, true);
        Add("call", "Позвонить", RiskLevel.Act, none, true, PhoneComms.PermContacts, PhoneComms.PermCall).SelfGated = true;
        Add("telegram_read", "Читать Telegram", RiskLevel.Observe, net, true).Connector = "telegram";
        Add("message", "Написать сообщение", RiskLevel.Act, none, true, PhoneComms.PermContacts, PhoneComms.PermSms).SelfGated = true;
    }

    private static CapabilityInfo Add(string id, string title, RiskLevel risk, NetworkNeed network, bool androidOnly, params string[] permissions)
    {
        var info = new CapabilityInfo
        {
            Id = id, Title = title, Risk = risk, Network = network, AndroidOnly = androidOnly,
            Permissions = permissions ?? new string[0]
        };
        All.Add(info);
        return info;
    }

    /// <summary>Возможность по идентификатору или началу названия; null, если не нашлась.</summary>
    public static CapabilityInfo Find(string spec)
    {
        string query = (spec ?? string.Empty).Trim().ToLowerInvariant();
        if (query.Length == 0) return null;
        CapabilityInfo found = Get(query);
        if (found != null) return found;
        foreach (CapabilityInfo info in All)
            if (info.Title.ToLowerInvariant().StartsWith(query, StringComparison.Ordinal)) return info;
        return null;
    }

    /// <summary>Вызывается при старте из главного потока: запоминает платформу и выключенные человеком возможности.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        _android = Application.platform == RuntimePlatform.Android;
        Disabled.Clear();
        try
        {
            foreach (string id in PlayerPrefs.GetString(DisabledKey, string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                Disabled.Add(id);
        }
        catch (Exception) { }
        _loaded = true;
    }

    public static IReadOnlyList<CapabilityInfo> List { get { return All; } }

    public static CapabilityInfo Get(string id)
    {
        foreach (CapabilityInfo info in All)
            if (string.Equals(info.Id, id, StringComparison.OrdinalIgnoreCase)) return info;
        return null;
    }

    /// <summary>Показывать ли возможность языковой модели: нет, если телефонная возможность на ПК или человек её выключил. Безопасно для любого потока.</summary>
    public static bool IsOffered(string id)
    {
        CapabilityInfo info = Get(id);
        if (info == null) return true;
        if (_loaded && Disabled.Contains(info.Id)) return false;
        return !(info.AndroidOnly && _loaded && !_android);
    }

    /// <summary>Состояние возможности сейчас. Только из главного потока (проверяет сеть и разрешения).</summary>
    public static CapabilityStatus Check(string id)
    {
        CapabilityInfo info = Get(id);
        if (info == null) return new CapabilityStatus { State = CapabilityState.Available };

        if (Disabled.Contains(info.Id))
            return Status(CapabilityState.Disabled, "вы её выключили (вернуть: /возможности включить " + info.Id + ")");
        if (info.AndroidOnly && !_android)
            return Status(CapabilityState.Unsupported, "работает только на телефоне");
        if (info.Network == NetworkNeed.Required && Application.internetReachability == NetworkReachability.NotReachable)
            return Status(CapabilityState.Offline, "нужна сеть, а интернета сейчас нет");

        var missing = new List<string>();
        foreach (string permission in info.Permissions)
            if (!PhoneComms.HasPermission(permission))
                missing.Add(PermissionTitles.ContainsKey(permission) ? PermissionTitles[permission] : permission);
        if (missing.Count > 0)
            return Status(CapabilityState.NeedsPermission, "нужно разрешение: " + string.Join(", ", missing.ToArray()) + " (запрошу при первом использовании)");

        if (!string.IsNullOrEmpty(info.Connector))
        {
            IConnector connector = ConnectorRegistry.Get(info.Connector);
            if (connector == null || connector.State == ConnectorState.NotConfigured)
                return Status(CapabilityState.NeedsSetup, "нужно подключение: " + (connector != null ? connector.Title : info.Connector) + " (/подключения)");
            if (connector.State == ConnectorState.Offline)
                return Status(CapabilityState.Offline, "нет связи со службой: " + connector.Title);
        }

        return Status(CapabilityState.Available, string.Empty);
    }

    private static CapabilityStatus Status(CapabilityState state, string reason)
    {
        return new CapabilityStatus { State = state, Reason = reason };
    }

    /// <summary>Краткий отчёт «что я умею и что сейчас недоступно».</summary>
    public static string Describe()
    {
        var ready = new List<string>();
        var permission = new List<string>();
        var blocked = new List<string>();
        foreach (CapabilityInfo info in All)
        {
            CapabilityStatus status = Check(info.Id);
            switch (status.State)
            {
                case CapabilityState.Available: ready.Add(info.Title); break;
                case CapabilityState.NeedsPermission: permission.Add(info.Title + " - " + status.Reason); break;
                default: blocked.Add(info.Title + " - " + status.Reason); break;
            }
        }

        var sb = new StringBuilder();
        sb.Append("Могу сейчас: ").Append(string.Join(", ", ready.ToArray())).Append('.');
        if (permission.Count > 0) sb.Append("\nНужно разрешение: ").Append(string.Join("; ", permission.ToArray())).Append('.');
        if (blocked.Count > 0) sb.Append("\nСейчас не могу: ").Append(string.Join("; ", blocked.ToArray())).Append('.');
        sb.Append("\nВыключить или включить возможность: /возможности выключить | включить <название>.");
        return sb.ToString();
    }

    /// <summary>Выключить или включить возможность по названию или идентификатору.</summary>
    public static string Toggle(string spec, bool enable)
    {
        string query = (spec ?? string.Empty).Trim().ToLowerInvariant();
        if (query.Length == 0) return "Назовите возможность, например: /возможности выключить call";

        CapabilityInfo found = Get(query);
        if (found == null)
            foreach (CapabilityInfo info in All)
                if (info.Title.ToLowerInvariant().StartsWith(query, StringComparison.Ordinal)) { found = info; break; }
        if (found == null) return "Не нашёл такую возможность. Список: /возможности";

        if (enable) Disabled.Remove(found.Id); else Disabled.Add(found.Id);
        try
        {
            PlayerPrefs.SetString(DisabledKey, string.Join(",", new List<string>(Disabled).ToArray()));
            PlayerPrefs.Save();
        }
        catch (Exception) { }
        return (enable ? "Включено: " : "Выключено: ") + found.Title + ".";
    }
}
