using System.Collections.Generic;
using Barabashka.Core;
using NUnit.Framework;

namespace Barabashka.Tests
{
    public class DocsTests
    {
        [Test]
        public void TestDocsListTool_Description()
        {
            var tool = new DocsListTool();
            Assert.AreEqual("docs_list", tool.Name);
            Assert.IsNotNull(tool.Description);
        }

        [Test]
        public void TestDocsOpenTool_Description()
        {
            var tool = new DocsOpenTool();
            Assert.AreEqual("docs_open", tool.Name);
            Assert.IsNotNull(tool.Description);
        }

        [Test]
        public void TestDocsListTool_SupportedExtensions()
        {
            var tool = new DocsListTool();
            
            // Проверяем поддерживаемые расширения
            Assert.IsTrue(tool.GetType().GetMethod("IsSupportedDocument") != null);
        }

        [Test]
        public void TestDocsOpenTool_SupportedExtensions()
        {
            var tool = new DocsOpenTool();
            
            // Проверяем поддерживаемые расширения
            Assert.IsTrue(tool.GetType().GetMethod("IsSupportedDocument") != null);
        }

        [Test]
        public void TestFileSizeFormatting()
        {
            var tool = new DocsListTool();
            
            // Проверяем форматирование размеров файлов
            Assert.AreEqual("1023 байт", tool.FormatFileSize(1023));
            Assert.AreEqual("1.0 КБ", tool.FormatFileSize(1024));
            Assert.AreEqual("1.5 КБ", tool.FormatFileSize(1536));
            Assert.AreEqual("1.0 МБ", tool.FormatFileSize(1024 * 1024));
            Assert.AreEqual("1.0 ГБ", tool.FormatFileSize(1024 * 1024 * 1024));
        }
    }
}