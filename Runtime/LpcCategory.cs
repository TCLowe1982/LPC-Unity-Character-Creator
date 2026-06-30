using System.Collections.Generic;

namespace Lpc
{
    /// <summary>
    /// The 21 LPC part categories and their default draw order (z). The runtime is otherwise
    /// category-agnostic — a slot is just a string — but a single source of truth for the
    /// canonical set and a sane back-to-front ordering lets the importer cover every category
    /// and lets a creator UI list them. Lower z draws further back; behind-body parts (shadow,
    /// backpack, cape, quiver) are negative so they sit behind the body.
    ///
    /// These are DEFAULTS. A sheet_definition's own zPos/priority overrides them per part
    /// (and multi-layer parts simply contribute several layers at different z); see the
    /// importer. Per-direction z (a cape that flips behind/in-front by facing) is tracked
    /// separately. Pure and Unity-independent so it unit-tests offline and in EditMode.
    /// </summary>
    public static class LpcCategory
    {
        // back -> front. Gaps of a few units leave room for definition overrides between parts.
        static readonly Dictionary<string, int> DefaultZTable = new Dictionary<string, int>
        {
            { "shadow",    -100 }, // ground blob, under everything
            { "backpack",   -20 }, // worn on the back, behind the body
            { "cape",       -15 }, // back of cape hangs behind the body
            { "quiver",     -10 }, // arrows on the back
            { "body",        10 },
            { "legs",        20 },
            { "feet",        30 },
            { "dress",       35 },
            { "torso",       40 },
            { "arms",        45 },
            { "neck",        50 },
            { "shoulders",   55 },
            { "head",        60 },
            { "eyes",        64 },
            { "facial",      66 },
            { "beards",      68 },
            { "hair",        72 },
            { "hat",         80 },
            { "tools",       90 },
            { "weapon",     100 },
            { "shield",     100 },
        };

        /// <summary>All 21 canonical LPC categories, back-to-front by default z.</summary>
        public static readonly string[] All =
        {
            "shadow", "backpack", "cape", "quiver", "body", "legs", "feet", "dress", "torso",
            "arms", "neck", "shoulders", "head", "eyes", "facial", "beards", "hair", "hat",
            "tools", "weapon", "shield",
        };

        public static bool IsKnown(string category) => category != null && DefaultZTable.ContainsKey(category);

        /// <summary>Default draw order for a category; unknown categories sit in front (100).</summary>
        public static int DefaultZ(string category) =>
            category != null && DefaultZTable.TryGetValue(category, out var z) ? z : 100;

        /// <summary>True if the category's default draw order is behind the body.</summary>
        public static bool IsBehindBody(string category) => DefaultZ(category) < DefaultZ("body");
    }
}
