using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;

// Инструменты действий на телефоне (см. PhoneActions). Каждый возвращает короткий текст-результат.

public sealed class FlashlightTool : IAgentTool
{
    public string Name { get { return "flashlight"; } }
    public string Description { get { return "включить или выключить фонарик. {\"tool\":\"flashlight\",\"state\":\"on\"} или \"off\""; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        bool on = !string.Equals(ToolArgs.Get(args, "state"), "off", System.StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(ToolResult.Ok(PhoneActions.Flashlight(on)));
    }
}

public sealed class VolumeTool : IAgentTool
{
    public string Name { get { return "volume"; } }
    public string Description { get { return "громкость телефона. {\"tool\":\"volume\",\"action\":\"up|down|max|mute|set\",\"percent\":\"50\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        int percent;
        if (!int.TryParse(ToolArgs.Get(args, "percent"), out percent)) percent = -1;
        return Task.FromResult(ToolResult.Ok(PhoneActions.Volume(ToolArgs.Get(args, "action"), percent)));
    }
}

public sealed class BatteryTool : IAgentTool
{
    public string Name { get { return "battery"; } }
    public string Description { get { return "уровень заряда батареи. {\"tool\":\"battery\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        return Task.FromResult(ToolResult.Ok(PhoneActions.Battery()));
    }
}

public sealed class WebSearchTool : IAgentTool
{
    public string Name { get { return "web_search"; } }
    public string Description { get { return "поиск в интернете (откроется браузер). {\"tool\":\"web_search\",\"query\":\"что искать\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        return Task.FromResult(ToolResult.Ok(PhoneActions.WebSearch(ToolArgs.Get(args, "query"))));
    }
}

public sealed class OpenSiteTool : IAgentTool
{
    public string Name { get { return "open_site"; } }
    public string Description { get { return "открыть известный сайт. {\"tool\":\"open_site\",\"name\":\"youtube|vk|google|yandex|wikipedia|mail|maps\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        return Task.FromResult(ToolResult.Ok(PhoneActions.OpenSite(ToolArgs.Get(args, "name"))));
    }
}

public sealed class RouteTool : IAgentTool
{
    public string Name { get { return "route"; } }
    public string Description { get { return "показать место или маршрут на карте. {\"tool\":\"route\",\"place\":\"куда\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        return Task.FromResult(ToolResult.Ok(PhoneActions.Route(ToolArgs.Get(args, "place"))));
    }
}

public sealed class DialTool : IAgentTool
{
    public string Name { get { return "dial"; } }
    public string Description { get { return "набрать номер в телефоне (звонок не начнётся сам). {\"tool\":\"dial\",\"number\":\"+79001234567\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        return Task.FromResult(ToolResult.Ok(PhoneActions.Dial(ToolArgs.Get(args, "number"))));
    }
}

public sealed class PhoneSettingsTool : IAgentTool
{
    public string Name { get { return "phone_settings"; } }
    public string Description { get { return "открыть настройки телефона. {\"tool\":\"phone_settings\",\"section\":\"wifi|bluetooth|main\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        return Task.FromResult(ToolResult.Ok(PhoneActions.PhoneSettings(ToolArgs.Get(args, "section"))));
    }
}

public sealed class OpenAppTool : IAgentTool
{
    public string Name { get { return "open_app"; } }
    public string Description { get { return "открыть приложение на телефоне по названию. {\"tool\":\"open_app\",\"name\":\"телеграм\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        return Task.FromResult(ToolResult.Ok(PhoneActions.OpenApp(ToolArgs.Get(args, "name"))));
    }
}

public sealed class AlarmSetTool : IAgentTool
{
    private static readonly string[] Formats = { "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss" };

    public string Name { get { return "alarm_set"; } }
    public string Description { get { return "поставить будильник в системных часах. {\"tool\":\"alarm_set\",\"at\":\"ГГГГ-ММ-ДДTЧЧ:ММ\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        System.DateTime due;
        if (!System.DateTime.TryParseExact(ToolArgs.Get(args, "at"), Formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out due))
            return Task.FromResult(ToolResult.Fail("Не понял время будильника. Скажите, например: «разбуди меня в 7 утра»."));

        // Вручную: открываем системные часы с готовым будильником (остаётся нажать «сохранить»); иначе ставим сразу.
        bool showUi = AppSettings.Actions == ActionLevel.Manual;
        return Task.FromResult(ToolResult.Ok(PhoneActions.Alarm(due, showUi)));
    }
}

public sealed class AlarmShowTool : IAgentTool
{
    public string Name { get { return "alarm_show"; } }
    public string Description { get { return "показать будильники (чтобы выключить или удалить). {\"tool\":\"alarm_show\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        return Task.FromResult(ToolResult.Ok(PhoneActions.ShowAlarms()));
    }
}
