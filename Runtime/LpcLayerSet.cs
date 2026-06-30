using System.Collections.Generic;
using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// One swappable appearance/equipment layer: a slot, a draw order, and its animation
    /// frames. Body, head, hair, pants, a sword, a hat... are all just LpcLayerSets, and a
    /// character is an ordered collection of these.
    ///
    /// Frames are stored per-animation in <see cref="clips"/> (each clip indexed
    /// <c>dir * framesPerDir + frame</c>). The legacy <see cref="frames"/> array holds just
    /// the walk sheet for assets imported before per-animation slicing (2g8.8); the runtime
    /// treats it as the "walk" clip. Use <see cref="FramesFor"/> to resolve either source.
    /// </summary>
    [CreateAssetMenu(fileName = "LpcLayer", menuName = "LPC/Layer Set")]
    public class LpcLayerSet : ScriptableObject
    {
        [Tooltip("Logical slot, e.g. body / head / hair / torso / legs / feet / weapon / hat.")]
        public string slot = "body";

        [Tooltip("Body type this sheet is drawn for: male/female/muscular/child/skeleton... " +
                 "A part has one LpcLayerSet per body type it supports.")]
        public string bodyType = LpcBodyType.Male;

        [Tooltip("Draw order within a character; higher renders in front (hair > head > body).")]
        public int zOrder = 0;

        [Tooltip("Per-animation frames. Each clip indexes dir*framesPerDir + frame.")]
        public LpcClipFrames[] clips;

        [Tooltip("Legacy walk-only sheet (36 = 9 frames x 4 dirs). Used as the 'walk' clip " +
                 "when clips is empty; superseded by per-animation slicing (2g8.8).")]
        public Sprite[] frames;

        /// <summary>Frames for the named clip, or null if this layer doesn't animate it.</summary>
        public Sprite[] FramesFor(string clip) => LpcClipFrames.Resolve(clips, frames, clip);

        /// <summary>Clip names this part actually has frames for (in registry order).</summary>
        public List<string> SupportedClips()
        {
            var list = new List<string>();
            foreach (var c in LpcClips.All)
                if (FramesFor(c.name) != null) list.Add(c.name);
            return list;
        }

        /// <summary>Clips of the standard set this part is MISSING (won't animate).</summary>
        public List<string> MissingClips()
        {
            var list = new List<string>();
            foreach (var c in LpcClips.All)
                if (FramesFor(c.name) == null) list.Add(c.name);
            return list;
        }
    }
}
