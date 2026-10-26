using System;

namespace Barabashka.Contracts
{
    /// <summary>
    /// Интерфейс провайдера маркетплейса: построение URL поиска и разбор ссылок на товары.
    /// Реализации оформляются как отдельные плагины (сборка Barabashka.MarketplaceProviders).
    /// </summary>
    public interface IMarketplaceProvider
    {
        /// <summary>Уникальный идентификатор провайдера (ozon, wildberries).</summary>
        string Id { get; }

        /// <summary>Отображаемое название («Озон», «Вайлдберриз»).</summary>
        string DisplayName { get; }

        /// <summary>Строит URL для поиска по запросу.</summary>
        string BuildSearchUrl(string query);

        /// <summary>
        /// Пытается разобрать URL как ссылку на товар данного маркетплейса.
        /// Возвращает true и заполняет productUrl, если URL принадлежит этому маркетплейсу и выглядит как карточка товара.
        /// </summary>
        bool TryParseProductUrl(string url, out string productUrl);
    }
}