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

        public void Schedule(int id, string text, DateTime whenLocal)
        {
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
