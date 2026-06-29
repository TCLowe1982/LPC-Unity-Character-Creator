using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// Pure frame arithmetic shared by <see cref="LpcCharacter"/> (posing) and
    /// <see cref="LpcAnimator"/> (clip playback). Kept free of MonoBehaviour/asset types so
    /// it can be unit-tested off the Unity main thread (the only Unity dependency is
    /// <c>Mathf</c>, which the offline test bridge shims). All timing/decimation lives in the
    /// caller; these helpers take an already-floored integer <c>step</c>.
    /// </summary>
    public static class LpcClipMath
    {
        /// <summary>
        /// Clamp a (dir, frame) request to the clip's layout and return the flat sprite index
        /// <c>dir * framesPerDir + frame</c>. Single-direction clips (e.g. hurt) force dir 0.
        /// </summary>
        public static int PoseIndex(LpcClip clip, int dir, int frame, out int clampedDir, out int clampedFrame)
        {
            int dirs = Mathf.Max(1, clip.directions);
            clampedDir = Mathf.Clamp(dir, 0, dirs - 1);
            clampedFrame = Mathf.Clamp(frame, 0, Mathf.Max(0, clip.framesPerDir - 1));
            return clampedDir * clip.framesPerDir + clampedFrame;
        }

        /// <summary>
        /// Walk/run cycle frame: skips the standing contact frame 0, looping over
        /// 1..framesPerDir-1 so the legs never snap to the idle pose mid-stride.
        /// </summary>
        public static int CycleFrame(int framesPerDir, int step)
        {
            int n = Mathf.Max(1, framesPerDir - 1);
            return 1 + Mod(step, n);
        }

        /// <summary>Looping clip (idle/combat/sit): wrap over the full 0..framesPerDir-1.</summary>
        public static int LoopFrame(int framesPerDir, int step) => Mod(step, Mathf.Max(1, framesPerDir));

        /// <summary>A play-once clip is complete once the step index passes its last frame.</summary>
        public static bool OneShotComplete(int framesPerDir, int step) => step >= framesPerDir;

        /// <summary>Non-negative modulo so negative steps (clock glitches) still wrap cleanly.</summary>
        public static int Mod(int a, int m)
        {
            if (m <= 0) return 0;
            int r = a % m;
            return r < 0 ? r + m : r;
        }
    }
}
