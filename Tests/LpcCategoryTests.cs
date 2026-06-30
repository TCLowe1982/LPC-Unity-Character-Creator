using System.Collections.Generic;
using NUnit.Framework;
using Lpc;

namespace Lpc.Tests
{
    public class LpcCategoryTests
    {
        [Test]
        public void All_Has21UniqueKnownCategories()
        {
            Assert.AreEqual(21, LpcCategory.All.Length);
            var seen = new HashSet<string>();
            foreach (var c in LpcCategory.All)
            {
                Assert.IsTrue(seen.Add(c), $"duplicate category '{c}'");
                Assert.IsTrue(LpcCategory.IsKnown(c), $"'{c}' should be known");
            }
        }

        [Test]
        public void DefaultZ_OrdersBackToFront()
        {
            // behind-body -> body -> outerwear -> head -> hair -> hat -> held
            Assert.Less(LpcCategory.DefaultZ("shadow"), LpcCategory.DefaultZ("backpack"));
            Assert.Less(LpcCategory.DefaultZ("backpack"), LpcCategory.DefaultZ("body"));
            Assert.Less(LpcCategory.DefaultZ("body"), LpcCategory.DefaultZ("torso"));
            Assert.Less(LpcCategory.DefaultZ("torso"), LpcCategory.DefaultZ("head"));
            Assert.Less(LpcCategory.DefaultZ("head"), LpcCategory.DefaultZ("hair"));
            Assert.Less(LpcCategory.DefaultZ("hair"), LpcCategory.DefaultZ("hat"));
            Assert.Less(LpcCategory.DefaultZ("hat"), LpcCategory.DefaultZ("weapon"));
        }

        [Test]
        public void IsBehindBody_OnlyForBackLayers()
        {
            foreach (var back in new[] { "shadow", "backpack", "cape", "quiver" })
                Assert.IsTrue(LpcCategory.IsBehindBody(back), $"{back} should be behind body");
            foreach (var front in new[] { "body", "torso", "head", "hair", "hat", "weapon" })
                Assert.IsFalse(LpcCategory.IsBehindBody(front), $"{front} should not be behind body");
        }

        [Test]
        public void UnknownCategory_DefaultsToFront()
        {
            Assert.IsFalse(LpcCategory.IsKnown("alien"));
            Assert.AreEqual(100, LpcCategory.DefaultZ("alien"));
            Assert.IsFalse(LpcCategory.IsBehindBody("alien"));
            Assert.AreEqual(100, LpcCategory.DefaultZ(null));
        }
    }
}
