using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Barabashka.Contracts
{
    /// <summary>
    /// Где находится служба и, значит, при каких условиях она доступна.
    /// None - сеть не нужна; Domestic - российские службы (Ozon, Wildberries, Яндекс, MAX, RuTube, VK);
    /// Foreign - нужен доступ за границу (может не работать без VPN, а VPN в России ненадёжен).
    /// Это другая ось, чем <see cref="NetworkNeed"/>: NetworkNeed отвечает «нужна ли сеть вообще», NetworkZone - «куда именно».
    /// </summary>
    public enum NetworkZone { None, Domestic, Foreign }

    /// <summary>Описание адресов одной службы: основной и запасные, свой прокси, таймауты.</summary>
    public sealed class EndpointProfile
    {
        /// <summary>Идентификатор службы: «llm-ollama», «llm-gigachat», «label-server», «max-bot» и т.п.</summary>
        public string ServiceId;
        public NetworkZone Zone = NetworkZone.Domestic;
        /// <summary>Адреса по порядку: первый основной, дальше запасные.</summary>
        public List<string> Urls = new List<string>();
        /// <summary>Прокси только для этой службы (например socks5://host:port); null - без прокси.</summary>
        public string ProxyUrl;
        public int ConnectTimeoutSeconds = 5;
        public int RequestTimeoutSeconds = 20;
        /// <summary>Сколько минут можно показывать последний удачный ответ, если служба недоступна; 0 - не кэшировать.</summary>
        public int CacheMinutes;
    }

    /// <summary>
    /// Единое место, которое знает, что сейчас доступно. Остальные блоки не проверяют сеть сами:
    /// они спрашивают здесь и при отказе мягко деградируют (понятное сообщение, без падений).
    /// Реализация лежит в отдельной сборке, соединяется в Composition.
    /// </summary>
    public interface IConnectivity
    {
        /// <summary>Есть ли вообще выход в сеть (быстрая проверка без запросов).</summary>
        bool HasInternet { get; }

        /// <summary>Профиль службы или null, если такой нет.</summary>
        EndpointProfile GetProfile(string serviceId);

        /// <summary>Добавить или заменить профиль (настройки, импорт из конфигурации).</summary>
        void SetProfile(EndpointProfile profile);

        /// <summary>
        /// Выбрать первый живой адрес службы: пробует основной, потом запасные, учитывает таймауты и прокси профиля.
        /// null - ничего не отвечает; вызывающий решает, показать ли кэш или сообщение.
        /// </summary>
        Task<string> PickAliveUrlAsync(string serviceId, CancellationToken ct);

        /// <summary>Запомнить удачный ответ службы (для режима без связи). Ключ выбирает вызывающий.</summary>
        void PutCache(string serviceId, string key, string value);

        /// <summary>Последний удачный ответ, если он не старше CacheMinutes профиля.</summary>
        bool TryGetCache(string serviceId, string key, out string value);

        /// <summary>Сработает, когда изменилось состояние сети или доступность служб.</summary>
        event Action Changed;
    }
}
