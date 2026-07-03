using System.Collections.Generic;
using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// A layered LPC character. Every layer (body, head, hair, torso, legs, feet, gear...)
    /// is a child SpriteRenderer sharing the same animation rig. The active animation is an
    /// <see cref="LpcClip"/>: it decides the layout (frames-per-direction, direction count)
    /// so a pose indexes each layer's frames as <c>dir * framesPerDir + frame</c>. All
    /// layers are advanced to the SAME (clip, direction, frame) so they animate in lockstep.
    /// Appearance and equipment are just which layers exist.
    ///
    /// Switch animations with <see cref="Play"/> (e.g. walk -> slash); the per-layer frames
    /// for the new clip are resolved and cached. Layers can be added/replaced/removed live
    /// (<see cref="SetLayer"/> / <see cref="RemoveLayer"/>) without a rebuild.
    /// </summary>
    public class LpcCharacter : MonoBehaviour
    {
        public const int Directions = 4; // 0=up 1=left 2=down 3=right

        [System.Serializable]
        public class Layer
        {
            public string name;                 // slot id (body/head/hair/weapon/...)
            public int zOrder;                  // draw order; higher = front
            public SpriteRenderer renderer;
            public LpcClipFrames[] clips;        // per-animation frames
            public Sprite[] frames;              // legacy walk-only sheet (fallback)

            [System.NonSerialized] string activeClip;
            [System.NonSerialized] Sprite[] active;
            [System.NonSerialized] bool activeIsFallback;

            /// <summary>True when the cached frames are the walk-clip FALLBACK because this
            /// layer has no frames for the active clip (pose on walk's grid, frame 0).</summary>
            public bool ActiveIsFallback => activeIsFallback;

            /// <summary>Resolve and cache this layer's frames for the given clip, falling
            /// back to the walk frames (standing pose) when the clip is missing.</summary>
            public Sprite[] Activate(string clip)
            {
                if (clip != activeClip)
                {
                    activeClip = clip;
                    active = LpcClipFrames.ResolveWithFallback(clips, frames, clip, out activeIsFallback);
                }
                return active;
            }

            public Sprite[] FramesFor(string clip) => LpcClipFrames.Resolve(clips, frames, clip);

            /// <summary>Drop the cached active frames so the next pose re-resolves.</summary>
            public void Invalidate() { activeClip = null; active = null; }
        }

        public Layer[] layers;
        public int baseSortingOrder = 100;

        LpcClip curClip = LpcClips.Walk;
        int curDir = 2, curFrame = 0;

        /// <summary>The animation currently driving poses.</summary>
        public LpcClip CurrentClip => curClip;

        /// <summary>True if any layer has frames for the named clip.</summary>
        public bool HasClip(string clip)
        {
            if (layers == null) return false;
            foreach (var L in layers)
                if (L != null && L.FramesFor(clip) != null) return true;
            return false;
        }

        /// <summary>Worn slots whose layer has no frames for this clip (they hold a standing
        /// walk frame while it plays — or hide, if walk is missing too).</summary>
        public List<string> SlotsMissingClip(string clip)
        {
            var miss = new List<string>();
            if (layers != null)
                foreach (var L in layers)
                    if (L != null && L.FramesFor(clip) == null) miss.Add(L.name);
            return miss;
        }

        /// <summary>Switch the active animation. Layers re-resolve their frames for the new clip.</summary>
        public void Play(LpcClip clip)
        {
            if (!clip.IsValid) return;
            curClip = clip;
            if (layers != null) foreach (var L in layers) if (L != null) L.Activate(clip.name);
            SetPose(curDir, curFrame);
        }

        public void Play(string clipName) => Play(LpcClips.Get(clipName));

        /// <summary>Set every layer to the same pose within the active clip, and remember it.</summary>
        public void SetPose(int dir, int frame)
        {
            int i = LpcClipMath.PoseIndex(curClip, dir, frame, out curDir, out curFrame);
            if (layers == null) return;
            foreach (var L in layers)
            {
                if (L == null || L.renderer == null) continue;
                var f = L.Activate(curClip.name);
                // A layer with no frames for this clip (e.g. a sword with combat sheets only)
                // holds walk frame 0 of the requested direction — the canonical standing
                // pose — instead of popping out as the character starts/stops. Layers
                // lacking walk too hide (no stale pose from the previous clip).
                int idx = L.ActiveIsFallback ? LpcClipMath.PoseIndex(LpcClips.Walk, dir, 0, out _, out _) : i;
                L.renderer.sprite = (f != null && idx >= 0 && idx < f.Length) ? f[idx] : null;
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
                existing.clips = set.clips;
                existing.frames = set.frames;
                existing.zOrder = set.zOrder;
                existing.Invalidate();
            }
            else
            {
                var go = new GameObject("LPC_" + set.slot);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
                var sr = go.AddComponent<SpriteRenderer>();
                list.Add(new Layer { name = set.slot, zOrder = set.zOrder, renderer = sr, clips = set.clips, frames = set.frames });
            }
            layers = list.ToArray();
            ReSort();
            SetPose(curDir, curFrame);
        }

        /// <summary>
        /// Replace the frames of an existing slot for the active clip (e.g. a recolored copy)
        /// without rebuilding the layer. Pose is preserved. No-op if the slot isn't present.
        /// </summary>
        public bool SetLayerFrames(string slot, Sprite[] frames)
        {
            if (layers == null) return false;
            foreach (var L in layers)
                if (L != null && L.name == slot)
                {
                    // Update the active clip's source: a per-animation entry if present,
                    // otherwise the legacy walk array. Then re-resolve the cache.
                    bool placed = false;
                    if (L.clips != null)
                        foreach (var cf in L.clips)
                            if (cf != null && cf.clip == curClip.name) { cf.frames = frames; placed = true; break; }
                    if (!placed && curClip.name == LpcClips.Walk.name) L.frames = frames;
                    L.Invalidate();
                    SetPose(curDir, curFrame);
                    return true;
                }
            return false;
        }

        /// <summary>
        /// Replace ALL of a slot's per-animation frames at once (e.g. a recolored copy across
        /// every clip, so the colour holds on every animation, not just walk). Pose preserved.
        /// No-op if the slot isn't present.
        /// </summary>
        public bool SetLayerClips(string slot, LpcClipFrames[] clips, Sprite[] legacyWalk = null)
        {
            if (layers == null) return false;
            foreach (var L in layers)
                if (L != null && L.name == slot)
                {
                    L.clips = clips;
                    if (legacyWalk != null) L.frames = legacyWalk;
                    L.Invalidate();
                    SetPose(curDir, curFrame);
                    return true;
                }
            return false;
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
