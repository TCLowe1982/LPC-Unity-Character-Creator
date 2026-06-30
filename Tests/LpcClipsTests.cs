using System.Collections.Generic;
using NUnit.Framework;
using Lpc;

namespace Lpc.Tests
{
    public class LpcClipsTests
    {
        [Test]
        public void All_HasFifteenAnimations()
        {
            Assert.AreEqual(15, LpcClips.All.Length);
        }

        [Test]
        public void Names_MatchUlpcSheetFiles()
        {
            // names must equal the on-disk PNG file names so an imported clip resolves its layout
            Assert.AreEqual("combat_idle", LpcClips.CombatIdle.name);
            Assert.AreEqual("backslash", LpcClips.Backslash.name);
            Assert.AreEqual("halfslash", LpcClips.Halfslash.name);
            Assert.AreEqual(6, LpcClips.Halfslash.framesPerDir);
        }

        [Test]
        public void All_NamesAreUniqueAndValid()
        {
            var seen = new HashSet<string>();
            foreach (var c in LpcClips.All)
            {
                Assert.IsTrue(c.IsValid, $"clip '{c.name}' is not valid");
                Assert.IsTrue(seen.Add(c.name), $"duplicate clip name '{c.name}'");
            }
        }

        [Test]
        public void Get_KnownName_ReturnsThatClip()
        {
            Assert.AreEqual("walk", LpcClips.Get("walk").name);
            Assert.AreEqual("shoot", LpcClips.Get("shoot").name);
        }

        [Test]
        public void Get_UnknownName_FallsBackToWalk()
        {
            Assert.AreEqual(LpcClips.Walk.name, LpcClips.Get("does-not-exist").name);
            Assert.AreEqual(LpcClips.Walk.name, LpcClips.Get(null).name);
        }

        [Test]
        public void TryGet_ReportsHitAndMiss()
        {
            Assert.IsTrue(LpcClips.TryGet("hurt", out var hurt));
            Assert.AreEqual("hurt", hurt.name);
            Assert.IsFalse(LpcClips.TryGet("nope", out _));
        }

        [Test]
        public void CanonicalLayouts_MatchUlpcSheet()
        {
            Assert.AreEqual(9, LpcClips.Walk.framesPerDir);
            Assert.AreEqual(4, LpcClips.Walk.directions);
            Assert.IsTrue(LpcClips.Walk.loop);

            Assert.AreEqual(6, LpcClips.Hurt.framesPerDir);
            Assert.AreEqual(1, LpcClips.Hurt.directions);   // south only
            Assert.IsFalse(LpcClips.Hurt.loop);

            Assert.AreEqual(13, LpcClips.Shoot.framesPerDir);
            Assert.AreEqual(52, LpcClips.Shoot.TotalFrames); // 13 * 4
        }
    }
}
