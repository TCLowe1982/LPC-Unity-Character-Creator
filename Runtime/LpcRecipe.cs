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
        [Tooltip("Body type to build: male/female/muscular/child/skeleton... Each layer is " +
                 "resolved to its matching body-type variant (with fallback) at build time.")]
        public string bodyType = LpcBodyType.Male;

        [Tooltip("Layer pool. May include several body-type variants of the same slot; the " +
                 "builder picks the one matching bodyType per slot.")]
        public LpcLayerSet[] layers;
    }
}
