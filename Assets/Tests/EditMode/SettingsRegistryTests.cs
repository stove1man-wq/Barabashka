using System;
using System.Collections.Generic;
using NUnit.Framework;
using Barabashka.Contracts;
using Barabashka.Core.Settings;

namespace Barabashka.Tests
{
    public class SettingsRegistryTests
    {
        [SetUp]
        public void SetUp() => SettingsRegistry.ResetForTests();

        [Test]
        public void Pages_AreSortedByOrder()
        {
            SettingsRegistry.Register(new GeneralPage());
            SettingsRegistry.Register(new VoicePage());
            SettingsRegistry.Register(new ChatPage());
            SettingsRegistry.Register(new ModelsPage());
            SettingsRegistry.Register(new SecurityPage());

            var pages = SettingsRegistry.Pages;
            Assert.That(pages.Count, Is.EqualTo(5));
            Assert.That(pages[0].Id, Is.EqualTo("voice"));
            Assert.That(pages[1].Id, Is.EqualTo("models"));
            Assert.That(pages[2].Id, Is.EqualTo("security"));
            Assert.That(pages[3].Id, Is.EqualTo("chat"));
            Assert.That(pages[4].Id, Is.EqualTo("general"));
        }

        [Test]
        public void Find_ById_ReturnsPage()
        {
            SettingsRegistry.Register(new VoicePage());
            SettingsRegistry.Register(new ModelsPage());

            Assert.That(SettingsRegistry.Find("voice").Id, Is.EqualTo("voice"));
            Assert.That(SettingsRegistry.Find("models").Id, Is.EqualTo("models"));
        }

        [Test]
        public void Find_ByAlias_ReturnsPage()
        {
            SettingsRegistry.Register(new VoicePage());
            SettingsRegistry.Register(new ModelsPage());

            Assert.That(SettingsRegistry.Find("голос").Id, Is.EqualTo("voice"));
            Assert.That(SettingsRegistry.Find("LLM").Id, Is.EqualTo("models"));
            Assert.That(SettingsRegistry.Find("нейросеть").Id, Is.EqualTo("models"));
        }

        [Test]
        public void Find_Unknown_ReturnsNull()
        {
            SettingsRegistry.Register(new VoicePage());
            Assert.That(SettingsRegistry.Find("unknown"), Is.Null);
            Assert.That(SettingsRegistry.Find(""), Is.Null);
            Assert.That(SettingsRegistry.Find(null), Is.Null);
            Assert.That(SettingsRegistry.Find(null), Is.Null);
        }

[Test]
        public void VoicePage_ToggleSet_ReturnsConfirmation()
        {
            var page = new VoicePage();
            var captured = new List<string>();

            var view = new TestView
            {
                OnToggle = (title, get, set) =>
                {
                    string result = set(!get());
                    captured.Add(result);
                }
            };

            page.Build(view);

            Assert.That(captured.Count, Is.EqualTo(1));
            Assert.That(captured[0], Does.Contain("Озвучка").IgnoreCase);
        }

        [Test]
        public void ModelsPage_ToggleLLM_ReturnsConfirmation()
        {
            var page = new ModelsPage();
            var captured = new List<string>();

            var view = new TestView
            {
                OnToggle = (title, get, set) =>
                {
                    if (title.Contains("умные"))
                    {
                        string result = set(!get());
                        captured.Add(result);
                    }
                }
            };

            page.Build(view);

            Assert.That(captured.Count, Is.EqualTo(1));
            Assert.That(captured[0], Does.Contain("Умные ответы").IgnoreCase);
        }

        [Test]
        public void ModelsPage_OllamaUrl_Invalid_ReturnsError()
        {
            var page = new ModelsPage();
            string result = null;

            var view = new TestView
            {
                OnInput = (caption, placeholder, secret, get, set) =>
                {
                    if (caption.Contains("Ollama"))
                    {
                        result = set("not-a-url");
                    }
                }
            };

            page.Build(view);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("не похож").IgnoreCase);
        }

        [Test]
        public void ModelsPage_OllamaUrl_Valid_ReturnsConfirmation()
        {
            var page = new ModelsPage();
            string result = null;

            var view = new TestView
            {
                OnInput = (caption, placeholder, secret, get, set) =>
                {
                    if (caption.Contains("Ollama"))
                    {
                        result = set("http://192.168.1.5:11434");
                    }
                }
            };

            page.Build(view);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("сохранён").IgnoreCase);
        }

        [Test]
        public void SecurityPage_Choice_ReturnsConfirmation()
        {
            var page = new SecurityPage();
            var captured = new List<string>();

            var view = new TestView
            {
                OnChoice = (title, options, get, set) =>
                {
                    if (title.Contains("Действия"))
                    {
                        int next = (get() + 1) % options.Length;
                        string result = set(next);
                        captured.Add(result);
                    }
                }
            };

            page.Build(view);

            Assert.That(captured.Count, Is.EqualTo(1));
            Assert.That(captured[0], Does.Contain("действи").IgnoreCase);
        }

        [Test]
        public void ChatPage_ChoiceAndToggle_ReturnConfirmations()
        {
            var page = new ChatPage();
            var captured = new List<string>();

            var view = new TestView
            {
                OnChoice = (title, options, get, set) =>
                {
                    if (title.Contains("истори"))
                    {
                        string result = set((get() + 1) % options.Length);
                        captured.Add(result);
                    }
                },
                OnToggle = (title, get, set) =>
                {
                    if (title.Contains("корзин") || title.Contains("смахну"))
                    {
                        string result = set(!get());
                        captured.Add(result);
                    }
                }
            };

            page.Build(view);

            Assert.That(captured.Count, Is.EqualTo(2));
        }

        [Test]
        public void GeneralPage_CityInput_ReturnsConfirmation()
        {
            var page = new GeneralPage();
            string result = null;

            var view = new TestView
            {
                OnInput = (caption, placeholder, secret, get, set) =>
                {
                    if (caption.Contains("Город"))
                    {
                        result = set("Москва");
                    }
                }
            };

            page.Build(view);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("обновлён").IgnoreCase);
        }

        // ----- Test helper -----

        private class TestView : ISettingsView
        {
            public Action<string, Func<bool>, Func<bool, string>> OnToggle;
            public Action<string, string[], Func<int>, Func<int, string>> OnChoice;
            public Action<string, string, bool, Func<string>, Func<string, string>> OnInput;

            public void Section(string title) { }
            public void Hint(string text) { }

            public void Toggle(string title, Func<bool> get, Func<bool, string> set)
            {
                OnToggle?.Invoke(title, get, set);
            }

            public void Choice(string title, string[] options, Func<int> get, Func<int, string> set)
            {
                OnChoice?.Invoke(title, options, get, set);
            }

            public void Input(string caption, string placeholder, bool secret, Func<string> get, Func<string, string> set)
            {
                OnInput?.Invoke(caption, placeholder, secret, get, set);
            }
        }
    }
}