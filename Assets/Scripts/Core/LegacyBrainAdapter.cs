using System;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;

/// <summary>
/// Переходный адаптер: старый IAgentBrain (строка -> строка) под новый IBrain.
/// Строки вида "COMMAND:XXX|арг" превращаются в AgentResponse.Command ("XXX|арг").
/// Удалить, когда IsolatedBrain будет реализовывать IBrain напрямую.
/// Токен отмены старым IAgentBrain не поддерживается и здесь игнорируется.
/// </summary>
public sealed class LegacyBrainAdapter : IBrain
{
    private const string CommandPrefix = "COMMAND:";
    private readonly IAgentBrain _inner;

    public LegacyBrainAdapter(IAgentBrain inner)
    {
        if (inner == null) throw new ArgumentNullException("inner");
        _inner = inner;
    }

    public string Id { get { return "isolated"; } }
    public bool RequiresNetwork { get { return false; } }

    public async Task<AgentResponse> RespondAsync(Utterance input, CancellationToken ct)
    {
        string raw = await _inner.ProcessMessageAsync(input != null ? input.Text : null);
        var response = new AgentResponse { LanguageCode = input != null ? input.LanguageCode : null };

        if (raw != null && raw.StartsWith(CommandPrefix, StringComparison.Ordinal))
            response.Command = raw.Substring(CommandPrefix.Length);
        else
            response.Text = raw;

        return response;
    }
}
