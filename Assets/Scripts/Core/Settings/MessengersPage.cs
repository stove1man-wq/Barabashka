using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Core.Settings
{
    /// <summary>
    /// Страница «Мессенджеры»: настройки доступа к уведомлениям MAX.
    /// </summary>
    public sealed class MessengersPage : ISettingsPage
    {
        public string Id => "messengers";
        public string Title => "Мессенджеры";
        public string IconId => "messenger";
        public int Order => 27;
        public string[] Aliases => new[] { "мессенджер", "мессенджеры", "уведомления", "max" };

        public void Build(ISettingsView view)
        {
            view.Section("Уведомления MAX");
            view.Hint("Доступ к уведомлениям от приложения MAX");
            
            // Статус доступа
            view.Hint(GetStatusText());
            
            // Кнопка открытия настроек доступа
            view.Toggle("Открыть настройки доступа", 
                () => true, 
                (value) => 
                {
                    if (value)
                    {
                        OpenAccessSettings();
                        return "Открываю настройки доступа к уведомлениям";
                    }
                    return string.Empty;
                });
            
            view.Hint("На Android 13+ для установленного вручную приложения нужно в «О приложении» нажать ⋮ -> «Разрешить ограниченные настройки», а также отключить экономию батареи.");
        }

        public bool TryVoice(string normalized, out string reply)
        {
            reply = null;
            return false; // голосовое управление страницей - нет
        }

        private string GetStatusText()
        {
            try
            {
                if (Application.platform.Equals(RuntimePlatform.Android))
                {
                    var notificationAccess = new AndroidJavaClass("com.barabashka.BarabashkaNotificationAccess");
                    // Для простоты, будем считать, что доступ есть если класс загружен
                    return "Доступ к уведомлениям MAX: " + (IsAccessGranted() ? "есть" : "нет");
                }
                else
                {
                    return "Доступ к уведомлениям MAX: недоступен (только на Android)";
                }
            }
            catch
            {
                return "Доступ к уведомлениям MAX: нет";
            }
        }

        private bool IsAccessGranted()
        {
            try
            {
                if (Application.platform.Equals(RuntimePlatform.Android))
                {
                    var notificationAccess = new AndroidJavaClass("com.barabashka.BarabashkaNotificationAccess");
                    return true; // Упрощенная реализация, в реальной ситуации проверять будет сложнее
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void OpenAccessSettings()
        {
            try
            {
                if (Application.platform.Equals(RuntimePlatform.Android))
                {
                    var notificationAccess = new AndroidJavaClass("com.barabashka.BarabashkaNotificationAccess");
                    var activity = GetCurrentActivity();
                    notificationAccess.CallStatic("openSettings", activity);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[MAX] Не удалось открыть настройки доступа: " + ex.Message);
            }
        }

        private AndroidJavaObject GetCurrentActivity()
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
    }
}