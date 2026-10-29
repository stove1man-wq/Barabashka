using System.Collections.Generic;
using NUnit.Framework;

namespace Barabashka.Tests
{
    /// <summary>Подбор приложения по названию, сказанному голосом (без телефона, на списке-образце).</summary>
    public class AppMatchTests
    {
        private static PhoneActions.AppEntry App(string label, string package)
        {
            return new PhoneActions.AppEntry { Label = label, Package = package };
        }

        private static List<PhoneActions.AppEntry> Sample()
        {
            return new List<PhoneActions.AppEntry>
            {
                App("Яндекс", "ru.yandex.searchplugin"),
                App("Яндекс Go", "ru.yandex.taxi"),
                App("Telegram", "org.telegram.messenger"),
                App("Калькулятор", "com.android.calculator2"),
                App("Google", "com.google.android.googlequicksearchbox")
            };
        }

        [TestCase("яндекс го")]
        [TestCase("яндекс гоу")]
        [TestCase("яндекс такси")]
        public void YandexGo_IsFoundByName(string spoken)
        {
            PhoneActions.AppEntry app = PhoneActions.MatchApp(spoken, Sample());
            Assert.That(app, Is.Not.Null, spoken);
            Assert.That(app.Package, Is.EqualTo("ru.yandex.taxi"));
        }

        [Test]
        public void YandexGo_IsFoundByPackage_WhenLabelDiffers()
        {
            var apps = new List<PhoneActions.AppEntry> { App("Go", "ru.yandex.taxi"), App("Яндекс", "ru.yandex.searchplugin") };
            PhoneActions.AppEntry app = PhoneActions.MatchApp("яндекс гоу", apps);
            Assert.That(app, Is.Not.Null);
            Assert.That(app.Package, Is.EqualTo("ru.yandex.taxi"));
        }

        [Test]
        public void Telegram_IsFound()
        {
            PhoneActions.AppEntry app = PhoneActions.MatchApp("телеграм", Sample());
            Assert.That(app, Is.Not.Null);
            Assert.That(app.Package, Is.EqualTo("org.telegram.messenger"));
        }

        [Test]
        public void Unknown_ReturnsNull()
        {
            Assert.That(PhoneActions.MatchApp("несуществующее приложение", Sample()), Is.Null);
        }
    }
}
