using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using Barabashka.MarketplaceProviders;
using UnityEngine;

namespace Barabashka.Core
{
    /// <summary>Открытие ссылки на товар маркетплейса (из буфера обмена или чата).</summary>
    public sealed class MarketplaceOpenTool : IAgentTool
    {
        public string Name { get { return "marketplace_open"; } }
        public string Description
        {
            get
            {
                return "открыть ссылку на товар маркетплейса. {\"tool\":\"marketplace_open\",\"url\":\"https://...\"}; " +
                       "URL можно не указывать — возьмётся из буфера обмена или последнего сообщения в чате.";
            }
        }

        public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
        {
            try
            {
                string url = ToolArgs.Get(args, "url");

                // Если URL не передан, пробуем взять из буфера обмена
                if (string.IsNullOrWhiteSpace(url))
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    try
                    {
                        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                        using (var clipboardManager = activity.Call<AndroidJavaObject>("getSystemService", "clipboard"))
                        using (var clipData = clipboardManager.Call<AndroidJavaObject>("getPrimaryClip"))
                        {
                            if (clipData != null)
                            {
                                int count = clipData.Call<int>("getItemCount");
                                if (count > 0)
                                {
                                    using (var item = clipData.Call<AndroidJavaObject>("getItemAt", 0))
                                    using (var text = item.Call<AndroidJavaObject>("getText"))
                                    {
                                        if (text != null)
                                            url = text.Call<string>("toString");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception) { }
#endif
                }

                if (string.IsNullOrWhiteSpace(url))
                    return ToolResult.Fail("Нет ссылки для открытия. Укажите URL или скопируйте его в буфер обмена.");

                // Ищем провайдера по URL
                var provider = MarketplaceProviderRegistry.FindByProductUrl(url);
                if (provider == null)
                {
                    // Если не нашли конкретного провайдера, пробуем открыть как есть
                    Application.OpenURL(url);
                    return ToolResult.Ok("Открываю ссылку в браузере.");
                }

                // Проверяем, что URL действительно распарсился
                string productUrl;
                if (!provider.TryParseProductUrl(url, out productUrl))
                {
                    Application.OpenURL(url);
                    return ToolResult.Ok("Открываю ссылку в браузере.");
                }

                string result = await OpenMarketplaceUrlAsync(productUrl, provider.DisplayName);
                return ToolResult.Ok(result);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MarketplaceOpen] " + ex);
                return ToolResult.Fail("Не удалось открыть товар: " + ex.Message);
            }
        }

        private static Task<string> OpenMarketplaceUrlAsync(string url, string marketplaceName)
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
                        return Task.FromResult("Открываю " + marketplaceName + ".");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MarketplaceOpen] Deep link failed: " + ex.Message);
            }
#endif
            Application.OpenURL(url);
            return Task.FromResult("Открываю " + marketplaceName + " в браузере.");
        }
    }
}