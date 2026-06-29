using System.Collections.Generic;
using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// Constructs an <see cref="LpcCharacter"/> on a target GameObject from an
    /// <see cref="LpcRecipe"/>: one child SpriteRenderer per layer, ordered by zOrder,
    /// all sharing the recipe's layers. Works in the editor and at runtime. Rebuilding
    /// clears any previously built layer children.
    /// </summary>
    public static class LpcCharacterBuilder
    {
        public static LpcCharacter Build(LpcRecipe recipe, GameObject target, int baseSortingOrder = 100)
        {
            if (recipe == null || target == null) return null;

            // clear previously built layer children
            var stale = new List<GameObject>();
            foreach (Transform c in target.transform)
                if (c.name.StartsWith("LPC_")) stale.Add(c.gameObject);
            foreach (var g in stale)
            {
                if (Application.isPlaying) Object.Destroy(g);
                else Object.DestroyImmediate(g);
            }

            // collect + order layers
            var layers = new List<LpcLayerSet>();
            if (recipe.layers != null)
                foreach (var l in recipe.layers) if (l != null) layers.Add(l);
            layers.Sort((a, b) => a.zOrder.CompareTo(b.zOrder));

            var lpc = target.GetComponent<LpcCharacter>();
            if (lpc == null) lpc = target.AddComponent<LpcCharacter>();

            var built = new LpcCharacter.Layer[layers.Count];
            for (int i = 0; i < layers.Count; i++)
            {
                var set = layers[i];
                var go = new GameObject("LPC_" + set.slot);
                go.transform.SetParent(target.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = baseSortingOrder + i;
                if (set.frames != null && set.frames.Length > 18) sr.sprite = set.frames[18];
                built[i] = new LpcCharacter.Layer { name = set.slot, zOrder = set.zOrder, renderer = sr, frames = set.frames };
            }

            lpc.layers = built;
            lpc.baseSortingOrder = baseSortingOrder;
            lpc.SetPose(2, 0); // face down, standing
            return lpc;
        }
    }
}
