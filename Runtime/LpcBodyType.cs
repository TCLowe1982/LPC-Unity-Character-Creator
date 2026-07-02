using System.Collections.Generic;

namespace Lpc
{
    /// <summary>
    /// The LPC body types and the rules for matching a part's variant to a requested body.
    /// Every clothing/equipment part is drawn per body type (a leather torso has a separate
    /// male/female/child sheet), so a recipe carries one body type and each layer is resolved
    /// to its matching variant. When a part has no sheet for the exact requested type we fall
    /// back along a per-type chain (e.g. muscular -> male, pregnant -> female) rather than
    /// drop the layer; if nothing in the chain exists the part is unsupported for that body.
    ///
    /// Body types are plain strings (matching the generator's sheet_definitions keys) so the
    /// set stays open; null/empty is treated as <see cref="Male"/>, the LPC base body.
    /// Pure and Unity-independent so it unit-tests offline and in EditMode.
    /// </summary>
    public static class LpcBodyType
    {
        public const string Male = "male";
        public const string Muscular = "muscular";
        public const string Female = "female";
        public const string Pregnant = "pregnant";
        public const string Teen = "teen";
        public const string Child = "child";
        public const string Skeleton = "skeleton";
        public const string Zombie = "zombie";

        /// <summary>A BODY-AGNOSTIC part variant (adult hair, hats, most weapons): fits every
        /// body type, at lower priority than a real matching variant. Not in <see cref="All"/> —
        /// it is a variant tag, not a body a recipe can request (and never an LPC folder name).</summary>
        public const string Any = "any";

        /// <summary>All known body types, base/common first.</summary>
        public static readonly string[] All =
            { Male, Muscular, Female, Pregnant, Teen, Child, Skeleton, Zombie };

        // Search order per requested body type: most specific first, then graceful fallbacks
        // to the nearest base body that parts are most likely to provide a sheet for.
        static readonly Dictionary<string, string[]> Chains = new Dictionary<string, string[]>
        {
            { Male,      new[] { Male } },
            { Muscular,  new[] { Muscular, Male } },
            { Female,    new[] { Female } },
            { Pregnant,  new[] { Pregnant, Female } },
            { Teen,      new[] { Teen, Female, Male } },
            { Child,     new[] { Child } },
            { Skeleton,  new[] { Skeleton, Male } },
            { Zombie,    new[] { Zombie, Male } },
        };

        /// <summary>Treat null/empty as the base <see cref="Male"/> body.</summary>
        public static string Normalize(string bodyType) => string.IsNullOrEmpty(bodyType) ? Male : bodyType;

        public static bool IsKnown(string bodyType) =>
            bodyType != null && System.Array.IndexOf(All, bodyType) >= 0;

        /// <summary>Ordered list of body types to try for a request: the type itself then fallbacks.</summary>
        public static string[] FallbackChain(string requested)
        {
            requested = Normalize(requested);
            return Chains.TryGetValue(requested, out var c) ? c : new[] { requested };
        }

        /// <summary>
        /// Pick the best available body type for <paramref name="requested"/> from those a part
        /// provides: the fallback chain first, then a body-agnostic <see cref="Any"/> variant.
        /// Returns null if the part has none of them.
        /// </summary>
        public static string Resolve(string requested, ICollection<string> available)
        {
            if (available == null || available.Count == 0) return null;
            foreach (var bt in FallbackChain(requested))
                if (available.Contains(bt)) return bt;
            return available.Contains(Any) ? Any : null;
        }

        /// <summary>True if a part offering <paramref name="available"/> can dress the requested body.</summary>
        public static bool Supports(string requested, ICollection<string> available) =>
            Resolve(requested, available) != null;
    }
}
