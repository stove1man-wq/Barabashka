using System;

namespace Barabashka.Contracts
{
    /// <summary>Что сейчас делает агент. Интерфейс и (позже) аватар подписываются на изменения.</summary>
    public enum AgentActivity
    {
        Idle = 0,        // ждёт (голос выключен или недоступен)
        Listening = 1,   // слушает микрофон
        Thinking = 2,    // обрабатывает запрос
        Speaking = 3,    // озвучивает ответ
        Error = 4        // голосовой блок не работает
    }

    /// <summary>
    /// Простая шина состояний агента. Публиковать только из главного потока Unity.
    /// Подписчики ничего не знают о Sherpa, TTS или мозге агента.
    /// </summary>
    public static class AgentActivityBus
    {
        public static AgentActivity Current { get; private set; }

        /// <summary>true, когда голосовой ввод запущен: после работы агент возвращается в Listening, а не в Idle.</summary>
        public static bool VoiceActive { get; set; }

        public static event Action<AgentActivity> Changed;

        public static void Publish(AgentActivity activity)
        {
            if (Current == activity) return;
            Current = activity;
            Action<AgentActivity> handler = Changed;
            if (handler != null) handler(activity);
        }

        /// <summary>Состояние покоя: Listening, если слушаем микрофон, иначе Idle.</summary>
        public static void PublishResting()
        {
            Publish(VoiceActive ? AgentActivity.Listening : AgentActivity.Idle);
        }
    }

    /// <summary>
    /// Управление микрофоном из интерфейса. Агент слушает, только пока приложение активно и микрофон не выключен.
    /// </summary>
    public static class AgentVoiceControl
    {
        public static bool Muted { get; private set; }

        /// <summary>true, пока приложение свёрнуто или на паузе.</summary>
        public static bool AppPaused { get; set; }

        public static bool ListeningAllowed { get { return !Muted && !AppPaused; } }

        public static event Action<bool> MutedChanged;

        /// <summary>Просьба прервать озвучку (касание аватара, команда «стоп»).</summary>
        public static event Action StopSpeakingRequested;

        public static void RequestStopSpeaking()
        {
            Action handler = StopSpeakingRequested;
            if (handler != null) handler();
        }

        public static void SetMuted(bool muted)
        {
            if (Muted == muted) return;
            Muted = muted;
            Action<bool> handler = MutedChanged;
            if (handler != null) handler(muted);
        }
    }
}
