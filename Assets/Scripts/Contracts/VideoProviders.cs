using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Barabashka.Contracts
{
    /// <summary>
    /// Чем играть найденное видео (три уровня, см. docs/MEDIA.md):
    /// DirectStream - прямой поток (mp4, HLS): играет встроенный плеер, работают субтитры и качество;
    /// EmbedPage - встраиваемая страница службы: показывается во встроенном веб-просмотре внутри окна плеера;
    /// WebPage - обычная страница сайта: открывается во встроенном браузере.
    /// Вытаскивать потоки из сайтов в обход их правил (как делают загрузчики) не закладываем: это хрупко и нарушает условия служб.
    /// </summary>
    public enum VideoResolveKind { DirectStream, EmbedPage, WebPage }

    /// <summary>Найденный ролик (до того, как решили, чем его играть).</summary>
    [Serializable]
    public sealed class VideoSearchResult
    {
        public string Provider;
        public string Id;
        public string Title;
        public string ThumbnailUri;
        /// <summary>0, если неизвестна.</summary>
        public double DurationSeconds;
        /// <summary>Страница ролика на сайте службы.</summary>
        public string PageUrl;
    }

    /// <summary>Результат подготовки ролика к показу.</summary>
    public sealed class VideoResolution
    {
        public VideoResolveKind Kind;
        /// <summary>Для DirectStream - готовый элемент для IMediaViewer (адрес, качество, субтитры). Иначе null.</summary>
        public MediaItem Stream;
        /// <summary>Для EmbedPage и WebPage - адрес страницы. Для DirectStream не используется.</summary>
        public string PageUri;
        /// <summary>Пояснение человеку, если вариант не лучший («играет во встроенной странице, субтитры недоступны»).</summary>
        public string Note;
    }

    /// <summary>
    /// Источник видео: служба, где можно искать и откуда можно получить, чем играть (RuTube, VK Видео и т.п.).
    /// Не путать с <see cref="IMediaSource"/>: тот отдаёт уже имеющиеся у человека файлы (телефон, чат).
    /// Российские источники в приоритете (docs/ARCHITECTURE.md, «Сеть и доступность»).
    /// </summary>
    public interface IVideoProvider
    {
        /// <summary>rutube, vk-video, direct-link ...</summary>
        string Id { get; }
        string DisplayName { get; }
        NetworkZone Zone { get; }
        /// <summary>false, если сейчас недоступен (нет сети, служба не отвечает, не настроен).</summary>
        bool IsAvailable { get; }

        Task<IReadOnlyList<VideoSearchResult>> SearchAsync(string query, int limit, CancellationToken ct);

        Task<VideoResolution> ResolveAsync(VideoSearchResult result, CancellationToken ct);

        /// <summary>Узнаёт ли провайдер эту ссылку («открой ссылку» без поиска).</summary>
        bool CanHandleUrl(string url);

        /// <summary>Подготовить ролик по ссылке; вызывать только если CanHandleUrl вернул true.</summary>
        Task<VideoResolution> ResolveUrlAsync(string url, CancellationToken ct);
    }

    /// <summary>Набор источников видео и выбор активного: настройкой и командой голосом или текстом («ищи видео на рутубе»).</summary>
    public interface IVideoProviderRegistry
    {
        /// <summary>Значение ActiveId, при котором поиск идёт по всем доступным источникам по приоритету.</summary>
        string AutoId { get; }

        IReadOnlyList<IVideoProvider> Providers { get; }

        /// <summary>Идентификатор активного источника или AutoId.</summary>
        string ActiveId { get; }

        /// <summary>Переключить источник по идентификатору или на AutoId; false, если такого нет.</summary>
        bool SetActive(string id);

        event Action ActiveChanged;
    }

    /// <summary>
    /// Встроенный веб-просмотр для EmbedPage и WebPage: открывается в окне плеера, остаётся внутри интерфейса Barabashka.
    /// Реализация лежит в сборке Media (позже), остальные блоки видят только этот интерфейс.
    /// </summary>
    public interface IEmbedViewer
    {
        bool IsOpen { get; }
        void Show(string pageUri, string title);
        void Close();
    }
}
