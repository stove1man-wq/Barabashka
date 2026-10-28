using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Barabashka.Contracts
{
    public enum MediaKind
    {
        Image = 0,
        Video = 1,
        Document = 2
    }

    /// <summary>Вариант качества потока (например, 720p из главного плейлиста HLS).</summary>
    public sealed class MediaVariant
    {
        /// <summary>Подпись для человека: «1080p», «720p».</summary>
        public string Label;
        public string Uri;
        /// <summary>Битрейт в битах в секунду; 0, если неизвестен.</summary>
        public int Bandwidth;
    }

    /// <summary>Что показываем: картинка, видео или документ. Только описание, само содержимое не хранится.</summary>
    public sealed class MediaItem
    {
        /// <summary>Уникален в пределах источника.</summary>
        public string Id;
        public MediaKind Kind;
        /// <summary>file:///..., https://... или путь внутри каталога приложения. Адреса content:// сначала копируются в кэш.</summary>
        public string Uri;
        /// <summary>Имя для показа («Отпуск_2026.mp4»).</summary>
        public string Title;
        /// <summary>image/jpeg, video/mp4 и т.п.; может быть пустым.</summary>
        public string MimeType;
        /// <summary>0, если неизвестен.</summary>
        public long SizeBytes;
        /// <summary>Длительность видео в секундах; 0, если неизвестна.</summary>
        public double DurationSeconds;
        /// <summary>Миниатюра (необязательно); если пусто, миниатюру делает просмотрщик.</summary>
        public string ThumbnailUri;
        /// <summary>Файл субтитров .srt или .vtt (необязательно).</summary>
        public string SubtitleUri;
        /// <summary>Варианты качества, если поток их предлагает (необязательно).</summary>
        public List<MediaVariant> Variants;
        /// <summary>Откуда взято: phone, chat, link, telegram...</summary>
        public string Source;
    }

    /// <summary>Запрос к источнику медиа.</summary>
    public sealed class MediaQuery
    {
        /// <summary>null - любые виды.</summary>
        public MediaKind? Kind;
        /// <summary>Сколько элементов вернуть самое большее; 0 - без ограничения.</summary>
        public int Limit;
        /// <summary>Часть названия; пусто - без фильтра.</summary>
        public string Text;
    }

    /// <summary>Источник медиа: сообщения чата, недавние, позже Telegram. Выбор через системное окно - IMediaPicker.</summary>
    public interface IMediaSource
    {
        /// <summary>phone, chat, link, recent, telegram.</summary>
        string Id { get; }
        string DisplayName { get; }
        /// <summary>false, если источник сейчас недоступен (нет подключения, нет разрешения).</summary>
        bool IsAvailable { get; }
        Task<IReadOnlyList<MediaItem>> ListAsync(MediaQuery query, CancellationToken ct);
    }

    /// <summary>Системное окно выбора файлов или галереи (на Android без лишних разрешений). Показывается человеку, поэтому асинхронно.</summary>
    public interface IMediaPicker
    {
        /// <summary>Пустой список, если человек ничего не выбрал.</summary>
        Task<IReadOnlyList<MediaItem>> PickAsync(MediaKind kind, bool allowMultiple, CancellationToken ct);
    }

    /// <summary>Полноэкранный просмотр. Реализация живёт в сборке Barabashka.Media, остальные блоки видят только этот интерфейс.</summary>
    public interface IMediaViewer
    {
        bool IsOpen { get; }
        /// <summary>Открывает просмотр списка с позиции startIndex.</summary>
        void Show(IReadOnlyList<MediaItem> items, int startIndex);
        void Close();
        /// <summary>false, если дальше некуда.</summary>
        bool Next();
        bool Previous();
        /// <summary>Просмотр закрыт (кнопкой, жестом или командой).</summary>
        event Action Closed;
    }

    /// <summary>Управление воспроизведением видео. Просмотрщик реализует его, если умеет играть видео (проверка: viewer as IMediaPlayback).</summary>
    public interface IMediaPlayback
    {
        /// <summary>Сейчас открыто видео (а не картинка).</summary>
        bool HasVideo { get; }
        bool IsPlaying { get; }
        void Play();
        void Pause();
        /// <summary>Перемотка относительно текущего места; отрицательное число - назад.</summary>
        void SeekBy(double seconds);
        /// <summary>Скорость: 0.5, 1, 1.25, 1.5, 2.</summary>
        void SetSpeed(float speed);
        /// <summary>Включить/выключить полноэкранный режим.</summary>
        void SetFullscreen(bool fullscreen);
        /// <summary>true, если сейчас полноэкранный режим.</summary>
        bool IsFullscreen { get; }
    }

    /// <summary>
    /// Точка, через которую блоки находят просмотрщик и выбор файлов. Заполняет Composition при запуске;
    /// пока там пусто, возможности недоступны, и агент честно говорит об этом.
    /// </summary>
    public static class MediaServices
    {
        public static IMediaViewer Viewer;
        public static IMediaPicker Picker;

        private static MediaItem _pendingAttachment;

        /// <summary>
        /// Инструмент кладёт сюда вложение, а чат прикрепляет его к следующему ответу агента
        /// (так вложение попадает в то самое сообщение, где агент пишет «Выбрано: …»).
        /// </summary>
        public static MediaItem PendingAttachment
        {
            set { _pendingAttachment = value; }
        }

        /// <summary>Забирает вложение, ожидающее ответа агента; после вызова оно снова пусто.</summary>
        public static MediaItem TakePendingAttachment()
        {
            MediaItem item = _pendingAttachment;
            _pendingAttachment = null;
            return item;
        }
    }

    /// <summary>
    /// Вложение в одну строку (для истории чата): «kind=image&amp;uri=...&amp;title=...». Значения закодированы,
    /// неизвестные ключи при чтении пропускаются, чтобы формат можно было расширять.
    /// </summary>
    public static class MediaCodec
    {
        public static string Encode(MediaItem item)
        {
            if (item == null) return string.Empty;
            var sb = new StringBuilder();
            Append(sb, "kind", ((int)item.Kind).ToString(CultureInfo.InvariantCulture));
            Append(sb, "id", item.Id);
            Append(sb, "uri", item.Uri);
            Append(sb, "title", item.Title);
            Append(sb, "mime", item.MimeType);
            if (item.SizeBytes > 0) Append(sb, "size", item.SizeBytes.ToString(CultureInfo.InvariantCulture));
            if (item.DurationSeconds > 0) Append(sb, "dur", item.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            Append(sb, "thumb", item.ThumbnailUri);
            Append(sb, "subs", item.SubtitleUri);
            Append(sb, "src", item.Source);
            return sb.ToString();
        }

        /// <summary>null, если строка пустая или без адреса.</summary>
        public static MediaItem Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var item = new MediaItem();
            foreach (string part in text.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = part.Substring(0, eq);
                string value = Uri.UnescapeDataString(part.Substring(eq + 1));
                switch (key)
                {
                    case "kind":
                        int kind;
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out kind) && Enum.IsDefined(typeof(MediaKind), kind))
                            item.Kind = (MediaKind)kind;
                        break;
                    case "id": item.Id = value; break;
                    case "uri": item.Uri = value; break;
                    case "title": item.Title = value; break;
                    case "mime": item.MimeType = value; break;
                    case "size":
                        long size;
                        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out size)) item.SizeBytes = size;
                        break;
                    case "dur":
                        double dur;
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out dur)) item.DurationSeconds = dur;
                        break;
                    case "thumb": item.ThumbnailUri = value; break;
                    case "subs": item.SubtitleUri = value; break;
                    case "src": item.Source = value; break;
                }
            }
            return string.IsNullOrEmpty(item.Uri) ? null : item;
        }

        /// <summary>Путь к локальному файлу из адреса file:// или обычного пути; null, если это не локальный файл.</summary>
        public static string ToLocalPath(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return null;
            if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                uri.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
                return null;
            if (!uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return uri;
            try
            {
                return new Uri(uri).LocalPath;
            }
            catch (Exception)
            {
                return Uri.UnescapeDataString(uri.Substring(7));
            }
        }

        private static void Append(StringBuilder sb, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (sb.Length > 0) sb.Append('&');
            sb.Append(key).Append('=').Append(Uri.EscapeDataString(value));
        }
    }
}
