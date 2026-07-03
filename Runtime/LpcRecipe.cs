using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// A character as data: the ordered set of <see cref="LpcLayerSet"/>s that make it
    /// up. Build it with <see cref="LpcCharacterBuilder"/>. Character creation produces
    /// one of these; swapping its layers re-skins / re-equips the character.
    /// </summary>
    [CreateAssetMenu(fileName = "LpcRecipe", menuName = "LPC/Recipe")]
    public class LpcRecipe : ScriptableObject
    {
        /// <summary>Palette recolor for one slot: the target ramp its layer is remapped onto
        /// (shade-for-shade by luminance, via <see cref="LpcRecolor"/>) at build time.</summary>
        [System.Serializable]
        public struct SlotColor
        {
            [Tooltip("Slot the ramp applies to (matches LpcLayerSet.slot), e.g. hair / torso.")]
            public string slot;
            [Tooltip("Target ramp; shades are matched to the source ramp in luminance order.")]
            public Color[] ramp;
        }

        [Tooltip("Body type to build: male/female/muscular/child/skeleton... Each layer is " +
                 "resolved to its matching body-type variant (with fallback) at build time.")]
        public string bodyType = LpcBodyType.Male;

        [Tooltip("Layer pool. May include several body-type variants of the same slot; the " +
                 "builder picks the one matching bodyType per slot.")]
        public LpcLayerSet[] layers;

        [Tooltip("Per-slot palette recolors applied when the recipe is built, across ALL " +
                 "clips. Slots without an entry keep the catalog's default colors.")]
        public SlotColor[] colors;

        /// <summary>The recolor ramp for a slot, or null if the recipe doesn't recolor it.</summary>
        public Color[] RampFor(string slot)
        {
            if (colors != null)
                foreach (var c in colors)
                    if (c.slot == slot && c.ramp != null && c.ramp.Length > 0) return c.ramp;
            return null;
        }
    }
}
