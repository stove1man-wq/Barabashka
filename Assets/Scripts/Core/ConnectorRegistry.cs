using System.Collections.Generic;
using System.Text;
using Barabashka.Contracts;

/// <summary>Реестр коннекторов: внешние службы, к которым подключён агент. Сюда же в следующих этапах добавится Telegram.</summary>
public static class ConnectorRegistry
{
    private static readonly List<IConnector> Items = new List<IConnector> { new LlmConnector(), new TelegramConnector() };

    public static IReadOnlyList<IConnector> List { get { return Items; } }

    public static void Register(IConnector connector)
    {
        if (connector != null && Get(connector.Id) == null) Items.Add(connector);
    }

    public static IConnector Get(string id)
    {
        foreach (IConnector c in Items)
            if (string.Equals(c.Id, id, System.StringComparison.OrdinalIgnoreCase)) return c;
        return null;
    }

    public static string Describe()
    {
        var sb = new StringBuilder("Подключения:");
        foreach (IConnector c in Items)
        {
            string state;
            switch (c.State)
            {
                case ConnectorState.Connected: state = "подключено"; break;
                case ConnectorState.Offline: state = "нет сети"; break;
                case ConnectorState.Error: state = "ошибка"; break;
                default: state = "не настроено"; break;
            }
            sb.Append("\n- ").Append(c.Title).Append(": ").Append(state);
            if (!string.IsNullOrEmpty(c.Note)) sb.Append(" (").Append(c.Note).Append(')');
        }
        sb.Append("\nКлючи хранятся в зашифрованном виде и показываются только маской.");
        return sb.ToString();
    }
}

/// <summary>Языковые модели (Ollama, OpenRouter, Groq, модель на телефоне) как единое подключение.</summary>
public sealed class LlmConnector : IConnector
{
    public string Id { get { return "llm"; } }
    public string Title { get { return "Языковые модели"; } }
    public AuthKind Auth { get { return AuthKind.ApiKey; } }

    public ConnectorState State
    {
        get { return LlmProfiles.Chain().Count > 0 ? ConnectorState.Connected : ConnectorState.NotConfigured; }
    }

    public string Note
    {
        get
        {
            var names = new List<string>();
            foreach (LlmProfile p in LlmProfiles.Chain())
                names.Add(p.IsOpenAi ? p.name + ", " + LlmProfiles.MaskKey(p.apiKey) : p.name);
            return names.Count == 0 ? "модели выключены или не добавлены: /llm" : string.Join("; ", names.ToArray());
        }
    }
}
