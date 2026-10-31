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
    }
}
