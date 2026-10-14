using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Barabashka.Contracts
{
    /// <summary>
    /// Инструмент, который агент может вызвать (время, браузер, позже - системные действия).
    /// </summary>
    public interface IAgentTool
    {
        string Name { get; }
        string Description { get; }
        Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct);
    }

    /// <summary>Реестр инструментов, доступных в текущем профиле.</summary>
    public interface IToolRegistry
    {
        IReadOnlyList<IAgentTool> Tools { get; }
        bool TryGet(string name, out IAgentTool tool);
    }

    /// <summary>
    /// "Мышление" агента нового образца. Принимает финальную реплику, возвращает ответ.
    /// Существующий IAgentBrain переносится в Contracts отдельной задачей (см. TASKS.md, T3),
    /// после чего IsolatedBrain оборачивается в этот интерфейс.
    /// </summary>
    public interface IBrain
    {
        string Id { get; }
        /// <summary>true, если для работы нужна сеть (облачная модель).</summary>
        bool RequiresNetwork { get; }
        Task<AgentResponse> RespondAsync(Utterance input, CancellationToken ct);
    }
}
