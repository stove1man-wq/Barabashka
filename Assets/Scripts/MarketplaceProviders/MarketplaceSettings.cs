using System;
using UnityEngine;

namespace Barabashka.MarketplaceProviders
{
    /// <summary>Настройки маркетплейсов (хранятся в PlayerPrefs).</summary>
    public static class MarketplaceSettings
    {
        private const string DefaultProviderKey = "marketplace.default_provider";

        /// <summary>
        /// Идентификатор маркетплейса по умолчанию: "ozon", "wildberries" или "ask" (спрашивать каждый раз).
        /// </summary>
        public static string DefaultProviderId
        {
            get
            {
                try
                {
                    string value = PlayerPrefs.GetString(DefaultProviderKey, "ask");
                    return string.IsNullOrEmpty(value) ? "ask" : value;
                }
                catch (Exception)
                {
                    return "ask";
                }
            }
            set
            {
                try
                {
                    if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(DefaultProviderKey);
                    else PlayerPrefs.SetString(DefaultProviderKey, value);
                    PlayerPrefs.Save();
                }
                catch (Exception) { }
            }
        }

        /// <summary>Человекочитаемое название текущей настройки.</summary>
        public static string DefaultProviderTitle
        {
            get
            {
                switch (DefaultProviderId)
                {
                    case "ozon": return "Маркетплейс по умолчанию: Озон";
                    case "wildberries": return "Маркетплейс по умолчанию: Вайлдберриз";
                    default: return "Маркетплейс по умолчанию: спрашивать";
                }
            }
        }

        /// <summary>Следующее значение при циклическом переключении.</summary>
        public static string NextDefaultProviderId()
        {
            string current = DefaultProviderId;
            string next = current == "ozon" ? "wildberries" : current == "wildberries" ? "ask" : "ozon";
            DefaultProviderId = next;
            return next;
        }
    }
}