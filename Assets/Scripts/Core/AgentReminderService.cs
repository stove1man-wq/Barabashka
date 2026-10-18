using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Следит за напоминаниями и таймерами, пока приложение открыто: в нужный момент показывает и озвучивает напоминание.
/// Если приложение было закрыто или свёрнуто и срок давно прошёл, напоминание только показывается в чате (системное уведомление уже было).
/// </summary>
public sealed class AgentReminderService : MonoBehaviour
{
    private const float CheckIntervalSec = 1f;
    private const double LateSeconds = 120.0;

    private AgentCommandRouter _router;
    private float _nextCheck;

    private void Start()
    {
        _router = GetComponent<AgentCommandRouter>();
        AgentReminders.RescheduleAll();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + CheckIntervalSec;

        if (_router == null) _router = GetComponent<AgentCommandRouter>();
        if (_router == null) return;

        DateTime nowUtc = DateTime.UtcNow;
        List<AgentReminders.Record> due = AgentReminders.TakeDue(nowUtc);
        foreach (AgentReminders.Record record in due)
        {
            double late = (nowUtc - AgentReminders.DueLocal(record).ToUniversalTime()).TotalSeconds;
            bool missed = late > LateSeconds;
            _router.Announce(Describe(record, missed), !missed);
        }
    }

    private static string Describe(AgentReminders.Record record, bool missed)
    {
        string text = record.text ?? string.Empty;
        if (record.isTimer)
        {
            bool plain = string.Equals(text, "Таймер", StringComparison.OrdinalIgnoreCase);
            if (missed) return "Таймер сработал: " + (plain ? "время вышло" : text) + ".";
            return plain ? "Таймер сработал!" : "Таймер сработал: " + text.Replace("Таймер: ", string.Empty) + ".";
        }
        return missed ? "Пропущено напоминание: " + text : "Напоминаю: " + text;
    }
}
