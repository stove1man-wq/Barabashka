namespace Barabashka.Contracts
{
    /// <summary>Как человек подключает внешнюю службу.</summary>
    public enum AuthKind { None, ApiKey, Token, OAuth, UserLogin }

    public enum ConnectorState { NotConfigured, Connected, Error, Offline }

    /// <summary>
    /// Связь с внешним приложением или службой (Telegram, языковые модели, сервисы билетов и т.д.).
    /// Секреты (ключи, токены) хранятся только в SecretStore и не попадают в журналы, подсказки моделей и чат.
    /// </summary>
    public interface IConnector
    {
        string Id { get; }
        string Title { get; }
        AuthKind Auth { get; }
        ConnectorState State { get; }
        /// <summary>Пояснение к состоянию для человека («ключ задан», «нужен вход»).</summary>
        string Note { get; }
    }
}
