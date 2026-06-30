using System.IO;
using NUnit.Framework;
using Lpc;
using Lpc.Editor;

namespace Lpc.Tests.Integration
{
    /// <summary>
    /// Verifies the Editor CREDITS.csv reader against a synthetic clone: it matches rows under
    /// a part's source, is quote-aware, dedupes, and merges manifest overrides. Editor-only
    /// (touches the filesystem + Lpc.Editor), so it lives under Tests/Integration.
    /// </summary>
    public class LpcCreditsReaderTests
    {
        string root;

        [SetUp]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "lpc_credits_test").Replace('\\', '/');
            Directory.CreateDirectory(root);
            File.WriteAllText(root + "/CREDITS.csv",
                "Filename,Notes,Authors,Licenses,URLs\n" +
                "torso/clothes/tunic/male/walk.png,base,\"bluecarrot16, Johannes Sjölund\",CC-BY-SA 3.0,https://example/a\n" +
                "torso/clothes/tunic/female/walk.png,,bluecarrot16,GPL 3.0,https://example/b\n" +
                "hair/long/male/walk.png,,Manuel,OGA-BY 3.0,https://example/c\n");
            LpcCreditsReader.ResetCache();
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            LpcCreditsReader.ResetCache();
        }

        [Test]
        public void ReadFor_AggregatesMatchingRows_QuoteAware_Deduped()
        {
            var e = LpcCreditsReader.ReadFor(root, "torso/clothes/tunic", null, null, null, null);

            CollectionAssert.AreEquivalent(new[] { "bluecarrot16", "Johannes Sjölund" }, e.authors);
            CollectionAssert.AreEquivalent(new[] { "CC-BY-SA 3.0", "GPL 3.0" }, e.licenses);
            CollectionAssert.AreEquivalent(new[] { "https://example/a", "https://example/b" }, e.urls);
            CollectionAssert.DoesNotContain(e.authors, "Manuel"); // hair row not matched
        }

        [Test]
        public void ReadFor_ManifestOverridesAugment()
        {
            var e = LpcCreditsReader.ReadFor(root, "hair/long", new[] { "Extra Author" }, null, null, "hand note");
            CollectionAssert.Contains(e.authors, "Manuel");      // from CSV
            CollectionAssert.Contains(e.authors, "Extra Author"); // from override
            Assert.AreEqual("hand note", e.notes);
        }

        [Test]
        public void ReadFor_UnknownPart_NotesMissingCredits()
        {
            var e = LpcCreditsReader.ReadFor(root, "weapon/sword/nonexistent", null, null, null, null);
            Assert.AreEqual(0, e.authors.Length);
            StringAssert.Contains("credits not found", e.notes);
        }
    }
}
