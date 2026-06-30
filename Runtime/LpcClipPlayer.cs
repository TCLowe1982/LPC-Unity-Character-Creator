using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// Plays a single chosen clip on an <see cref="LpcCharacter"/>, looping, for PREVIEW use
    /// (a character-creation mirror, an animation browser). Unlike <see cref="LpcAnimator"/>,
    /// which models gameplay locomotion + one-shots, this just loops whatever clip you select
    /// so you can watch it — including normally play-once clips (slash/hurt) so they keep
    /// repeating in the preview. Pick a facing with <see cref="direction"/>.
    /// </summary>
    [RequireComponent(typeof(LpcCharacter))]
    public class LpcClipPlayer : MonoBehaviour
    {
        [Tooltip("Global multiplier on the clip's fps.")]
        public float speedScale = 1f;

        [Tooltip("Facing row to preview: 0=up 1=left 2=down 3=right.")]
        public int direction = 2;

        LpcCharacter character;
        LpcClip clip;
        bool playing;
        float t;

        /// <summary>The clip currently previewing (empty name if stopped).</summary>
        public LpcClip Current => clip;
        public bool IsPlaying => playing;

        void Awake() { character = GetComponent<LpcCharacter>(); }

        public void Play(string clipName) => Play(LpcClips.Get(clipName));

        /// <summary>Start looping a clip. No-op if the character has no frames for it.</summary>
        public void Play(LpcClip c)
        {
            if (character == null) character = GetComponent<LpcCharacter>();
            if (character == null || !c.IsValid || !character.HasClip(c.name)) return;
            clip = c;
            t = 0f;
            playing = true;
            character.Play(c);
            character.SetPose(direction, 0);
        }

        public void Stop() { playing = false; }

        /// <summary>Change the previewed facing without restarting the clip.</summary>
        public void SetDirection(int dir)
        {
            direction = dir;
            if (playing) character.SetPose(direction, LpcClipMath.LoopFrame(clip.framesPerDir, Mathf.FloorToInt(t)));
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>Advance the preview by dt seconds. Update calls this; tests drive it directly.</summary>
        public void Tick(float dt)
        {
            if (!playing || character == null) return;
            t += dt * Mathf.Max(0.01f, clip.fps) * speedScale;
            character.SetPose(direction, LpcClipMath.LoopFrame(clip.framesPerDir, Mathf.FloorToInt(t)));
        }
    }
}
