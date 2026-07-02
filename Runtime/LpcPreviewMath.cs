using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// Pure math for drawing a (dir, frame) cell of an LPC sheet with GUI/texture primitives
    /// (the Catalog Window's live preview, 2g8.22). Splitting this out keeps the editor
    /// window a thin shell and lets the frame/anchor arithmetic unit-test offline.
    /// </summary>
    public static class LpcPreviewMath
    {
        /// <summary>
        /// Normalized texture coordinates (bottom-up, GL convention — what
        /// <c>GUI.DrawTextureWithTexCoords</c> expects) of one cell in a rows x cols sheet.
        /// Row 0 (dir 0 = north) is the TOP image row, so it has the highest v.
        /// </summary>
        public static Rect FrameUV(int cols, int rows, int dir, int frame)
        {
            cols = cols < 1 ? 1 : cols;
            rows = rows < 1 ? 1 : rows;
            dir = dir < 0 ? 0 : (dir >= rows ? rows - 1 : dir);
            frame = frame < 0 ? 0 : (frame >= cols ? cols - 1 : frame);
            return new Rect((float)frame / cols, (float)(rows - 1 - dir) / rows, 1f / cols, 1f / rows);
        }

        /// <summary>
        /// Screen rect (y-down GUI space) that draws a cell at <paramref name="scale"/>
        /// screen px per source px with its sprite pivot — bottom-center for standard cells,
        /// the embedded body baseline for oversize (<see cref="LpcSliceMath.PivotY"/>) —
        /// anchored at (anchorX, anchorY). Anchoring every layer's pivot at the SAME point is
        /// exactly how the runtime aligns them, so the preview matches in-game composition.
        /// </summary>
        public static Rect DestRect(int cellW, int cellH, float scale, float anchorX, float anchorY, int baseCell = LpcSliceMath.BaseCell)
        {
            float pivotPx = LpcSliceMath.PivotY(cellH, baseCell) * cellH;   // px above the cell bottom
            float w = cellW * scale, h = cellH * scale;
            return new Rect(anchorX - w / 2f, anchorY - (cellH - pivotPx) * scale, w, h);
        }

        /// <summary>Frame index to show at a wall-clock time for a clip layout (loops).</summary>
        public static int FrameAt(double time, float fps, int framesPerDir)
        {
            if (framesPerDir <= 0) return 0;
            if (fps <= 0f || time < 0) return 0;
            return (int)(time * fps) % framesPerDir;
        }
    }
}
