using System.Collections.Generic;
using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// Definition of one LPC animation: its layout (frames-per-direction, directions,
    /// cell size) and playback (fps, loop-or-once). Each of the 17 LPC animations has its
    /// OWN layout — walk is 9x4, hurt is 6x1, shoot is 13x4, oversize variants use a
    /// larger cell — so a character can't assume a single 36-frame walk grid. The runtime
    /// indexes a layer's frames as <c>dir * framesPerDir + frame</c> for the active clip.
    ///
    /// These are the canonical Universal-LPC values. Only walk is loaded today; the
    /// per-animation importer (2g8.8) is the source of truth for the actual sliced sheets
    /// and reconciles oversize/offset layouts from sheet_definitions.
    /// </summary>
    [System.Serializable]
    public struct LpcClip
    {
        public string name;
        [Tooltip("Frames in one direction's row (the row length on the sheet).")]
        public int framesPerDir;
        [Tooltip("Direction rows: 4 = N/W/S/E, 1 = single-direction (e.g. hurt faces south).")]
        public int directions;
        [Tooltip("Playback speed in frames per second.")]
        public float fps;
        [Tooltip("Loop forever (walk/idle/run) vs play once then return (slash/hurt/cast).")]
        public bool loop;
        [Tooltip("Source cell size in px (64 standard; oversize variants are larger).")]
        public int frameSize;

        /// <summary>Total frames across all directions = framesPerDir * directions.</summary>
        public int TotalFrames => framesPerDir * Mathf.Max(1, directions);

        public bool IsValid => !string.IsNullOrEmpty(name) && framesPerDir > 0;
    }

    /// <summary>
    /// Registry of the 17 standard LPC animations, in universal-sheet row order. Look up a
    /// clip by name (<see cref="Get"/> / <see cref="TryGet"/>) or iterate <see cref="All"/>.
    /// Frame counts and directions follow the classic ULPC layout; fps/loop are sensible
    /// defaults a game can override per <see cref="LpcAnimator"/>.
    /// </summary>
    public static class LpcClips
    {
        const int Cell = 64; // standard LPC cell; oversize variants override frameSize

        static LpcClip C(string name, int framesPerDir, int directions, float fps, bool loop, int frameSize = Cell)
            => new LpcClip { name = name, framesPerDir = framesPerDir, directions = directions, fps = fps, loop = loop, frameSize = frameSize };

        // ---- the 17 universal LPC animations (row order on the full sheet) -------------
        public static readonly LpcClip Spellcast    = C("spellcast",     7, 4,  8f, false);
        public static readonly LpcClip Thrust       = C("thrust",        8, 4,  8f, false);
        public static readonly LpcClip Walk         = C("walk",          9, 4,  8f, true);
        public static readonly LpcClip Slash        = C("slash",         6, 4, 12f, false);
        public static readonly LpcClip Shoot        = C("shoot",        13, 4,  8f, false);
        public static readonly LpcClip Hurt         = C("hurt",          6, 1,  8f, false); // south only
        public static readonly LpcClip Watering     = C("watering",      5, 4,  8f, false);
        public static readonly LpcClip Idle         = C("idle",          2, 4,  2f, true);
        public static readonly LpcClip Jump         = C("jump",          5, 4,  8f, false);
        public static readonly LpcClip Run          = C("run",           8, 4, 12f, true);
        public static readonly LpcClip Sit          = C("sit",           3, 4,  4f, true);
        public static readonly LpcClip Emote        = C("emote",         3, 4,  6f, false);
        public static readonly LpcClip Climb        = C("climb",         6, 1,  8f, true);  // single column
        public static readonly LpcClip Combat       = C("combat",        2, 4,  6f, true);  // combat idle
        public static readonly LpcClip OneHandSlash     = C("1h_slash",     6, 4, 12f, false);
        public static readonly LpcClip OneHandBackslash = C("1h_backslash",13, 4, 12f, false);
        public static readonly LpcClip OneHandHalfslash = C("1h_halfslash", 7, 4, 12f, false);

        /// <summary>All 17 standard clips, in sheet order.</summary>
        public static readonly LpcClip[] All =
        {
            Spellcast, Thrust, Walk, Slash, Shoot, Hurt, Watering, Idle, Jump, Run,
            Sit, Emote, Climb, Combat, OneHandSlash, OneHandBackslash, OneHandHalfslash,
        };

        static Dictionary<string, LpcClip> _byName;
        static Dictionary<string, LpcClip> ByName
        {
            get
            {
                if (_byName == null)
                {
                    _byName = new Dictionary<string, LpcClip>(All.Length);
                    foreach (var c in All) _byName[c.name] = c;
                }
                return _byName;
            }
        }

        public static bool TryGet(string name, out LpcClip clip)
        {
            if (!string.IsNullOrEmpty(name)) return ByName.TryGetValue(name, out clip);
            clip = Walk;
            return false;
        }

        /// <summary>Look up a clip by name, falling back to <see cref="Walk"/> if unknown.</summary>
        public static LpcClip Get(string name) => TryGet(name, out var c) ? c : Walk;
    }

    /// <summary>
    /// One animation's sprite frames for a single layer: the clip name plus the frames laid
    /// out as <c>dir * framesPerDir + frame</c>. A layer carries an array of these (one per
    /// animation it supports) so the runtime can switch clips without rebuilding.
    /// </summary>
    [System.Serializable]
    public class LpcClipFrames
    {
        public string clip;
        public Sprite[] frames;

        /// <summary>
        /// Resolve a clip's frames from a per-animation set, falling back to the legacy
        /// single-sheet walk array. Returns null when the layer has no frames for the clip.
        /// </summary>
        public static Sprite[] Resolve(LpcClipFrames[] clips, Sprite[] legacyWalk, string clipName)
        {
            if (clips != null)
                foreach (var cf in clips)
                    if (cf != null && cf.clip == clipName && cf.frames != null && cf.frames.Length > 0)
                        return cf.frames;
            // legacy import (pre-2g8.8) stores only the walk sheet in a flat array
            if (clipName == LpcClips.Walk.name && legacyWalk != null && legacyWalk.Length > 0)
                return legacyWalk;
            return null;
        }
    }
}
