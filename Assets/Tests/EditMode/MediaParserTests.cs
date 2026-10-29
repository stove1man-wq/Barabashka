using System;
using System.Collections.Generic;
using Barabashka.Contracts;
using NUnit.Framework;

namespace Barabashka.Tests
{
    /// <summary>Команды просмотра и полного экрана: работают только при открытом просмотре.</summary>
    public class MediaParserTests
    {
        private sealed class FakeViewer : IMediaViewer, IMediaPlayback
        {
            public bool IsOpen { get; set; }
            public bool HasVideo { get; set; }
            public bool IsPlaying { get { return false; } }
            public bool IsFullscreen { get; private set; }
            public event Action Closed { add { } remove { } }
            public void Show(IReadOnlyList<MediaItem> items, int startIndex) { IsOpen = true; }
            public void Close() { IsOpen = false; }
            public bool Next() { return false; }
            public bool Previous() { return false; }
            public void Play() { }
            public void Pause() { }
            public void SeekBy(double seconds) { }
            public void SetSpeed(float speed) { }
            public void SetFullscreen(bool fullscreen) { IsFullscreen = fullscreen; }
        }

        private IMediaViewer _saved;

        [SetUp]
        public void SetUp()
        {
            _saved = MediaServices.Viewer;
            MediaServices.Viewer = new FakeViewer { IsOpen = true, HasVideo = true };
        }

        [TearDown]
        public void TearDown()
        {
            MediaServices.Viewer = _saved;
        }

        [TestCase("на весь экран", "fullscreen")]
        [TestCase("полный экран", "fullscreen")]
        [TestCase("выйти из полного экрана", "exit_fullscreen")]
        [TestCase("закрой полный экран", "exit_fullscreen")]
        [TestCase("дальше", "next")]
        [TestCase("закрой", "close")]
        [TestCase("пауза", "pause")]
        [TestCase("продолжи", "play")]
        [TestCase("быстрее", "faster")]
        public void Control_Phrases(string phrase, string action)
        {
            string command;
            Assert.That(MediaParser.TryControl(phrase, out command), Is.True, phrase);
            Assert.That(command, Is.EqualTo("TOOL|media_control|action=" + action));
        }

        [Test]
        public void Control_WithoutOpenViewer_IsIgnored()
        {
            MediaServices.Viewer = new FakeViewer { IsOpen = false };
            string command;
            Assert.That(MediaParser.TryControl("на весь экран", out command), Is.False);
        }

        [Test]
        public void Fullscreen_ForImageViewer_IsIgnored()
        {
            MediaServices.Viewer = new FakeViewer { IsOpen = true, HasVideo = false };
            string command;
            Assert.That(MediaParser.TryControl("на весь экран", out command), Is.False);
        }

        [TestCase("покажи фото", "image")]
        [TestCase("выбери видео", "video")]
        public void Pick_Phrases(string phrase, string kind)
        {
            string command;
            Assert.That(MediaParser.TryPick(phrase, out command), Is.True, phrase);
            Assert.That(command, Is.EqualTo("TOOL|media_pick|kind=" + kind));
        }
    }
}
