using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Barabashka.Contracts
{
    /// <summary>Сообщение из мессенджера в общем виде, не зависящем от провайдера.</summary>
    [Serializable]
    public sealed class MessengerMessage
    {
        public string Provider;
        public string ChatId;
        public string ChatTitle;
        public string Sender;
        public string Text;
        public bool Incoming = true;
        public DateTime TimestampUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Провайдер мессенджера. Идентификаторы: «max-notifications» (уведомления и интенты), «max-bot» (бот MAX),
    /// «telegram-tdlib» (приостановлен). Подробнее: docs/MESSENGERS.md.
    /// Отправка от имени человека относится к действиям уровня Act: подчиняется настройке «Действия агента»
    /// и не может обходить её (это проверяет вызывающий инструмент, не провайдер).
    /// </summary>
    public interface IMessengerConnector : IConnector
    {
        NetworkZone Zone { get; }

        /// <summary>Умеет читать входящие (уведомления, события бота).</summary>
        bool CanRead { get; }

        /// <summary>Умеет отвечать без участия человека (бот). Иначе ответ готовится в открытом чате.</summary>
        bool CanReplyDirectly { get; }

        /// <summary>Приходят новые входящие сообщения.</summary>
        event Action<MessengerMessage> OnIncoming;

        /// <summary>Последние сообщения; who - имя собеседника или чата, пусто - из всех. Список может быть пустым.</summary>
        Task<IReadOnlyList<MessengerMessage>> ReadRecentAsync(string who, int limit, CancellationToken ct);

        /// <summary>
        /// Ответ. Если провайдер не умеет отправлять сам, открывает чат с готовым текстом и сообщает об этом в результате.
        /// </summary>
        Task<ToolResult> ReplyAsync(string who, string text, CancellationToken ct);

        /// <summary>Открыть чат в приложении мессенджера (диплинк); false, если приложение не найдено.</summary>
        bool TryOpenChat(string who);
    }

    /// <summary>Набор провайдеров и выбор активного: настройкой и командой голосом или текстом («мессенджер макс»).</summary>
    public interface IMessengerHub
    {
        IReadOnlyList<IMessengerConnector> Providers { get; }

        /// <summary>Активный провайдер или null, если ни один не подключён.</summary>
        IMessengerConnector Active { get; }

        /// <summary>Переключить активный провайдер по идентификатору; false, если такого нет.</summary>
        bool SetActive(string id);

        event Action ActiveChanged;
    }
}
