using NUnit.Framework;
using Lpc;

namespace Lpc.Tests
{
    public class LpcBodyTypeTests
    {
        [Test]
        public void Normalize_NullOrEmpty_IsMale()
        {
            Assert.AreEqual(LpcBodyType.Male, LpcBodyType.Normalize(null));
            Assert.AreEqual(LpcBodyType.Male, LpcBodyType.Normalize(""));
            Assert.AreEqual("female", LpcBodyType.Normalize("female"));
        }

        [Test]
        public void IsKnown_OnlyForListedTypes()
        {
            Assert.IsTrue(LpcBodyType.IsKnown("male"));
            Assert.IsTrue(LpcBodyType.IsKnown("skeleton"));
            Assert.IsFalse(LpcBodyType.IsKnown("alien"));
            Assert.IsFalse(LpcBodyType.IsKnown(null));
        }

        [Test]
        public void FallbackChain_WalksToNearestBase()
        {
            CollectionAssert.AreEqual(new[] { "muscular", "male" }, LpcBodyType.FallbackChain("muscular"));
            CollectionAssert.AreEqual(new[] { "pregnant", "female" }, LpcBodyType.FallbackChain("pregnant"));
            CollectionAssert.AreEqual(new[] { "teen", "female", "male" }, LpcBodyType.FallbackChain("teen"));
            CollectionAssert.AreEqual(new[] { "female" }, LpcBodyType.FallbackChain("female"));
        }

        [Test]
        public void FallbackChain_NullIsMale_UnknownIsItself()
        {
            CollectionAssert.AreEqual(new[] { "male" }, LpcBodyType.FallbackChain(null));
            CollectionAssert.AreEqual(new[] { "alien" }, LpcBodyType.FallbackChain("alien"));
        }

        [Test]
        public void Resolve_PicksExactWhenAvailable()
        {
            Assert.AreEqual("female", LpcBodyType.Resolve("female", new[] { "male", "female" }));
        }

        [Test]
        public void Resolve_FallsBackAlongChain()
        {
            // muscular not present -> male
            Assert.AreEqual("male", LpcBodyType.Resolve("muscular", new[] { "male", "female" }));
            // pregnant not present -> female
            Assert.AreEqual("female", LpcBodyType.Resolve("pregnant", new[] { "female", "child" }));
        }

        [Test]
        public void Resolve_NullWhenNothingInChainAvailable()
        {
            // child has no fallback, and the part only ships male/female
            Assert.IsNull(LpcBodyType.Resolve("child", new[] { "male", "female" }));
            Assert.IsNull(LpcBodyType.Resolve("male", new string[0]));
            Assert.IsNull(LpcBodyType.Resolve("male", null));
        }

        [Test]
        public void Supports_MatchesResolve()
        {
            Assert.IsTrue(LpcBodyType.Supports("muscular", new[] { "male" }));
            Assert.IsFalse(LpcBodyType.Supports("child", new[] { "male", "female" }));
        }
    }
}
