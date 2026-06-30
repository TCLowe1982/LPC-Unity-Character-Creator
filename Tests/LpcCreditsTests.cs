using NUnit.Framework;
using Lpc;

namespace Lpc.Tests
{
    public class LpcCreditsTests
    {
        static LpcCreditEntry E(string part, string[] authors, string[] licenses, string[] urls = null, string notes = null)
            => new LpcCreditEntry { part = part, authors = authors, licenses = licenses, urls = urls, notes = notes };

        [Test]
        public void UniqueAuthors_DedupesCaseInsensitive_PreservesFirstSeen()
        {
            var entries = new[]
            {
                E("a", new[] { "Johann", "  bluecarrot16 " }, new[] { "CC-BY-SA 3.0" }),
                E("b", new[] { "JOHANN", "Sharm", "" }, new[] { "GPL 3.0" }),
            };
            var authors = LpcCredits.UniqueAuthors(entries);
            CollectionAssert.AreEqual(new[] { "Johann", "bluecarrot16", "Sharm" }, authors); // trimmed, deduped, ordered
        }

        [Test]
        public void UniqueLicenses_AggregatesAcrossParts()
        {
            var entries = new[]
            {
                E("a", new[] { "x" }, new[] { "CC-BY-SA 3.0", "GPL 3.0" }),
                E("b", new[] { "y" }, new[] { "gpl 3.0", "OGA-BY 3.0" }),
            };
            CollectionAssert.AreEqual(new[] { "CC-BY-SA 3.0", "GPL 3.0", "OGA-BY 3.0" }, LpcCredits.UniqueLicenses(entries));
        }

        [Test]
        public void Collect_HandlesNullEntriesAndArrays()
        {
            var entries = new[] { null, E("a", null, null), E("b", new string[] { null, " " }, new[] { "MIT" }) };
            Assert.AreEqual(0, LpcCredits.UniqueAuthors(entries).Count);
            CollectionAssert.AreEqual(new[] { "MIT" }, LpcCredits.UniqueLicenses(entries));
        }

        [Test]
        public void Format_IncludesAggregatesAndPerPartDetail()
        {
            var entries = new[]
            {
                E("torso/clothes/tunic", new[] { "bluecarrot16" }, new[] { "CC-BY-SA 3.0" },
                  new[] { "https://opengameart.org/x" }, "recolored"),
                E("hair/long", new[] { "Manuel" }, new[] { "OGA-BY 3.0" }),
            };
            string s = LpcCredits.Format(entries);

            StringAssert.Contains("Licenses: CC-BY-SA 3.0, OGA-BY 3.0", s);
            StringAssert.Contains("Authors:  bluecarrot16, Manuel", s);
            StringAssert.Contains("torso/clothes/tunic", s);
            StringAssert.Contains("authors: bluecarrot16", s);
            StringAssert.Contains("url: https://opengameart.org/x", s);
            StringAssert.Contains("notes: recolored", s);
            StringAssert.Contains("Parts (2):", s);
        }

        [Test]
        public void Format_EmptyInput_RecordsNone()
        {
            string s = LpcCredits.Format(new LpcCreditEntry[0]);
            StringAssert.Contains("Licenses: (none recorded)", s);
            StringAssert.Contains("Authors:  (none recorded)", s);
            StringAssert.Contains("Parts (0):", s);
        }
    }
}
