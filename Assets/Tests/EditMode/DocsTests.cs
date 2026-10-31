using NUnit.Framework;
using Barabashka.Core;

namespace Barabashka.Tests
{
    public class DocsTests
    {
        [Test]
        public void Tools_HaveNamesAndDescriptions()
        {
            var list = new DocsListTool();
            var open = new DocsOpenTool();
            Assert.AreEqual("docs_list", list.Name);
            Assert.AreEqual("docs_open", open.Name);
            Assert.IsNotNull(list.Description);
            Assert.IsNotNull(open.Description);
        }

        [Test]
        public void SupportedExtensions()
        {
            Assert.IsTrue(DocsStore.IsSupported(".pdf"));
            Assert.IsTrue(DocsStore.IsSupported(".DOCX"));
            Assert.IsFalse(DocsStore.IsSupported(".exe"));
            Assert.IsFalse(DocsStore.IsSupported(""));
            Assert.IsFalse(DocsStore.IsSupported(null));
        }

        [Test]
        public void FileSizeFormatting()
        {
            Assert.AreEqual("1023 байт", DocsStore.FormatSize(1023));
            Assert.AreEqual("1.0 КБ", DocsStore.FormatSize(1024));
            Assert.AreEqual("1.5 КБ", DocsStore.FormatSize(1536));
            Assert.AreEqual("1.0 МБ", DocsStore.FormatSize(1024 * 1024));
            Assert.AreEqual("1.0 ГБ", DocsStore.FormatSize(1024L * 1024 * 1024));
        }

        [Test]
        public void MimeTypes()
        {
            Assert.AreEqual("application/pdf", DocsStore.MimeType(".PDF"));
            Assert.AreEqual("application/octet-stream", DocsStore.MimeType(".xyz"));
        }

        private static string Parse(string phrase)
        {
            string command;
            return DocsParser.TryHandle(phrase, out command) ? command : null;
        }

        [Test]
        public void Parser_ListPhrases()
        {
            foreach (string p in new[] { "документы", "мои документы", "покажи документы", "открой документы", "открой мои документы", "список документов" })
                Assert.That(Parse(p), Is.EqualTo("TOOL|docs_list|"), p);
        }

        // «TOOL|имя|ключ=значение» -> имя инструмента и значение аргумента (значения закодированы).
        private static string ToolOf(string command) { return command.Split('|')[1]; }

        private static string ArgOf(string command, string key)
        {
            string[] parts = command.Split(new[] { '|' }, 3);
            var args = AgentToolRegistry.ParseQuery(parts.Length > 2 ? parts[2] : string.Empty);
            string value;
            return args.TryGetValue(key, out value) ? value : null;
        }

        [Test]
        public void Parser_SearchAndOpenByName()
        {
            string find = Parse("найди документ договор");
            Assert.That(ToolOf(find), Is.EqualTo("docs_list"));
            Assert.That(ArgOf(find, "query"), Is.EqualTo("договор"));

            string open = Parse("открой документ договор");
            Assert.That(ToolOf(open), Is.EqualTo("docs_open"));
            Assert.That(ArgOf(open, "name"), Is.EqualTo("договор"));

            string inflected = Parse("открой документ договора");
            Assert.That(ArgOf(inflected, "name"), Is.EqualTo("договор"));
        }

        [Test]
        public void Parser_AddPhrases()
        {
            Assert.That(Parse("выбери документ"), Is.EqualTo("TOOL|docs_add|"));
            Assert.That(Parse("добавь документ"), Is.EqualTo("TOOL|docs_add|"));
        }

        [Test]
        public void Parser_IgnoresOtherPhrases()
        {
            Assert.That(Parse("добавь задачу проверить документы"), Is.Null);
            Assert.That(Parse("открой телеграм"), Is.Null);
            Assert.That(Parse("покажи документальный фильм"), Is.Null);
            Assert.That(Parse("напомни взять документы"), Is.Null);
        }

        [Test]
        public void OpenMax_FindsLatinLabel()
        {
            var apps = new System.Collections.Generic.List<PhoneActions.AppEntry>
            {
                new PhoneActions.AppEntry { Label = "Telegram", Package = "org.telegram.messenger" },
                new PhoneActions.AppEntry { Label = "MAX", Package = "ru.oneme.app" }
            };
            PhoneActions.AppEntry found = PhoneActions.MatchApp("макс", apps);
            Assert.That(found, Is.Not.Null);
            Assert.That(found.Package, Is.EqualTo("ru.oneme.app"));
        }
    }
}
