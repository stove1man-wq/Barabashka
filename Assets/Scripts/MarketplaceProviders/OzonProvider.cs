using System;
using System.Text.RegularExpressions;
using Barabashka.Contracts;

namespace Barabashka.MarketplaceProviders
{
    /// <summary>
    /// Провайдер для Озон (ozon.ru).
    /// Поиск: https://www.ozon.ru/search/?text=...
    /// Товар: https://www.ozon.ru/product/... или https://www.ozon.ru/context/detail/id/...
    /// </summary>
    public sealed class OzonProvider : IMarketplaceProvider
    {
        public string Id => "ozon";
        public string DisplayName => "Озон";

        private static readonly Regex ProductUrlRegex = new Regex(
            @"^https?://(?:www\.)?ozon\.ru/(?:product/|context/detail/id/)[^/\s?#]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string BuildSearchUrl(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return "https://www.ozon.ru/search/?text=";
            string encoded = Uri.EscapeDataString(query.Trim());
            return $"https://www.ozon.ru/search/?text={encoded}";
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