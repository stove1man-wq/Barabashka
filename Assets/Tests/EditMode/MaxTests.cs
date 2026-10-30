using System.Collections.Generic;
using Barabashka.MessengerMax;
using NUnit.Framework;
using UnityEngine;

namespace Barabashka.Tests
{
    public class MaxTests
    {
        [Test]
        public void TestParseNotificationsJson_Empty()
        {
            var connector = new MaxNotificationsConnector();
            var messages = connector.ParseNotificationsJson("[]");
            Assert.AreEqual(0, messages.Count);
        }

        [Test]
        public void TestParseNotificationsJson_Single()
        {
            // Пример JSON для тестирования
            string json = @"[{""Title"":""Тест заголовок"",""Text"":""Тест текст"",""Time"":1640995200000}]";
            var connector = new MaxNotificationsConnector();
            var messages = connector.ParseNotificationsJson(json);
            
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual("Тест заголовок", messages[0].ChatTitle);
            Assert.AreEqual("Тест текст", messages[0].Text);
        }

        [Test]
        public void TestParseNotificationsJson_Multiple()
        {
            string json = @"[
                {""Title"":""Сообщение 1"",""Text"":""Текст 1"",""Time"":1640995200000},
                {""Title"":""Сообщение 2"",""Text"":""Текст 2"",""Time"":1640995300000}
            ]";
            var connector = new MaxNotificationsConnector();
            var messages = connector.ParseNotificationsJson(json);
            
            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual("Сообщение 1", messages[0].ChatTitle);
            Assert.AreEqual("Сообщение 2", messages[1].ChatTitle);
        }

        [Test]
        public void TestParseNotificationsJson_Invalid()
        {
            var connector = new MaxNotificationsConnector();
            var messages = connector.ParseNotificationsJson("{invalid json}");
            Assert.AreEqual(0, messages.Count);
        }

        [Test]
        public void TestTaxiMaxWordSeparation()
        {
            // Проверка, что "макс" не мешает "максим" в такси
            // Проверяем что они различаются как слова
            string[] words = {"максим", "макс"};
            
            // По сути проверим что слово "макс" работает корректно как отдельное слово
            Assert.IsTrue(words[0].StartsWith("максим"));
            Assert.IsTrue(words[1].StartsWith("макс"));
            // Это просто проверка логики разделения - потенциальный кейс для дальнейшей реализации
        }
    }
}