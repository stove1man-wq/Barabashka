using System;
using Barabashka.Contracts;
using Barabashka.Core;
using Barabashka.TaxiProviders;
using NUnit.Framework;

namespace Barabashka.Tests
{
    /// <summary>Разбор фраз про такси и ссылка маршрута Яндекс Go: без телефона и сети.</summary>
    public class TaxiTests
    {
        private static string Handle(string phrase)
        {
            string command;
            return TaxiParser.TryHandle(phrase, out command) ? Uri.UnescapeDataString(command) : null;
        }

        [TestCase("вызови такси до вокзала", "TOOL|taxi_call|to=вокзала")]
        [TestCase("закажи такси домой", "TOOL|taxi_call|to=home")]
        [TestCase("такси на работу", "TOOL|taxi_call|to=work")]
        [TestCase("такси до дома", "TOOL|taxi_call|to=home")]
        [TestCase("мне нужно такси до аэропорта", "TOOL|taxi_call|to=аэропорта")]
        [TestCase("вызови такси", "TOOL|taxi_call|")]
        [TestCase("вызови такси пожалуйста", "TOOL|taxi_call|")]
        [TestCase("закажи такси эконом", "TOOL|taxi_call|")]
        [TestCase("открой такси", "TOOL|taxi_call|")]
        [TestCase("вызови такси в 8 утра", "TOOL|taxi_call|")]
        [TestCase("такси по адресу ленина 5", "TOOL|taxi_call|to=ленина 5")]
        [TestCase("такси в аэропорт шереметьево", "TOOL|taxi_call|to=аэропорт шереметьево")]
        [TestCase("вызови яндекс го до тверской 1", "TOOL|taxi_call|to=тверской 1&provider=yandex-go")]
        [TestCase("вызови яндекс такси до вокзала", "TOOL|taxi_call|to=вокзала&provider=yandex-go")]
        [TestCase("вызови ситимобил", "TOOL|taxi_call|provider=citymobil")]
        [TestCase("закажи максим до ленина 5", "TOOL|taxi_call|to=ленина 5&provider=maxim")]
        public void Call_IsRecognized(string phrase, string expected)
        {
            Assert.That(Handle(phrase), Is.EqualTo(expected), phrase);
        }

        // Открытие приложения, задачи, напоминания, звонки и сообщения остаются у своих разборщиков.
        [TestCase("открой яндекс го")]
        [TestCase("открой яндекс такси")]
        [TestCase("запусти яндекс го")]
        [TestCase("добавь задачу вызвать такси")]
        [TestCase("напомни вызвать такси в восемь")]
        [TestCase("позвони маме")]
        [TestCase("позвони в такси")]
        [TestCase("напиши маме что я еду на такси")]
        [TestCase("отправь маше смс что вызвал такси")]
        [TestCase("вызови максима")]
        [TestCase("который час")]
        [TestCase("яндекс музыка")]
        public void NotTaxi_IsLeftToOthers(string phrase)
        {
            Assert.That(Handle(phrase), Is.Null, phrase);
        }

        [TestCase("мой дом тверская 1", "TOOL|taxi_set_place|slot=home&address=тверская 1")]
        [TestCase("запомни дом ленина", "TOOL|taxi_set_place|slot=home&address=ленина")]
        [TestCase("запомни работу на ленина 5", "TOOL|taxi_set_place|slot=work&address=ленина 5")]
        [TestCase("моя работа на советской 10", "TOOL|taxi_set_place|slot=work&address=советской 10")]
        [TestCase("адрес дома пушкина 3", "TOOL|taxi_set_place|slot=home&address=пушкина 3")]
        public void SetPlace_IsRecognized(string phrase, string expected)
        {
            Assert.That(Handle(phrase), Is.EqualTo(expected), phrase);
        }

        [TestCase("моя работа скучная")]
        [TestCase("мой дом")]
        [TestCase("я работаю на заводе")]
        public void SetPlace_NotAnAddress_IsIgnored(string phrase)
        {
            Assert.That(Handle(phrase), Is.Null, phrase);
        }

        [Test]
        public void YandexGo_RouteUrl_HasCoordinatesAndTracking()
        {
            string url = new YandexGoProvider().BuildRouteUrl(new TaxiPlace { Title = "Тест", Lat = 55.7677, Lon = 37.6071 });
            Assert.That(url, Is.EqualTo("https://3.redirect.appmetrica.yandex.com/route?end-lat=55.767700&end-lon=37.607100&lang=ru&appmetrica_tracking_id=1178268795219780156"));
        }

        [Test]
        public void AppOnlyProviders_HaveNoRoute()
        {
            ITaxiProvider maxim = TaxiProviderRegistry.GetById("maxim");
            Assert.That(maxim, Is.Not.Null);
            Assert.That(maxim.SupportsRoute, Is.False);
            Assert.That(maxim.BuildRouteUrl(new TaxiPlace { Lat = 1, Lon = 2 }), Is.Null);
        }

        [Test]
        public void Registry_FindsByIdIgnoringCase()
        {
            Assert.That(TaxiProviderRegistry.GetById("YANDEX-GO"), Is.Not.Null);
            Assert.That(TaxiProviderRegistry.GetById("unknown"), Is.Null);
            Assert.That(TaxiProviderRegistry.GetById(null), Is.Null);
        }
    }
}
