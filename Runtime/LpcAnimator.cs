using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// Drives an <see cref="LpcCharacter"/> from a motion source: facing picks the
    /// direction row, and while moving the walk cycle (frames 1..8) advances; idle shows
    /// the standing frame (0). The motion source is any <see cref="ILpcMotion"/> on this
    /// object or a parent (e.g. a player/NPC controller); if none is found, set
    /// <see cref="facing"/> / <see cref="walking"/> directly. Animation is decoupled from
    /// movement timing so the walk reads smoothly even with grid-step movement.
    /// </summary>
    [RequireComponent(typeof(LpcCharacter))]
    public class LpcAnimator : MonoBehaviour
    {
        public float fps = 8f;

        [Tooltip("Fallback used when no ILpcMotion driver is found on this object or a parent.")]
        public Vector2Int facing = new Vector2Int(0, -1);
        public bool walking;

        LpcCharacter character;
        ILpcMotion motion;
        float t;

        void Awake()
        {
            character = GetComponent<LpcCharacter>();
            motion = GetComponentInParent<ILpcMotion>();
        }

        void Update()
        {
            if (character == null) character = GetComponent<LpcCharacter>();
            if (character == null) return; // built at runtime by the spawner/builder
            if (motion == null) motion = GetComponentInParent<ILpcMotion>();

            Vector2Int f = motion != null ? motion.Facing : facing;
            bool moving = motion != null ? motion.Walking : walking;

            int dir = DirRow(f);
            int frame;
            if (moving)
            {
                t += Time.deltaTime * fps;
                frame = 1 + (Mathf.FloorToInt(t) % 8); // cycle 1..8
            }
            else { t = 0f; frame = 0; }                // standing pose
            character.SetPose(dir, frame);
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
