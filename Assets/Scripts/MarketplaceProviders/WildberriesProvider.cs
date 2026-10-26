using System;
using System.Text.RegularExpressions;
using Barabashka.Contracts;

namespace Barabashka.MarketplaceProviders
{
    /// <summary>
    /// Провайдер для Вайлдберриз (wildberries.ru).
    /// Поиск: https://www.wildberries.ru/catalog/0/search.aspx?search=...
    /// Товар: https://www.wildberries.ru/catalog/12345678/detail.aspx или короткие ссылки wb.ru/...
    /// </summary>
    public sealed class WildberriesProvider : IMarketplaceProvider
    {
        public string Id => "wildberries";
        public string DisplayName => "Вайлдберриз";

        // Регулярка для ссылок на товары Вайлдберриз
        // Основной формат: https://www.wildberries.ru/catalog/{id}/detail.aspx
        // Короткие ссылки: https://wb.ru/catalog/{id}/detail.aspx или https://wildberries.ru/catalog/{id}/detail.aspx
        private static readonly Regex ProductUrlRegex = new Regex(
            @"^https?://(?:www\.)?(?:wildberries\.ru|wb\.ru)/catalog/\d+/detail\.aspx",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string BuildSearchUrl(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return "https://www.wildberries.ru/catalog/0/search.aspx?search=";
            string encoded = Uri.EscapeDataString(query.Trim());
            return $"https://www.wildberries.ru/catalog/0/search.aspx?search={encoded}";
        }

        public bool TryParseProductUrl(string url, out string productUrl)
        {
            productUrl = null;
            if (string.IsNullOrWhiteSpace(url)) return false;
            Match match = ProductUrlRegex.Match(url.Trim());
            if (match.Success)
            {
                productUrl = match.Value;
                return true;
            }
            return false;
        }
    }
}