using System;
using NUnit.Framework;

namespace Barabashka.Tests
{
    /// <summary>Разбор фраз без телефона: калькулятор, погода и поиск, телефонные команды.</summary>
    public class ParserTests
    {
        // ---- калькулятор ----

        [TestCase("сколько будет 12 умножить на 7", "12 умножить на 7 равно 84.")]
        [TestCase("сколько будет 100 разделить на 8", "100 разделить на 8 равно 12,5.")]
        [TestCase("сколько будет 100 делить на 8", "100 разделить на 8 равно 12,5.")]
        [TestCase("раздели 100 на 4", "100 разделить на 4 равно 25.")]
        [TestCase("дели 100 на 4", "100 разделить на 4 равно 25.")]
        public void Calculator_Answers(string phrase, string expected)
        {
            string answer;
            Assert.That(AgentCalculator.TryAnswer(phrase, out answer), Is.True, phrase);
            Assert.That(answer, Is.EqualTo(expected));
        }

        [Test]
        public void Calculator_DivideByZero_IsRefused()
        {
            string answer;
            Assert.That(AgentCalculator.TryAnswer("сколько будет 5 делить на 0", out answer), Is.True);
            Assert.That(answer, Is.EqualTo("На ноль делить нельзя."));
        }

        // ---- погода и поиск в интернете ----

        [Test]
        public void Weather_PlainPhrase_IsWeather()
        {
            string command;
            Assert.That(AgentUtilityParser.TryHandle("погода в казани завтра", out command), Is.True);
            Assert.That(command, Does.StartWith("TOOL|weather|"));
        }

        [Test]
        public void SearchInInternet_AboutWeather_IsNotWeather()
        {
            string command;
            bool handled = AgentUtilityParser.TryHandle("найди в интернете погода завтра", out command);
            Assert.That(handled && command != null && command.StartsWith("TOOL|weather"), Is.False, "это поиск, а не прогноз: " + command);
        }

        [Test]
        public void SearchInInternet_AboutWeather_IsWebSearch()
        {
            string command;
            Assert.That(AgentPhoneParser.TryHandle("найди в интернете погода завтра", out command), Is.True);
            Assert.That(command, Is.EqualTo("TOOL|web_search|query=" + Uri.EscapeDataString("погода завтра")));
        }

        // ---- открытие приложений ----

        [TestCase("открой яндекс", "яндекс")]
        [TestCase("открой яндекс го", "яндекс го")]
        [TestCase("запусти калькулятор", "калькулятор")]
        public void OpenApp_PassesName(string phrase, string name)
        {
            string command;
            Assert.That(AgentPhoneParser.TryHandle(phrase, out command), Is.True, phrase);
            Assert.That(command, Is.EqualTo("TOOL|open_app|name=" + Uri.EscapeDataString(name)));
        }
    }
}
