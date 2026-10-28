using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Media
{
    /// <summary>
    /// Выбор файлов. На Android открывает системное окно (галерея, «Файлы»), без лишних разрешений; файлы копируются
    /// в каталог приложения (см. MediaPickerFragment.java). На компьютере берёт самую свежую подходящую картинку
    /// из папки media-inbox в каталоге данных приложения (удобно для проверки без телефона).
    /// </summary>
    public sealed class MediaPicker : MonoBehaviour, IMediaPicker
    {
        public const string ReceiverName = "BarabashkaMediaPickerReceiver";
        private const string PluginClass = "com.barabashka.MediaPickerFragment";
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

        private TaskCompletionSource<IReadOnlyList<MediaItem>> _pending;

        /// <summary>Каталог для проверки на компьютере.</summary>
        public static string InboxFolder
        {
            get { return Path.Combine(Application.persistentDataPath, "media-inbox"); }
        }

        public async Task<IReadOnlyList<MediaItem>> PickAsync(MediaKind kind, bool allowMultiple, CancellationToken ct)
        {
            if (_pending != null && !_pending.Task.IsCompleted)
                return new List<MediaItem>();

#if UNITY_ANDROID && !UNITY_EDITOR
            _pending = new TaskCompletionSource<IReadOnlyList<MediaItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
            string mime = kind == MediaKind.Image ? "image/*" : kind == MediaKind.Video ? "video/*" : "*/*";
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var plugin = new AndroidJavaClass(PluginClass))
                {
                    plugin.CallStatic("pick", activity, mime, allowMultiple, ReceiverName, "OnPicked");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Media] Не удалось открыть окно выбора: " + ex.Message);
                _pending.TrySetResult(new List<MediaItem>());
            }

            Task finished = await Task.WhenAny(_pending.Task, Task.Delay(Timeout, ct));
            if (finished != _pending.Task) _pending.TrySetResult(new List<MediaItem>());
            return await _pending.Task;
#else
            await Task.CompletedTask;
            return PickFromInbox(kind);
#endif
        }

        /// <summary>Вызывается из Java через UnitySendMessage (главный поток Unity).</summary>
        public void OnPicked(string payload)
        {
            var result = new List<MediaItem>();
            if (!string.IsNullOrEmpty(payload) && !payload.StartsWith("ERROR:", StringComparison.Ordinal))
            {
                foreach (string line in payload.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    MediaItem item = ParseRecord(line);
                    if (item != null) result.Add(item);
                }
            }
            else if (!string.IsNullOrEmpty(payload))
            {
                Debug.LogWarning("[Media] Окно выбора вернуло ошибку: " + payload);
            }
            if (_pending != null) _pending.TrySetResult(result);
        }

        // вид \t адрес \t название \t mime \t размер \t миниатюра \t длительность (мс)
        private static MediaItem ParseRecord(string line)
        {
            string[] f = line.Split('\t');
            if (f.Length < 5) return null;
            int kind;
            long size;
            if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out kind) || !Enum.IsDefined(typeof(MediaKind), kind)) return null;
            long.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
            long durationMs = 0;
            if (f.Length > 6) long.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out durationMs);
            return new MediaItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = (MediaKind)kind,
                Uri = f[1],
                Title = f[2],
                MimeType = f[3],
                SizeBytes = size,
                ThumbnailUri = f.Length > 5 ? f[5] : string.Empty,
                DurationSeconds = durationMs / 1000.0,
                Source = "phone"
            };
        }

        private static IReadOnlyList<MediaItem> PickFromInbox(MediaKind kind)
        {
            var result = new List<MediaItem>();
            try
            {
                string folder = InboxFolder;
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                if (kind != MediaKind.Image) return result;

                FileInfo newest = null;
                foreach (string path in Directory.GetFiles(folder))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") continue;
                    var info = new FileInfo(path);
                    if (newest == null || info.LastWriteTimeUtc > newest.LastWriteTimeUtc) newest = info;
                }
                if (newest == null) return result;

                result.Add(new MediaItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Kind = MediaKind.Image,
                    Uri = new Uri(newest.FullName).AbsoluteUri,
                    Title = newest.Name,
                    MimeType = newest.Extension.ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg",
                    SizeBytes = newest.Length,
                    Source = "phone"
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Media] Не удалось прочитать папку media-inbox: " + ex.Message);
            }
            return result;
        }
    }

    /// <summary>Создаёт просмотрщик и выбор файлов; вызывает Composition.</summary>
    public static class MediaFactory
    {
        public static IMediaViewer CreateViewer()
        {
            var go = new GameObject("BarabashkaMediaViewerHost");
            UnityEngine.Object.DontDestroyOnLoad(go);
            return go.AddComponent<MediaViewerHost>();
        }

        public static IMediaPicker CreatePicker()
        {
            // Имя объекта должно совпадать с MediaPicker.ReceiverName: по нему Java находит получателя.
            var go = new GameObject(MediaPicker.ReceiverName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            return go.AddComponent<MediaPicker>();
        }
    }
}
