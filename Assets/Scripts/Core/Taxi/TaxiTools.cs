using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using Barabashka.TaxiProviders;
using UnityEngine;

namespace Barabashka.Core
{
    /// <summary>Результат разбора «куда ехать»: найденная точка, текст адреса или жёсткая ошибка.</summary>
    internal sealed class TaxiDestination
    {
        public TaxiPlace Place;     // координаты найдены
        public string Text;         // как адрес называется (для буфера обмена и ответа)
        public string Problem;      // нельзя продолжать (например, дом не задан)
    }

    internal static class TaxiResolver
    {
        /// <summary>spec: «home», «work» или адрес словами.</summary>
        public static async Task<TaxiDestination> ResolveAsync(string spec, CancellationToken ct)
        {
            string value = (spec ?? string.Empty).Trim();
            string slot = value == TaxiSettings.Home || value == TaxiSettings.Work ? value : null;
            if (slot == null)
            {
                TaxiPlace found = await AgentGeocoder.FindAsync(value, ct);
                return new TaxiDestination { Place = found, Text = value };
            }

            string address = TaxiSettings.GetAddress(slot);
            if (string.IsNullOrEmpty(address))
            {
                string where = slot == TaxiSettings.Work ? "работы" : "дома";
                return new TaxiDestination
                {
                    Problem = "Адрес " + where + " не задан. Скажите, например: «" +
                              (slot == TaxiSettings.Work ? "моя работа" : "мой дом") + " Тверская 1», или задайте его в настройках, страница «Такси»."
                };
            }

            TaxiPlace cached = TaxiSettings.GetCached(slot);
            if (cached != null) return new TaxiDestination { Place = cached, Text = address };

            TaxiPlace place = await AgentGeocoder.FindAsync(address, ct);
            if (place != null) TaxiSettings.SetCached(slot, place);
            return new TaxiDestination { Place = place, Text = address };
        }
    }

    internal static class TaxiPicker
    {
        /// <summary>
        /// Выбор оператора: названный голосом, затем из настроек, затем первый установленный. app - его приложение на телефоне.
        /// null, если подходящего нет; тогда note объясняет почему.
        /// </summary>
        public static ITaxiProvider Pick(string requestedId, IList<PhoneActions.AppEntry> apps, out PhoneActions.AppEntry app, out string note)
        {
            app = null;
            note = string.Empty;

            if (!string.IsNullOrEmpty(requestedId) && !string.Equals(requestedId, TaxiSettings.Auto, StringComparison.OrdinalIgnoreCase))
            {
                ITaxiProvider named = TaxiProviderRegistry.GetById(requestedId);
                if (named == null) { note = "Не знаю такого такси. Могу: Яндекс Go, Ситимобил, Максим, inDrive."; return null; }
                app = FindApp(named, apps);
                if (app == null) { note = named.DisplayName + " не найден на телефоне."; return null; }
                return named;
            }

            string prefix = string.Empty;
            string configured = TaxiSettings.Operator;
            if (!string.Equals(configured, TaxiSettings.Auto, StringComparison.OrdinalIgnoreCase))
            {
                ITaxiProvider chosen = TaxiProviderRegistry.GetById(configured);
                if (chosen != null)
                {
                    app = FindApp(chosen, apps);
                    if (app != null) return chosen;
                    prefix = "Выбранный оператор «" + chosen.DisplayName + "» не найден на телефоне, ищу другой. ";
                }
            }

            foreach (ITaxiProvider provider in TaxiProviderRegistry.All)
            {
                app = FindApp(provider, apps);
                if (app != null) { note = prefix; return provider; }
            }

            note = prefix + "Не нашёл на телефоне приложение такси (Яндекс Go, Ситимобил, Максим, inDrive). Установите одно из них.";
            return null;
        }

        private static PhoneActions.AppEntry FindApp(ITaxiProvider provider, IList<PhoneActions.AppEntry> apps)
        {
            foreach (string name in provider.AppNames)
            {
                PhoneActions.AppEntry entry = PhoneActions.MatchApp(name, apps);
                if (entry != null) return entry;
            }
            return null;
        }
    }

    /// <summary>
    /// Вызов такси. Агент открывает приложение оператора (с готовым маршрутом, если оператор это умеет) и останавливается:
    /// «Заказать» нажимает человек, поездка и оплата - его решение. Риск Prepare.
    /// </summary>
    public sealed class TaxiCallTool : IAgentTool
    {
        public string Name { get { return "taxi_call"; } }
        public string Description
        {
            get
            {
                return "вызвать такси: открывает приложение такси с маршрутом, заказ подтверждает человек. " +
                       "{\"tool\":\"taxi_call\",\"to\":\"адрес словами | home | work (можно не указывать)\",\"provider\":\"yandex-go|citymobil|maxim|indrive|auto\"}";
            }
        }

