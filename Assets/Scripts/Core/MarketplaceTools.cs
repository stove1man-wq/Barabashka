using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using Barabashka.MarketplaceProviders;
using UnityEngine;

namespace Barabashka.Core
{
    // Вспомогательный класс для разбора аргументов инструментов
    internal static class ToolArgs
    {
        public static string Get(IDictionary<string, string> args, string key)
        {
            if (args == null) return string.Empty;
            foreach (var kv in args)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value ?? string.Empty;
            return string.Empty;
        }
    }

    /// <summary>Поиск товара на маркетплейсе (открывает страницу поиска в браузере/приложении).</summary>
    public sealed class MarketplaceSearchTool : IAgentTool
    {
        public string Name { get { return "marketplace_search"; } }
        public string Description
        {
            get
            {
                return "найти товар на маркетплейсе. {\"tool\":\"marketplace_search\",\"query\":\"что искать\",\"provider\":\"ozon|wildberries|auto\"}; " +
                       "provider можно не указывать — будет использован маркетплейс по умолчанию или спросит пользователя.";
            }
        }

        public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
        {
            try
            {
                string query = ToolArgs.Get(args, "query");
                if (string.IsNullOrWhiteSpace(query))
                    return ToolResult.Fail("Не услышал, что искать. Скажите, например: «найди на Озоне наушники».");

                string providerId = ToolArgs.Get(args, "provider");
                IMarketplaceProvider provider;

                if (string.Equals(providerId, "auto", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(providerId))
                {
                    provider = MarketplaceProviderRegistry.Default;
                    if (MarketplaceProviderRegistry.ShouldAsk)
                    {
                        return ToolResult.Ok("ASK_PROVIDER|" + Uri.EscapeDataString(query));
                    }
                }
                else
                {
                    provider = MarketplaceProviderRegistry.GetById(providerId);
                }

                if (provider == null)
                    return ToolResult.Fail("Не нашёл маркетплейс «" + providerId + "». Доступны: Озон, Вайлдберриз.");

                string url = provider.BuildSearchUrl(query);
                string result = await OpenMarketplaceUrlAsync(url, provider.DisplayName, query);
                return ToolResult.Ok(result);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MarketplaceSearch] " + ex);
                return ToolResult.Fail("Не удалось выполнить поиск: " + ex.Message);
            }
        }

        private static Task<string> OpenMarketplaceUrlAsync(string url, string marketplaceName, string query)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", url))
                {
                    intent.Call<AndroidJavaObject>("setData", uri);
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK

                    var packageManager = activity.Call<AndroidJavaObject>("getPackageManager");
                    var resolveInfo = packageManager.Call<AndroidJavaObject>("resolveActivity", intent, 0);
                    if (resolveInfo != null)
                    {
                        activity.Call("startActivity", intent);
                        return Task.FromResult("Открываю " + marketplaceName + " по запросу «" + query + "».");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MarketplaceSearch] Deep link failed: " + ex.Message);
            }
#endif
            Application.OpenURL(url);
            return Task.FromResult("Открываю " + marketplaceName + " в браузере по запросу «" + query + "».");
        }
    }
}