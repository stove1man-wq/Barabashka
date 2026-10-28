using System;
using System.Collections.Generic;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Media
{
    /// <summary>
    /// Общий просмотрщик: открывает картинки в ImageViewer, видео в VideoPlayerView (по виду выбранного элемента)
    /// и передаёт им команды. Именно он регистрируется как MediaServices.Viewer и как управление воспроизведением.
    /// </summary>
    public sealed class MediaViewerHost : MonoBehaviour, IMediaViewer, IMediaPlayback
    {
        private ImageViewer _images;
        private VideoPlayerView _video;

        public event Action Closed;

        public bool IsOpen { get { return (_images != null && _images.IsOpen) || (_video != null && _video.IsOpen); } }

        public bool HasVideo { get { return _video != null && _video.IsOpen; } }

        public bool IsPlaying { get { return HasVideo && _video.IsPlaying; } }

        private void Awake()
        {
            _images = gameObject.AddComponent<ImageViewer>();
            _video = gameObject.AddComponent<VideoPlayerView>();
            _images.Closed += OnChildClosed;
            _video.Closed += OnChildClosed;
        }

        private void OnChildClosed()
        {
            Action handler = Closed;
            if (handler != null) handler();
        }

        public void Show(IReadOnlyList<MediaItem> items, int startIndex)
        {
            if (items == null || startIndex < 0 || startIndex >= items.Count || items[startIndex] == null) return;
            MediaKind kind = items[startIndex].Kind;
            if (kind == MediaKind.Video)
            {
                if (_images.IsOpen) _images.Close();
                _video.Show(items, startIndex);
            }
            else if (kind == MediaKind.Image)
            {
                if (_video.IsOpen) _video.Close();
                _images.Show(items, startIndex);
            }
        }

        public void Close()
        {
            if (_video.IsOpen) _video.Close();
            if (_images.IsOpen) _images.Close();
        }

        public bool Next()
        {
            if (_video.IsOpen) return _video.Next();
            return _images.Next();
        }

        public bool Previous()
        {
            if (_video.IsOpen) return _video.Previous();
            return _images.Previous();
        }

        public void Play() { if (HasVideo) _video.Play(); }

        public void Pause() { if (HasVideo) _video.Pause(); }

        public void SeekBy(double seconds) { if (HasVideo) _video.SeekBy(seconds); }

        public void SetSpeed(float speed) { if (HasVideo) _video.SetSpeed(speed); }

        public bool IsFullscreen { get { return HasVideo && _video.IsFullscreen; } }

        public void SetFullscreen(bool fullscreen) { if (HasVideo) _video.SetFullscreen(fullscreen); }
    }
}
