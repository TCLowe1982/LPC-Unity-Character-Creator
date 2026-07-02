using NUnit.Framework;
using Lpc;

namespace Lpc.Tests
{
    public class LpcCustomAnimsTests
    {
        [TestCase("walk_128", "walk")]
        [TestCase("slash_128", "slash")]
        [TestCase("slash_oversize", "slash")]
        [TestCase("thrust_128", "thrust")]
        [TestCase("thrust_oversize", "thrust")]
        [TestCase("halfslash_128", "halfslash")]
        [TestCase("backslash_128", "backslash")]
        public void BaseClip_GridCompatible_MapsToItsBaseAnimation(string custom, string expected)
        {
            Assert.AreEqual(expected, LpcCustomAnims.BaseClip(custom));
            Assert.IsTrue(LpcCustomAnims.IsGridCompatible(custom));
        }

        [TestCase("slash_reverse_oversize")] // reversed frame order
        [TestCase("tool_whip")]              // remixed slash frames
        [TestCase("tool_rod")]               // remixed thrust frames
        [TestCase("wheelchair")]             // sit remap
        [TestCase("not_a_custom_anim")]
        [TestCase(null)]
        public void BaseClip_RemixedOrUnknown_IsNull(string custom)
        {
            Assert.IsNull(LpcCustomAnims.BaseClip(custom));
            Assert.IsFalse(LpcCustomAnims.IsGridCompatible(custom));
        }

        [Test]
        public void BaseClips_AllResolveInTheClipRegistry()
        {
            foreach (var custom in new[] { "walk_128", "slash_128", "slash_oversize", "thrust_128", "thrust_oversize", "halfslash_128", "backslash_128" })
                Assert.IsTrue(LpcClips.TryGet(LpcCustomAnims.BaseClip(custom), out _), custom + " must map to a registered clip");
        }
    }
}
