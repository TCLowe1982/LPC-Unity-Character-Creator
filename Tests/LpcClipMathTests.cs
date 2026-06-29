using NUnit.Framework;
using Lpc;

namespace Lpc.Tests
{
    public class LpcClipMathTests
    {
        [Test]
        public void PoseIndex_Walk_StandingDown_Is18()
        {
            int i = LpcClipMath.PoseIndex(LpcClips.Walk, 2, 0, out int d, out int f);
            Assert.AreEqual(18, i);   // dir 2 (south) * 9 + frame 0
            Assert.AreEqual(2, d);
            Assert.AreEqual(0, f);
        }

        [Test]
        public void PoseIndex_Walk_ClampsOutOfRange()
        {
            int i = LpcClipMath.PoseIndex(LpcClips.Walk, 99, 99, out int d, out int f);
            Assert.AreEqual(3, d);    // 4 dirs -> max index 3
            Assert.AreEqual(8, f);    // 9 frames -> max index 8
            Assert.AreEqual(3 * 9 + 8, i);
        }

        [Test]
        public void PoseIndex_SingleDirectionClip_ForcesDirZero()
        {
            // hurt is 6x1: any requested direction collapses to row 0
            int i = LpcClipMath.PoseIndex(LpcClips.Hurt, 3, 4, out int d, out int f);
            Assert.AreEqual(0, d);
            Assert.AreEqual(4, f);
            Assert.AreEqual(4, i);
        }

        [Test]
        public void CycleFrame_SkipsStandingFrameAndWraps()
        {
            // walk: framesPerDir 9 -> cycle over 1..8 (8 distinct frames)
            Assert.AreEqual(1, LpcClipMath.CycleFrame(9, 0));
            Assert.AreEqual(8, LpcClipMath.CycleFrame(9, 7));
            Assert.AreEqual(1, LpcClipMath.CycleFrame(9, 8));   // wrapped back to start of stride
            Assert.AreEqual(2, LpcClipMath.CycleFrame(9, 9));
        }

        [Test]
        public void LoopFrame_WrapsOverFullRange()
        {
            // idle: framesPerDir 2 -> 0,1,0,1,...
            Assert.AreEqual(0, LpcClipMath.LoopFrame(2, 0));
            Assert.AreEqual(1, LpcClipMath.LoopFrame(2, 1));
            Assert.AreEqual(0, LpcClipMath.LoopFrame(2, 2));
        }

        [Test]
        public void OneShotComplete_TrueAtAndPastLastFrame()
        {
            Assert.IsFalse(LpcClipMath.OneShotComplete(6, 5)); // last valid frame index
            Assert.IsTrue(LpcClipMath.OneShotComplete(6, 6));  // past the end
        }

        [Test]
        public void Mod_HandlesNegativeAndZero()
        {
            Assert.AreEqual(3, LpcClipMath.Mod(-1, 4));
            Assert.AreEqual(0, LpcClipMath.Mod(8, 4));
            Assert.AreEqual(0, LpcClipMath.Mod(5, 0)); // guard: no divide-by-zero
        }
    }
}
