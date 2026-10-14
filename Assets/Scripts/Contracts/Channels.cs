using System;
using System.Threading;
using System.Threading.Tasks;

namespace Barabashka.Contracts
{
    /// <summary>Канал ввода: текстовое поле, микрофон, позже уведомления и жесты.</summary>
    public interface IInputChannel
    {
        string Id { get; }
        /// <summary>Приходят и промежуточные (IsFinal=false), и финальные реплики.</summary>
        event Action<Utterance> OnUtterance;
        void Start();
        void Stop();
    }

    /// <summary>Канал вывода: экран, озвучка, аватар.</summary>
    public interface IOutputChannel
    {
        string Id { get; }
        Task PresentAsync(AgentResponse response, CancellationToken ct);
    }

    /// <summary>Возврат в главный поток Unity. Реализация лежит в сборке App.</summary>
    public interface IMainThread
    {
        void Post(Action action);
    }
}
