using System;
using System.Collections.Generic;
using System.Text;
using Barabashka.Contracts;

/// <summary>
/// Реестр инструментов нового образца (IAgentTool). Сюда подключаются новые возможности агента;
/// старые команды (время, задачи, браузер) пока обрабатывает роутер напрямую и переедут сюда по одной.
/// Инструмент вызывается командой «TOOL|имя|ключ=значение&amp;ключ=значение» (значения закодированы).
/// </summary>
public sealed class AgentToolRegistry : IToolRegistry
{
    public static readonly AgentToolRegistry Default = CreateDefault();

    private readonly List<IAgentTool> _tools = new List<IAgentTool>();

    public IReadOnlyList<IAgentTool> Tools { get { return _tools; } }

    public void Register(IAgentTool tool)
    {
        if (tool == null) throw new ArgumentNullException("tool");
        _tools.Add(tool);
    }

    public bool TryGet(string name, out IAgentTool tool)
    {
        foreach (IAgentTool candidate in _tools)
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                tool = candidate;
                return true;
            }
        }
        tool = null;
        return false;
    }

    private static AgentToolRegistry CreateDefault()
    {
        var registry = new AgentToolRegistry();
        registry.Register(new ReminderAddTool());
        registry.Register(new ReminderListTool());
        registry.Register(new ReminderDeleteTool());
        registry.Register(new WeatherTool());
        registry.Register(new WeatherSetCityTool());
        registry.Register(new NoteAddTool());
        registry.Register(new NoteListTool());
        registry.Register(new NoteDeleteTool());
        registry.Register(new FlashlightTool());
        registry.Register(new VolumeTool());
        registry.Register(new BatteryTool());
        registry.Register(new WebSearchTool());
        registry.Register(new OpenSiteTool());
        registry.Register(new RouteTool());
        registry.Register(new DialTool());
        registry.Register(new PhoneSettingsTool());
        registry.Register(new OpenAppTool());
        registry.Register(new AlarmSetTool());
        registry.Register(new AlarmShowTool());
        registry.Register(new CallTool());
        registry.Register(new MessageTool());
        return registry;
    }

    public static string BuildQuery(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var sb = new StringBuilder();
        foreach (KeyValuePair<string, string> pair in pairs)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(pair.Key)).Append('=').Append(Uri.EscapeDataString(pair.Value ?? string.Empty));
        }
        return sb.ToString();
    }

    public static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return result;
        foreach (string part in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            result[Uri.UnescapeDataString(part.Substring(0, eq))] = Uri.UnescapeDataString(part.Substring(eq + 1));
        }
        return result;
    }
}
