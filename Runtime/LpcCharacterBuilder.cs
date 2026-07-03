using System.Collections.Generic;
using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// Constructs an <see cref="LpcCharacter"/> on a target GameObject from an
    /// <see cref="LpcRecipe"/>: one child SpriteRenderer per slot, ordered by zOrder. Each
    /// slot is resolved to the recipe body type's matching variant (see
    /// <see cref="ResolveLayers"/>), then recolored onto the recipe's ramp for that slot if
    /// one is set (<see cref="LpcRecipe.colors"/>). Works in the editor and at runtime.
    /// Rebuilding clears any previously built layer children.
    /// </summary>
    public static class LpcCharacterBuilder
    {
        /// <summary>
        /// Collapse a recipe's layer pool to one chosen layer per slot for the recipe's body
        /// type: among each slot's variants, pick the best body-type match (with fallback),
        /// skipping slots no variant supports. Ordered by zOrder (then slot for determinism).
        /// Exposed so the creator UI can preview the resolved set for a chosen body type.
        /// </summary>
        public static List<LpcLayerSet> ResolveLayers(LpcRecipe recipe)
        {
            var chosen = new List<LpcLayerSet>();
            if (recipe == null || recipe.layers == null) return chosen;
            string body = LpcBodyType.Normalize(recipe.bodyType);

            // group variants by slot, preserving recipe order within each slot
            var bySlot = new Dictionary<string, List<LpcLayerSet>>();
            var slotOrder = new List<string>();
            foreach (var l in recipe.layers)
            {
                if (l == null) continue;
                if (!bySlot.TryGetValue(l.slot, out var list))
                {
                    list = new List<LpcLayerSet>(); bySlot[l.slot] = list; slotOrder.Add(l.slot);
                }
                list.Add(l);
            }

            foreach (var slot in slotOrder)
            {
                var variants = bySlot[slot];
                var avail = new List<string>(variants.Count);
                foreach (var v in variants) avail.Add(LpcBodyType.Normalize(v.bodyType));
                string pick = LpcBodyType.Resolve(body, avail);
                if (pick == null) continue; // no variant for this body type -> drop the slot
                foreach (var v in variants)
                    if (LpcBodyType.Normalize(v.bodyType) == pick) { chosen.Add(v); break; }
            }

            chosen.Sort((a, b) =>
            {
                int z = a.zOrder.CompareTo(b.zOrder);
                return z != 0 ? z : string.CompareOrdinal(a.slot, b.slot);
            });
            return chosen;
        }

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

            // resolve the recipe's body-type variant per slot, ordered by zOrder
            var layers = ResolveLayers(recipe);

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

                // apply the recipe's palette for this slot (across ALL clips + the legacy
                // walk sheet) so a recipe-built character keeps its colors on every animation
                var clips = set.clips;
                var frames = set.frames;
                var ramp = recipe.RampFor(set.slot);
                if (ramp != null)
                {
                    clips = LpcRecolor.RecolorClips(clips, ramp);
                    frames = LpcRecolor.RecolorFrames(frames, ramp);
                }

                built[i] = new LpcCharacter.Layer { name = set.slot, zOrder = set.zOrder, renderer = sr, clips = clips, frames = frames };
            }

            lpc.layers = built;
            lpc.baseSortingOrder = baseSortingOrder;
            lpc.Play(LpcClips.Walk);
            lpc.SetPose(2, 0); // face down, standing
            return lpc;
        }
    }
}
