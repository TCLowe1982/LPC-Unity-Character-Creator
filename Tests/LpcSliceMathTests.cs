using NUnit.Framework;
using Lpc;

namespace Lpc.Tests
{
    public class LpcSliceMathTests
    {
        [Test]
        public void TryCellSize_Walk_Is64()
        {
            // walk = 9 cols x 4 rows, standard 64px cell -> 576x256
            Assert.IsTrue(LpcSliceMath.TryCellSize(576, 256, 9, 4, out int cw, out int ch));
            Assert.AreEqual(64, cw);
            Assert.AreEqual(64, ch);
            Assert.IsFalse(LpcSliceMath.IsOversize(cw, ch));
        }

        [Test]
        public void TryCellSize_OversizeSlash_DerivesLargerCell()
        {
            // a 6x4 slash sheet drawn in 192px cells -> 1152x768
            Assert.IsTrue(LpcSliceMath.TryCellSize(1152, 768, 6, 4, out int cw, out int ch));
            Assert.AreEqual(192, cw);
            Assert.AreEqual(192, ch);
            Assert.IsTrue(LpcSliceMath.IsOversize(cw, ch));
        }

        [Test]
        public void TryCellSize_Hurt_SingleRow()
        {
            // hurt = 6 cols x 1 row, 64px -> 384x64
            Assert.IsTrue(LpcSliceMath.TryCellSize(384, 64, 6, 1, out int cw, out int ch));
            Assert.AreEqual(64, cw);
            Assert.AreEqual(64, ch);
        }

        [Test]
        public void TryCellSize_IndivisibleSheet_Fails()
        {
            // 100 is not divisible by 9 -> caller should fall back to a fixed grid
            Assert.IsFalse(LpcSliceMath.TryCellSize(100, 100, 9, 4, out _, out _));
        }

        [Test]
        public void TryCellSize_NonPositiveCounts_Fails()
        {
            Assert.IsFalse(LpcSliceMath.TryCellSize(576, 256, 0, 4, out _, out _));
            Assert.IsFalse(LpcSliceMath.TryCellSize(576, 256, 9, 0, out _, out _));
        }

        [Test]
        public void Slice_Walk_IndexesAndUsesBottomUpY()
        {
            Assert.IsTrue(LpcSliceMath.TrySlice(576, 256, 9, 4, out var cells, out _, out _));
            Assert.AreEqual(36, cells.Length);

            // dir 0 (top image row) sits at the highest y
            var top = cells[0];
            Assert.AreEqual(0, top.index);
            Assert.AreEqual(0, top.x);
            Assert.AreEqual(192, top.y);  // 256 - 1*64

            // dir 2 (south), frame 0 -> index 18, second-from-bottom row
            var standDown = System.Array.Find(cells, c => c.dir == 2 && c.frame == 0);
            Assert.AreEqual(18, standDown.index);
            Assert.AreEqual(0, standDown.x);
            Assert.AreEqual(64, standDown.y);  // 256 - 3*64

            // last cell: dir 3, frame 8 -> bottom row, rightmost
            var last = cells[cells.Length - 1];
            Assert.AreEqual(35, last.index);
            Assert.AreEqual(512, last.x);      // 8*64
            Assert.AreEqual(0, last.y);        // 256 - 4*64
        }

        [Test]
        public void IsOversize_OnlyWhenAboveBaseCell()
        {
            Assert.IsFalse(LpcSliceMath.IsOversize(64, 64));
            Assert.IsTrue(LpcSliceMath.IsOversize(128, 64));
            Assert.IsTrue(LpcSliceMath.IsOversize(64, 192));
        }
    }
}