        public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
        {
            try
            {
                string to = ToolArgs.Get(args, "to");
                string providerId = ToolArgs.Get(args, "provider");

                List<PhoneActions.AppEntry> apps = PhoneActions.ListApps();
                if (apps.Count == 0) return ToolResult.Fail("Такси открывается только на телефоне.");

                PhoneActions.AppEntry app;
                string note;
                ITaxiProvider provider = TaxiPicker.Pick(providerId, apps, out app, out note);
                if (provider == null) return ToolResult.Fail(note);

                if (string.IsNullOrWhiteSpace(to))
                {
                    PhoneActions.Launch(app);
                    return ToolResult.Ok(note + "Открываю " + provider.DisplayName + ". Куда ехать, укажите в приложении.");
                }

                TaxiDestination dest = await TaxiResolver.ResolveAsync(to, ct);
                if (dest.Problem != null) return ToolResult.Fail(dest.Problem);

                if (dest.Place != null && provider.SupportsRoute)
                {
                    string url = provider.BuildRouteUrl(dest.Place);
                    OpenRoute(url, provider.RoutePackage);
                    return ToolResult.Ok(note + "Нашёл: " + dest.Place.Title + ". Открываю " + provider.DisplayName +
                                         " с готовым маршрутом. Проверьте адрес и нажмите «Заказать» сами.");
                }

                // Маршрут ссылкой не передать (оператор не умеет или адрес не нашёлся): открываем приложение, адрес кладём в буфер.
                GUIUtility.systemCopyBuffer = dest.Text;
                PhoneActions.Launch(app);
                string why = dest.Place == null
                    ? "Не смог найти адрес на карте (нет сети или адрес неточный)."
                    : provider.DisplayName + " не принимает маршрут по ссылке.";
                return ToolResult.Ok(note + why + " Открываю " + provider.DisplayName + ", адрес «" + dest.Text +
                                     "» скопировал: вставьте его в поле «Куда».");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Taxi] " + ex);
                return ToolResult.Fail("Не удалось вызвать такси: " + ex.Message);
            }
        }

        // Ссылку сначала отдаём самому приложению оператора (чтобы не через браузер); если не вышло, обычным открытием ссылки.
        private static void OpenRoute(string url, string package)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(package))
            {
                try
                {
                    using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW"))
                    using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                    using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", url))
                    {
                        intent.Call<AndroidJavaObject>("setData", uri);
                        intent.Call<AndroidJavaObject>("setPackage", package);
                        intent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
                        activity.Call("startActivity", intent);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Taxi] Ссылка в приложение не ушла, открываю обычным способом: " + ex.Message);
                }
            }
#endif
            Application.OpenURL(url);
        }
    }

    /// <summary>Запомнить адрес дома или работы для команд «такси домой» и «такси на работу».</summary>
    public sealed class TaxiSetPlaceTool : IAgentTool
    {
        public string Name { get { return "taxi_set_place"; } }
        public string Description
        {
            get { return "запомнить адрес дома или работы для такси. {\"tool\":\"taxi_set_place\",\"slot\":\"home|work\",\"address\":\"адрес словами\"}"; }
        }

        public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
        {
            try
            {
                string slot = string.Equals(ToolArgs.Get(args, "slot"), TaxiSettings.Work, StringComparison.OrdinalIgnoreCase)
                    ? TaxiSettings.Work : TaxiSettings.Home;
                string address = ToolArgs.Get(args, "address").Trim();
                if (address.Length == 0) return ToolResult.Fail("Не услышал адрес. Скажите, например: «мой дом Тверская 1».");

                TaxiSettings.SetAddress(slot, address);
                TaxiPlace place = await AgentGeocoder.FindAsync(address, ct);
                string title = TaxiSettings.SlotTitle(slot);
                if (place == null)
                    return ToolResult.Ok(title + " сохранён: «" + address + "». Проверить адрес на карте не получилось (нет сети или адрес неточный): проверю при вызове такси.");

                TaxiSettings.SetCached(slot, place);
                return ToolResult.Ok(title + " сохранён. Нашёл: " + place.Title + ". Если это не тот адрес, назовите точнее.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Taxi] " + ex);
                return ToolResult.Fail("Не удалось сохранить адрес: " + ex.Message);
            }
        }
    }
}
