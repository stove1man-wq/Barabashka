using System;

namespace Barabashka.Contracts
{
    /// <summary>
    /// Системные уведомления о напоминаниях (когда приложение свёрнуто или закрыто).
    /// Реализация зависит от платформы и подключается сама, если есть нужный пакет; без неё напоминания работают только пока приложение открыто.
    /// </summary>
    public interface IReminderNotifier
    {
        void Schedule(int id, string text, DateTime whenLocal);
        void Cancel(int id);
    }

    public static class ReminderNotifierHost
    {
        /// <summary>Устанавливается платформенной сборкой при старте; может быть null.</summary>
        public static IReminderNotifier Current;
    }
}
