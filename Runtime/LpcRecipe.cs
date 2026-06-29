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
        public LpcLayerSet[] layers;
    }
}
