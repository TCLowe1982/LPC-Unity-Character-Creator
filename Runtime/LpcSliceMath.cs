using UnityEngine;

namespace Lpc
{
    /// <summary>One sliced cell in texture space: pixel rect (y from the BOTTOM, Unity
    /// convention) plus its logical (direction, frame) and the flat sprite index
    /// <c>dir * cols + frame</c> the runtime clip system expects.</summary>
    public struct LpcCell
    {
        public int x, y, w, h;     // pixel rect; y measured from the texture bottom
        public int dir, frame;     // row = direction, col = frame
        public int index;          // dir * cols + frame
    }

    /// <summary>
    /// Pure sprite-sheet slicing math for the per-animation importer (2g8.8). Each LPC
    /// animation has its OWN grid (walk 9x4, hurt 6x1, shoot 13x4) and some parts ship
    /// OVERSIZE sheets (big weapons drawn in 128/192px cells). Rather than enumerate
    /// variants, the cell size is DERIVED: given the animation's row/col counts and the
    /// sheet's real pixel size, <c>cell = width/cols x height/rows</c> — so oversize sheets
    /// produce a larger cell automatically while staying on the same grid.
    ///
    /// Kept free of UnityEditor/asset types so it unit-tests offline and in EditMode; the
    /// AssetPostprocessor turns these cells into SpriteMetaData. Row 0 maps to the TOP image
    /// row, so a bottom-up y keeps index = dir*cols + frame consistent with LpcClipMath.
    /// </summary>
    public static class LpcSliceMath
    {
        public const int BaseCell = 64; // standard LPC cell; larger => oversize

        /// <summary>
        /// Derive the per-cell pixel size from the sheet dimensions and grid counts. Returns
        /// false when counts are non-positive or the sheet doesn't divide evenly by them
        /// (the caller should then fall back to a fixed grid rather than slice garbage).
        /// </summary>
        public static bool TryCellSize(int sheetW, int sheetH, int cols, int rows, out int cellW, out int cellH)
        {
            cellW = 0; cellH = 0;
            if (cols <= 0 || rows <= 0 || sheetW <= 0 || sheetH <= 0) return false;
            if (sheetW % cols != 0 || sheetH % rows != 0) return false;
            cellW = sheetW / cols; cellH = sheetH / rows;
            return cellW > 0 && cellH > 0;
        }

        /// <summary>
        /// Lay out rows x cols cells over the sheet. y is bottom-up so the top image row
        /// (dir 0 = north) sits at the highest y, giving index = dir*cols + frame.
        /// </summary>
        public static LpcCell[] Slice(int sheetW, int sheetH, int cols, int rows, int cellW, int cellH)
        {
            cols = Mathf.Max(1, cols);
            rows = Mathf.Max(1, rows);
            var cells = new LpcCell[rows * cols];
            int k = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    cells[k++] = new LpcCell
                    {
                        x = c * cellW,
                        y = sheetH - (r + 1) * cellH,
                        w = cellW,
                        h = cellH,
                        dir = r,
                        frame = c,
                        index = r * cols + c,
                    };
            return cells;
        }

        /// <summary>Derive the cell size and slice in one step; false if the sheet doesn't fit the grid.</summary>
        public static bool TrySlice(int sheetW, int sheetH, int cols, int rows, out LpcCell[] cells, out int cellW, out int cellH)
        {
            cells = null;
            if (!TryCellSize(sheetW, sheetH, cols, rows, out cellW, out cellH)) return false;
            cells = Slice(sheetW, sheetH, cols, rows, cellW, cellH);
            return true;
        }

        /// <summary>A cell larger than the standard 64px body cell is an oversize frame.</summary>
        public static bool IsOversize(int cellW, int cellH, int baseCell = BaseCell) => cellW > baseCell || cellH > baseCell;
    }
}
