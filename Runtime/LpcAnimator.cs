using System.Collections.Generic;
using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// Drives an <see cref="LpcCharacter"/> through its animation clips. Locomotion is
    /// automatic: facing picks the direction row, and the character walks while moving and
    /// idles (or stands) otherwise. One-shot animations — slash, hurt, cast, jump — are
    /// triggered via <see cref="PlayOnce"/> and play through once before returning to
    /// locomotion; queue several to chain them.
    ///
    /// The motion source is any <see cref="ILpcMotion"/> on this object or a parent; if none
    /// is found, set <see cref="facing"/> / <see cref="walking"/> directly. Timing comes from
    /// each clip's own fps (scaled by <see cref="speedScale"/>), so clips with different
    /// lengths read at their intended speed. When only the walk sheet is imported, behaviour
    /// is identical to the original walk-or-stand rig — idle/run/etc. light up automatically
    /// once their frames exist (2g8.8).
    /// </summary>
    [RequireComponent(typeof(LpcCharacter))]
    public class LpcAnimator : MonoBehaviour
    {
        [Tooltip("Global multiplier on every clip's fps.")]
        public float speedScale = 1f;

        [Tooltip("Clip used while moving."), SerializeField] string walkClipName = "walk";
        [Tooltip("Clip used while standing still, if its frames exist."), SerializeField] string idleClipName = "idle";

        [Tooltip("Fallback used when no ILpcMotion driver is found on this object or a parent.")]
        public Vector2Int facing = new Vector2Int(0, -1);
        public bool walking;

        LpcCharacter character;
        ILpcMotion motion;

        LpcClip walkClip, idleClip, active;
        bool oneShot;
        float t; // elapsed time, in frames, within the active clip
        readonly Queue<LpcClip> queue = new Queue<LpcClip>();

        void Awake() => EnsureInit();

        /// <summary>
        /// Resolve dependencies and seed the clip state. Safe to call repeatedly and from a
        /// non-play context (e.g. EditMode tests, where Awake never fires), so the animator is
        /// usable the moment it's ticked rather than only after the play-mode lifecycle.
        /// </summary>
        void EnsureInit()
        {
            if (character == null) character = GetComponent<LpcCharacter>();
            if (motion == null) motion = GetComponentInParent<ILpcMotion>();
            if (!active.IsValid)
            {
                walkClip = LpcClips.Get(walkClipName);
                idleClip = LpcClips.Get(idleClipName);
                active = walkClip;
            }
        }

        /// <summary>Trigger a one-shot animation now (or queue it behind the current one-shot).</summary>
        public void PlayOnce(LpcClip clip, bool queueIt = false)
        {
            EnsureInit();
            if (!clip.IsValid || character == null || !character.HasClip(clip.name)) return;
            if (queueIt && oneShot) { queue.Enqueue(clip); return; }
            queue.Clear();
            SetActive(clip, true);
        }

        public void PlayOnce(string clipName, bool queueIt = false) => PlayOnce(LpcClips.Get(clipName), queueIt);

        /// <summary>Abort any one-shot (and its queue) and return to locomotion.</summary>
        public void Stop() { queue.Clear(); oneShot = false; }

        void Update() => Tick(Time.deltaTime);

        /// <summary>
        /// Advance the animation by <paramref name="dt"/> seconds and apply the resulting pose.
        /// Update calls this with Time.deltaTime; tests call it with fixed steps to drive the
        /// pathway deterministically (no play loop or wall-clock needed).
        /// </summary>
        public void Tick(float dt)
        {
            EnsureInit();
            if (character == null) return; // built at runtime by the spawner/builder

            Vector2Int f = motion != null ? motion.Facing : facing;
            bool moving = motion != null ? motion.Walking : walking;
            int dir = DirRow(f);

            if (oneShot)
            {
                t += dt * active.fps * speedScale;
                int fr = Mathf.FloorToInt(t);
                if (!LpcClipMath.OneShotComplete(active.framesPerDir, fr)) { character.SetPose(dir, fr); return; }
                // one-shot finished: chain the queue, else fall back to locomotion this frame
                if (queue.Count > 0) { SetActive(queue.Dequeue(), true); character.SetPose(dir, 0); return; }
                oneShot = false;
            }

            // ---- locomotion ----
            if (moving && character.HasClip(walkClip.name))
            {
                SetActive(walkClip, false);
                t += dt * active.fps * speedScale;
                character.SetPose(dir, LpcClipMath.CycleFrame(active.framesPerDir, Mathf.FloorToInt(t)));
            }
            else if (!moving && character.HasClip(idleClip.name))
            {
                SetActive(idleClip, false);
                t += dt * active.fps * speedScale;
                character.SetPose(dir, LpcClipMath.LoopFrame(active.framesPerDir, Mathf.FloorToInt(t)));
            }
            else
            {
                SetActive(walkClip, false);     // only the walk sheet: stand on frame 0
                t = 0f;
                character.SetPose(dir, 0);
            }
        }

        void SetActive(LpcClip clip, bool isOneShot)
        {
            if (active.name != clip.name)
            {
                active = clip;
                t = 0f;
                character.Play(clip);
            }
            oneShot = isOneShot;
        }

        // LPC rows: 0=up(north) 1=left(west) 2=down(south) 3=right(east)
        static int DirRow(Vector2Int f)
        {
            if (f.y > 0) return 0;
            if (f.y < 0) return 2;
            if (f.x < 0) return 1;
            if (f.x > 0) return 3;
            return 2;
        }
    }
}
