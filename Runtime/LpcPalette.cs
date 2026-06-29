using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// A set of named colour ramps for a recolourable category (hair, cloth, skin, eyes…),
    /// baked from the LPC palette_definitions so it ships and loads at runtime. Each ramp is
    /// an ordered list of shades (dark → light). Recolouring maps a base layer's shades onto
    /// a chosen ramp, so appearance is style (which layer) × colour (which ramp) — independent
    /// dimensions, stored as N styles + M ramps rather than N×M baked variants.
    /// </summary>
    [CreateAssetMenu(fileName = "LpcPalette", menuName = "LPC/Palette")]
    public class LpcPalette : ScriptableObject
    {
        [System.Serializable]
        public class Ramp
        {
            public string name;
            public Color[] colors;   // dark -> light
        }

        [Tooltip("Category this palette recolours, e.g. hair / cloth / body / eye.")]
        public string category = "hair";

        public Ramp[] ramps;

        public string[] Names()
        {
            if (ramps == null) return new string[0];
            var n = new string[ramps.Length];
            for (int i = 0; i < ramps.Length; i++) n[i] = ramps[i] != null ? ramps[i].name : null;
            return n;
        }

        public Color[] Get(string name)
        {
            if (ramps == null || string.IsNullOrEmpty(name)) return null;
            foreach (var r in ramps)
                if (r != null && r.name == name) return r.colors;
            return null;
        }

        public Color[] GetAt(int index)
        {
            if (ramps == null || ramps.Length == 0) return null;
            index = ((index % ramps.Length) + ramps.Length) % ramps.Length;
            return ramps[index] != null ? ramps[index].colors : null;
        }
    }
}
