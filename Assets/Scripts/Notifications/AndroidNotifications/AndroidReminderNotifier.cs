using System;
using Barabashka.Contracts;
using Unity.Notifications.Android;
using UnityEngine;

namespace Barabashka.Notifications
{
    /// <summary>
    /// Системные уведомления Android для напоминаний и таймеров: срабатывают, даже когда приложение свёрнуто.
    /// Сборка подключается только на Android и только если установлен пакет Mobile Notifications
    /// (Window - Package Manager - Unity Registry - Mobile Notifications); без пакета проект собирается как раньше.
    /// </summary>
    internal sealed class AndroidReminderNotifier : IReminderNotifier
    {
        private const string ChannelId = "barabashka_reminders";
        private const string ExactAskedKey = "reminders.exactAsked";

        public AndroidReminderNotifier()
        {
            var channel = new AndroidNotificationChannel
            {
                Id = ChannelId,
                Name = "Напоминания",
                Importance = Importance.High,
                Description = "Напоминания и таймеры Барабашки",
                EnableVibration = true
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
        }

        // Без точного времени Android откладывает уведомление на минуты (экономия батареи), и «через 1 минуту» приходит поздно или
        // только после открытия приложения. Если разрешение на точные будильники не выдано, один раз открываем системную страницу выдачи.
        private static void EnsureExactScheduling()
        {
            try
            {
                if (AndroidNotificationCenter.UsingExactScheduling) return;
                if (PlayerPrefs.GetInt(ExactAskedKey, 0) != 0) return;
                PlayerPrefs.SetInt(ExactAskedKey, 1);
                PlayerPrefs.Save();
                AndroidNotificationCenter.RequestExactScheduling();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Reminders] Точное время уведомлений недоступно: " + ex.Message);
            }
        }

        public void Schedule(int id, string text, DateTime whenLocal)
        {
            EnsureExactScheduling();
            Debug.Log("[Reminders] Уведомление " + id + " на " + whenLocal.ToString("HH:mm:ss") + ", точное время: " + AndroidNotificationCenter.UsingExactScheduling);
            var notification = new AndroidNotification
            {
                Title = "Барабашка",
                Text = text,
                FireTime = whenLocal,
                ShouldAutoCancel = true
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, id);
        }

        public void Cancel(int id)
        {
            AndroidNotificationCenter.CancelScheduledNotification(id);
            AndroidNotificationCenter.CancelDisplayedNotification(id);
        }
    }

    internal static class AndroidReminderNotifierRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            try
            {
                ReminderNotifierHost.Current = new AndroidReminderNotifier();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Reminders] Системные уведомления недоступны: " + ex.Message);
            }
        }
    }
}
