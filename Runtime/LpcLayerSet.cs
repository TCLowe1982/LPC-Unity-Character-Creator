using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// One swappable appearance/equipment layer: a slot, a draw order, and the 36 LPC
    /// walk frames (index = direction*9 + frame). Body, head, hair, pants, a sword, a
    /// hat... are all just LpcLayerSets. A character is an ordered collection of these.
    /// </summary>
    [CreateAssetMenu(fileName = "LpcLayer", menuName = "LPC/Layer Set")]
    public class LpcLayerSet : ScriptableObject
    {
        [Tooltip("Logical slot, e.g. body / head / hair / torso / legs / feet / weapon / hat.")]
        public string slot = "body";

        [Tooltip("Draw order within a character; higher renders in front (hair > head > body).")]
        public int zOrder = 0;

        [Tooltip("36 sprites = 9 walk frames x 4 directions (index = dir*9 + frame).")]
        public Sprite[] frames;
    }
}
