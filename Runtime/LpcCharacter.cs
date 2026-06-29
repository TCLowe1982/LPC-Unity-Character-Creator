using System.Collections.Generic;
using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// A layered LPC character. Every layer (body, head, hair, torso, legs, feet, gear...)
    /// is a child SpriteRenderer sharing the same animation rig: the LPC universal walk
    /// sheet, 9 frames x 4 directions (rows = up/left/down/right). All layers are advanced
    /// to the SAME (direction, frame) so they animate in lockstep. Appearance and equipment
    /// are just which layers exist — the animation never changes.
    ///
    /// Layers can be added/replaced/removed live (<see cref="SetLayer"/> / <see cref="RemoveLayer"/>):
    /// equip a sword => add the weapon layer; change hair => replace the hair slot. No rebuild.
    /// </summary>
    public class LpcCharacter : MonoBehaviour
    {
        public const int FramesPerDir = 9;
        public const int Directions = 4; // 0=up 1=left 2=down 3=right

        [System.Serializable]
        public class Layer
        {
            public string name;          // slot id (body/head/hair/weapon/...)
            public int zOrder;           // draw order; higher = front
            public SpriteRenderer renderer;
            public Sprite[] frames;      // 36 = dir*9 + frame
        }

        public Layer[] layers;
        public int baseSortingOrder = 100;

        int curDir = 2, curFrame = 0;

        /// <summary>Set every layer to the same pose and remember it for later layer changes.</summary>
        public void SetPose(int dir, int frame)
        {
            curDir = Mathf.Clamp(dir, 0, Directions - 1);
            curFrame = Mathf.Clamp(frame, 0, FramesPerDir - 1);
            int i = curDir * FramesPerDir + curFrame;
            if (layers == null) return;
            foreach (var L in layers)
            {
                if (L == null || L.renderer == null || L.frames == null) continue;
                if (i >= 0 && i < L.frames.Length) L.renderer.sprite = L.frames[i];
            }
        }

        /// <summary>Add a layer for the set's slot, or replace the existing one (same slot).</summary>
        public void SetLayer(LpcLayerSet set)
        {
            if (set == null) return;
            var list = layers != null ? new List<Layer>(layers) : new List<Layer>();
            var existing = list.Find(l => l != null && l.name == set.slot);
            if (existing != null)
            {
                existing.frames = set.frames;
                existing.zOrder = set.zOrder;
            }
            else
            {
                var go = new GameObject("LPC_" + set.slot);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
                var sr = go.AddComponent<SpriteRenderer>();
                list.Add(new Layer { name = set.slot, zOrder = set.zOrder, renderer = sr, frames = set.frames });
            }
            layers = list.ToArray();
            ReSort();
            SetPose(curDir, curFrame);
        }

        /// <summary>Remove the layer occupying the given slot (unequip), if present.</summary>
        public void RemoveLayer(string slot)
        {
            if (layers == null) return;
            var list = new List<Layer>(layers);
            var found = list.Find(l => l != null && l.name == slot);
            if (found == null) return;
            if (found.renderer != null)
            {
                var go = found.renderer.gameObject;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            list.Remove(found);
            layers = list.ToArray();
            ReSort();
        }

        /// <summary>Re-assign sortingOrder by ascending zOrder so the stack draws correctly.</summary>
        void ReSort()
        {
            if (layers == null) return;
            var sorted = new List<Layer>(layers);
            sorted.Sort((a, b) => a.zOrder.CompareTo(b.zOrder));
            for (int i = 0; i < sorted.Count; i++)
                if (sorted[i] != null && sorted[i].renderer != null)
                    sorted[i].renderer.sortingOrder = baseSortingOrder + i;
        }
    }
}
