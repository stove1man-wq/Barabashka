using System;
using NUnit.Framework;
using Barabashka.Contracts;
using Barabashka.Core.Settings;
using UnityEngine;

namespace Barabashka.Tests
{
    public class SettingsVoiceTests
    {
        [SetUp]
        public void SetUp()
        {
            SettingsRegistry.ResetForTests();
            SettingsRegistry.Register(new VoicePage());
            SettingsRegistry.Register(new ModelsPage());
        }

        [Test]
        public void TryHandle_OpenSettingsPage_WithAlias()
        {
            var ui = CreateMockUI();
            
            // Проверяем команду "открой настройки голос"
            bool result = SettingsVoice.TryHandle("открой настройки голос", false, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryHandle_OpenSettingsPage_WithTitle()
        {
            var ui = CreateMockUI();
            
            // Проверяем команду "покажи настройки умные"
            bool result = SettingsVoice.TryHandle("покажи настройки умные", false, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryHandle_OpenSettingsPage_WithoutName()
        {
            var ui = CreateMockUI();
            
            // Проверяем команду "открой настройки"
            bool result = SettingsVoice.TryHandle("открой настройки", false, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryHandle_VoicePage_ToggleOn()
        {
            var ui = CreateMockUI();
            
            // Проверяем команду "включи озвучку"
            bool result = SettingsVoice.TryHandle("включи озвучку", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryHandle_VoicePage_ToggleOff()
        {
            var ui = CreateMockUI();
            
            // Проверяем команду "выключи озвучку"
            bool result = SettingsVoice.TryHandle("выключи озвучку", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryHandle_ModelsPage_ToggleOn()
        {
            var ui = CreateMockUI();
            
            // Проверяем команду "включи умные ответы"
            bool result = SettingsVoice.TryHandle("включи умные ответы", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryHandle_ModelsPage_ToggleOff()
        {
            var ui = CreateMockUI();
            
            // Проверяем команду "выключи умные ответы"
            bool result = SettingsVoice.TryHandle("выключи умные ответы", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryHandle_ModelsPage_EnableDeviceModel()
        {
            var ui = CreateMockUI();
            
            // Проверяем команду "включи модель в телефоне"
            bool result = SettingsVoice.TryHandle("включи модель в телефоне", true, ui);
            Assert.IsTrue(result);
        }

        private AgentUIManager CreateMockUI()
        {
            // Создаем mock для тестирования
            var mock = UnityEngine.ScriptableObject.CreateInstance<AgentUIManager>();
            return mock;
        }
    }
}