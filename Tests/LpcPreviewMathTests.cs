using NUnit.Framework;
using Lpc;

namespace Lpc.Tests
{
    public class LpcPreviewMathTests
    {
        [Test]
        public void FrameUV_TopImageRow_HasHighestV()
        {
            // walk 9x4: dir 0 (north) is the top row -> v = 0.75, height 0.25
            var uv = LpcPreviewMath.FrameUV(9, 4, 0, 0);
            Assert.AreEqual(0f, uv.x, 1e-6f);
            Assert.AreEqual(0.75f, uv.y, 1e-6f);
            Assert.AreEqual(1f / 9f, uv.width, 1e-6f);
            Assert.AreEqual(0.25f, uv.height, 1e-6f);
        }

        [Test]
        public void FrameUV_BottomRowLastFrame()
        {
            // dir 3 (east) frame 8 -> bottom row (v=0), last column
            var uv = LpcPreviewMath.FrameUV(9, 4, 3, 8);
            Assert.AreEqual(8f / 9f, uv.x, 1e-6f);
            Assert.AreEqual(0f, uv.y, 1e-6f);
        }

        [Test]
        public void FrameUV_ClampsOutOfRange()
        {
            var uv = LpcPreviewMath.FrameUV(6, 4, 9, 99);   // clamped to dir 3, frame 5
            Assert.AreEqual(5f / 6f, uv.x, 1e-6f);
            Assert.AreEqual(0f, uv.y, 1e-6f);
        }

        [Test]
        public void DestRect_StandardCell_BottomSitsOnAnchor()
        {
            // 64px cell, x2 scale, anchored at (100, 200): bottom edge = anchor y
            var r = LpcPreviewMath.DestRect(64, 64, 2f, 100f, 200f);
            Assert.AreEqual(100f - 64f, r.x, 1e-4f);        // centered
            Assert.AreEqual(200f - 128f, r.y, 1e-4f);
            Assert.AreEqual(128f, r.width, 1e-4f);
            Assert.AreEqual(128f, r.height, 1e-4f);
            Assert.AreEqual(200f, r.y + r.height, 1e-4f);   // feet on the anchor
        }

        [Test]
        public void DestRect_OversizeCell_ExtendsBelowAnchorByThePivotOffset()
        {
            // 192px cell: embedded body baseline is 64px above the cell bottom, so at x2
            // scale the cell bottom lands 128px BELOW the anchor and the same body pixels
            // line up with a 64px layer drawn at the same anchor.
            var r = LpcPreviewMath.DestRect(192, 192, 2f, 100f, 200f);
            Assert.AreEqual(200f + 128f, r.y + r.height, 1e-4f);
            Assert.AreEqual(100f - 192f, r.x, 1e-4f);
        }

        [Test]
        public void FrameAt_LoopsAtClipRate()
        {
            Assert.AreEqual(0, LpcPreviewMath.FrameAt(0.0, 8f, 9));
            Assert.AreEqual(4, LpcPreviewMath.FrameAt(0.5, 8f, 9));     // 8fps * 0.5s = frame 4
            Assert.AreEqual(0, LpcPreviewMath.FrameAt(9.0 / 8.0, 8f, 9)); // wraps after 9 frames
            Assert.AreEqual(0, LpcPreviewMath.FrameAt(5.0, 0f, 9));     // paused/invalid fps
            Assert.AreEqual(0, LpcPreviewMath.FrameAt(5.0, 8f, 0));     // no frames
        }
    }
}
