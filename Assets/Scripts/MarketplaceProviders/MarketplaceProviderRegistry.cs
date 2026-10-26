using System;
using System.Collections.Generic;
using Barabashka.Contracts;

namespace Barabashka.MarketplaceProviders
{
    /// <summary>
    /// Реестр доступных провайдеров маркетплейсов. Инициализируется извне (Composition/VoiceBootstrap).
    /// </summary>
    public static class MarketplaceProviderRegistry
    {
        private static readonly List<IMarketplaceProvider> _providers = new List<IMarketplaceProvider>();
        private static bool _initialized;

        /// <summary>Зарегистрировать провайдера. Вызывать при старте приложения.</summary>
        public static void Register(IMarketplaceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (!_providers.Exists(p => string.Equals(p.Id, provider.Id, StringComparison.OrdinalIgnoreCase)))
                _providers.Add(provider);
        }

        /// <summary>Получить всех зарегистрированных провайдеров.</summary>
        public static IReadOnlyList<IMarketplaceProvider> Providers
        {
            get
            {
                EnsureInitialized();
                return _providers;
            }
        }

        /// <summary>Найти провайдера по идентификатору (ozon, wildberries).</summary>
        public static IMarketplaceProvider GetById(string id)
        {
            EnsureInitialized();
            foreach (var p in _providers)
                if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                    return p;
            return null;
        }

        /// <summary>Найти провайдера, которому принадлежит URL товара.</summary>
        public static IMarketplaceProvider FindByProductUrl(string url)
        {
            EnsureInitialized();
            foreach (var p in _providers)
                if (p.TryParseProductUrl(url, out _))
                    return p;
            return null;
        }

        /// <summary>Провайдер по умолчанию (из настроек или первый в списке).</summary>
        public static IMarketplaceProvider Default
        {
            get
            {
                EnsureInitialized();
                string defaultId = MarketplaceSettings.DefaultProviderId;
                if (!string.IsNullOrEmpty(defaultId))
                {
                    var p = GetById(defaultId);
                    if (p != null) return p;
                }
                return _providers.Count > 0 ? _providers[0] : null;
            }
        }

        /// <summary>Требуется ли спрашивать пользователя, какой маркетплейс использовать.</summary>
        public static bool ShouldAsk
        {
            get
            {
                EnsureInitialized();
                return MarketplaceSettings.DefaultProviderId == "ask";
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            // Авто-регистрация встроенных провайдеров
            Register(new OzonProvider());
            Register(new WildberriesProvider());
        }
    }
}