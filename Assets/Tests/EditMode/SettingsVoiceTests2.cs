using System;
using NUnit.Framework;
using Barabashka.Contracts;
using Barabashka.Core.Settings;
using UnityEngine;

namespace Barabashka.Tests
{
    public class SettingsVoiceTests2
    {
        [SetUp]
        public void SetUp()
        {
            SettingsRegistry.ResetForTests();
            SettingsRegistry.Register(new TaxiPage());
            SettingsRegistry.Register(new ChatPage());
            SettingsRegistry.Register(new GeneralPage());
            SettingsRegistry.Register(new SecurityPage());
        }

        [Test]
        public void TryVoice_TaxiPage_SetOperator()
        {
            var ui = UnityEngine.ScriptableObject.CreateInstance<AgentUIManager>();
            
            // Проверяем команду "оператор такси"
            bool result = SettingsVoice.TryHandle("оператор такси", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryVoice_ChatPage_ClearChat()
        {
            var ui = UnityEngine.ScriptableObject.CreateInstance<AgentUIManager>();
            
            // Проверяем команду "удали чат"
            bool result = SettingsVoice.TryHandle("удали чат", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryVoice_ChatPage_SaveChat()
        {
            var ui = UnityEngine.ScriptableObject.CreateInstance<AgentUIManager>();
            
            // Проверяем команду "хранить чат"
            bool result = SettingsVoice.TryHandle("хранить чат", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryVoice_GeneralPage_ShowCity()
        {
            var ui = UnityEngine.ScriptableObject.CreateInstance<AgentUIManager>();
            
            // Проверяем команду "покажи город"
            bool result = SettingsVoice.TryHandle("покажи город", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryVoice_GeneralPage_ShowTime()
        {
            var ui = UnityEngine.ScriptableObject.CreateInstance<AgentUIManager>();
            
            // Проверяем команду "покажи время"
            bool result = SettingsVoice.TryHandle("покажи время", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryVoice_GeneralPage_ShowDate()
        {
            var ui = UnityEngine.ScriptableObject.CreateInstance<AgentUIManager>();
            
            // Проверяем команду "покажи дату"
            bool result = SettingsVoice.TryHandle("покажи дату", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryVoice_SecurityPage_Actions()
        {
            var ui = UnityEngine.ScriptableObject.CreateInstance<AgentUIManager>();
            
            // Проверяем команду "действия агента"
            bool result = SettingsVoice.TryHandle("действия агента", true, ui);
            Assert.IsTrue(result);
        }

        [Test]
        public void TryVoice_SecurityPage_EnableCalls()
        {
            var ui = UnityEngine.ScriptableObject.CreateInstance<AgentUIManager>();
            
            // Проверяем команду "включи звонки"
            bool result = SettingsVoice.TryHandle("включи звонки", true, ui);
            Assert.IsTrue(result);
        }
    }
}