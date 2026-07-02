namespace Lpc
{
    /// <summary>
    /// The ULPC generator's "custom animations" are alternate sheet layouts a part can carry
    /// (declared per layer in sheet_definitions as <c>custom_animation</c>). Most are just an
    /// existing animation drawn in bigger cells — <c>slash_oversize</c> is slash's 6x4 grid in
    /// 192px cells — so they can be imported AS that base clip: same frame order, same grid
    /// counts, only the derived cell size (and pivot, see <see cref="LpcSliceMath"/>) differs.
    ///
    /// The rest (<c>slash_reverse_oversize</c>, <c>tool_whip</c>, <c>tool_rod</c>,
    /// <c>wheelchair</c>) REMIX frames into a new sequence that matches no clip in
    /// <see cref="LpcClips"/>; those return null and the importer skips them (od3 decision:
    /// representing remixed sequences needs a dynamic clip registry, not silent mis-slicing).
    /// </summary>
    public static class LpcCustomAnims
    {
        /// <summary>The base clip a grid-compatible custom animation plays as, else null.</summary>
        public static string BaseClip(string customAnimation)
        {
            switch (customAnimation)
            {
                case "walk_128": return "walk";
                case "slash_128":
                case "slash_oversize": return "slash";
                case "thrust_128":
                case "thrust_oversize": return "thrust";
                case "halfslash_128": return "halfslash";
                case "backslash_128": return "backslash";
                default: return null; // remixed/reversed sequences (or unknown): no base clip
            }
        }

        /// <summary>True when the custom animation is importable as its base clip.</summary>
        public static bool IsGridCompatible(string customAnimation) => BaseClip(customAnimation) != null;
    }
}
