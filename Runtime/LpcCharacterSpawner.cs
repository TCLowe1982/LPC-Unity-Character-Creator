using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// Builds this object's LPC character from a recipe at startup. Drop it on any actor
    /// (party, NPC), assign a recipe, and the character is constructed data-driven via
    /// <see cref="LpcCharacterBuilder"/>. Swap the recipe to swap the character.
    /// </summary>
    public class LpcCharacterSpawner : MonoBehaviour
    {
        public LpcRecipe recipe;
        public int baseSortingOrder = 100;

        void Awake()
        {
            if (recipe != null) LpcCharacterBuilder.Build(recipe, gameObject, baseSortingOrder);
        }
    }
}
