using NUnit.Framework;
using Barabashka.Contracts;
using Barabashka.Core.Settings;

namespace Barabashka.Tests
{
    public class SettingsVoiceTests
    {
        private bool _speak;
        private bool _llmEnabled;

        [SetUp]
        public void SetUp()
        {
            _speak = AppSettings.SpeakReplies;
            _llmEnabled = LlmSettings.Enabled;
            SettingsRegistry.ResetForTests();
            SettingsRegistry.Register(new VoicePage());
            SettingsRegistry.Register(new ModelsPage());
            SettingsRegistry.Register(new SecurityPage());
            SettingsRegistry.Register(new ChatPage());
        }

        [TearDown]
        public void TearDown()
        {
            AppSettings.SpeakReplies = _speak;
            LlmSettings.Enabled = _llmEnabled;
            LlmProfiles.Remove("gigachat");
            LlmProfiles.Remove("yandex");
            LlmProfiles.Remove("openrouter");
            SettingsRegistry.ResetForTests();
        }

        [Test]
        public void Normalize_LowercasesAndStripsPunctuation()
        {
            Assert.That(SettingsVoice.Normalize("  Открой НАСТРОЙКИ,  Голос! "), Is.EqualTo("открой настройки голос"));
            Assert.That(SettingsVoice.Normalize("Ёлка"), Is.EqualTo("елка"));
        }

        [Test]
        public void OpenPage_ByAlias()
        {
            var r = SettingsVoice.Resolve("открой настройки голос", false, null);
            Assert.That(r.Handled, Is.True);
            Assert.That(r.PageId, Is.EqualTo("voice"));
        }

        [Test]
        public void OpenPage_ByInflectedName()
        {
            Assert.That(SettingsVoice.Resolve("покажи настройки озвучки", false, null).PageId, Is.EqualTo("voice"));
            Assert.That(SettingsVoice.Resolve("открой настройки модели", false, null).PageId, Is.EqualTo("models"));
        }

        [Test]
        public void OpenPage_UnknownPage_NotHandled()
        {
            Assert.That(SettingsVoice.Resolve("открой настройки вайфай", false, null).Handled, Is.False);
            Assert.That(SettingsVoice.Resolve("открой настройки телефона", false, null).Handled, Is.False);
        }

        [Test]
        public void PageCommands_IgnoredOutsideSettingsMode()
        {
            AppSettings.SpeakReplies = true;
            var r = SettingsVoice.Resolve("выключи озвучку", false, null);
            Assert.That(r.Handled, Is.False);
            Assert.That(AppSettings.SpeakReplies, Is.True);
        }

        [Test]
        public void VoicePage_TurnsSpeechOff_InsideSettingsMode()
        {
            AppSettings.SpeakReplies = true;
            var r = SettingsVoice.Resolve("выключи озвучку", true, "chat");
            Assert.That(r.Handled, Is.True);
            Assert.That(r.PageId, Is.EqualTo("voice"));
            Assert.That(r.Reply, Is.EqualTo("Озвучка выключена"));
            Assert.That(AppSettings.SpeakReplies, Is.False);
        }

        [Test]
        public void ModelsPage_TogglesSmartAnswers()
        {
            LlmSettings.Enabled = true;
            var r = SettingsVoice.Resolve("выключи умные ответы", true, "voice");
            Assert.That(r.Handled, Is.True);
            Assert.That(r.PageId, Is.EqualTo("models"));
            Assert.That(LlmSettings.Enabled, Is.False);

            r = SettingsVoice.Resolve("включи умные ответы", true, "models");
            Assert.That(r.Handled, Is.True);
            Assert.That(LlmSettings.Enabled, Is.True);
        }

        [Test]
        public void SecurityPage_HasNoVoiceCommands()
        {
            Assert.That(SettingsVoice.Resolve("включи звонки", true, "security").Handled, Is.False);
            Assert.That(SettingsVoice.Resolve("разрешить звонки", true, "security").Handled, Is.False);
        }

        [Test]
        public void KeysAreNeverAcceptedByVoice()
        {
            string reply;
            Assert.That(new ModelsPage().TryVoice("ключ gigachat abc", out reply), Is.False);
        }
    }
}
