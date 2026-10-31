using System.Collections.Generic;
using NUnit.Framework;

namespace Barabashka.Tests
{
    public class ModelsTests
    {
        [TearDown]
        public void TearDown()
        {
            LlmProfiles.Remove("gigachat");
            LlmProfiles.Remove("yandex");
            LlmProfiles.Remove("openrouter");
        }

        private static int IndexOf(List<LlmProfile> chain, string name)
        {
            for (int i = 0; i < chain.Count; i++)
                if (chain[i].name == name) return i;
            return -1;
        }

        [Test]
        public void Chain_PutsDomesticBeforeForeign()
        {
            LlmProfiles.Preset router, giga;
            Assert.That(LlmProfiles.TryGetPreset("openrouter", out router), Is.True);
            Assert.That(LlmProfiles.TryGetPreset("gigachat", out giga), Is.True);
            LlmProfiles.AddOrReplace(new LlmProfile { name = "openrouter", kind = router.Kind, url = router.Url, model = router.Model, apiKey = "k1" });
            LlmProfiles.AddOrReplace(new LlmProfile { name = "gigachat", kind = giga.Kind, url = giga.Url, model = giga.Model, apiKey = "k2" });

            List<LlmProfile> chain = LlmProfiles.Chain();
            int g = IndexOf(chain, "gigachat");
            int o = IndexOf(chain, "openrouter");
            Assert.That(g, Is.GreaterThanOrEqualTo(0));
            Assert.That(o, Is.GreaterThan(g));
        }

        [Test]
        public void Yandex_WithoutProject_IsSkipped_WithProject_IsUsed()
        {
            LlmProfiles.Preset ya;
            Assert.That(LlmProfiles.TryGetPreset("yandex", out ya), Is.True);
            LlmProfiles.AddOrReplace(new LlmProfile { name = "yandex", kind = ya.Kind, url = ya.Url, model = ya.Model, apiKey = "k" });
            Assert.That(IndexOf(LlmProfiles.Chain(), "yandex"), Is.EqualTo(-1));

            LlmProfiles.AddOrReplace(new LlmProfile { name = "yandex", kind = ya.Kind, url = ya.Url, model = ya.Model, apiKey = "k", project = "b1gtest" });
            Assert.That(IndexOf(LlmProfiles.Chain(), "yandex"), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Project_IsSaved()
        {
            LlmProfiles.Preset ya;
            LlmProfiles.TryGetPreset("yandex", out ya);
            LlmProfiles.AddOrReplace(new LlmProfile { name = "yandex", kind = ya.Kind, url = ya.Url, model = ya.Model, apiKey = "k", project = "b1gsave" });
            foreach (LlmProfile p in LlmProfiles.Extra())
                if (p.name == "yandex") { Assert.That(p.project, Is.EqualTo("b1gsave")); return; }
            Assert.Fail("профиль yandex не найден");
        }

        [Test]
        public void GigaChat_IsRecognised()
        {
            LlmProfiles.Preset giga;
            Assert.That(LlmProfiles.TryGetPreset("gigachat", out giga), Is.True);
            var p = new LlmProfile { name = "gigachat", kind = giga.Kind, url = giga.Url };
            Assert.That(p.IsGigaChat, Is.True);
            Assert.That(LlmProfiles.IsDomestic(p), Is.True);
        }
    }
}
