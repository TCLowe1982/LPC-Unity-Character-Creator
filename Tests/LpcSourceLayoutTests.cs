using NUnit.Framework;
using Lpc;

namespace Lpc.Tests
{
    public class LpcSourceLayoutTests
    {
        [Test]
        public void GroupParts_BodyTypeVariants_CollapseToOnePart()
        {
            var parts = LpcSourceLayout.GroupParts(new[]
            {
                "body/bodies/male/walk.png",
                "body/bodies/male/slash.png",
                "body/bodies/female/walk.png",
            });
            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual("body", parts[0].category);
            Assert.AreEqual("body/bodies", parts[0].source);
            CollectionAssert.AreEquivalent(new[] { "male", "female" }, parts[0].bodyTypes);
            CollectionAssert.AreEquivalent(new[] { "walk", "slash" }, parts[0].animations);
        }

        [Test]
        public void GroupParts_NonBodyTypeSegment_StaysInTheSourcePath()
        {
            // "adult" is an age folder, not a body type, so it's part of the source
            var parts = LpcSourceLayout.GroupParts(new[] { "hair/afro/adult/walk.png" });
            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual("hair", parts[0].category);
            Assert.AreEqual("hair/afro/adult", parts[0].source);
            Assert.AreEqual(0, parts[0].bodyTypes.Count); // body-agnostic
            CollectionAssert.AreEquivalent(new[] { "walk" }, parts[0].animations);
        }

        [Test]
        public void GroupParts_DeepClothingPath()
        {
            var parts = LpcSourceLayout.GroupParts(new[]
            {
                "torso/clothes/longsleeve/longsleeve/male/walk.png",
                "torso/clothes/longsleeve/formal/male/walk.png",
            });
            Assert.AreEqual(2, parts.Count); // two distinct parts
            Assert.AreEqual("torso/clothes/longsleeve/longsleeve", parts[0].source);
            Assert.AreEqual("torso/clothes/longsleeve/formal", parts[1].source);
        }

        [Test]
        public void GroupParts_IgnoresNonPngAndEmpty()
        {
            var parts = LpcSourceLayout.GroupParts(new[] { null, "", "body/bodies/male/notes.txt", "x" });
            Assert.AreEqual(0, parts.Count);
        }
    }
}
