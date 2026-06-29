using UnityEngine;

namespace Lpc
{
    /// <summary>
    /// A motion source for <see cref="LpcAnimator"/>. Implement this on whatever controls
    /// an actor's movement (player party, NPC mover, AI) and put the animator on the same
    /// GameObject or a child — it auto-wires via GetComponentInParent. Keeps the LPC
    /// character/animation system independent of any particular movement code.
    /// </summary>
    public interface ILpcMotion
    {
        /// <summary>Facing direction, e.g. (0,1)=up, (0,-1)=down, (-1,0)=left, (1,0)=right.</summary>
        Vector2Int Facing { get; }

        /// <summary>True while moving (plays the walk cycle); false shows the standing pose.</summary>
        bool Walking { get; }
    }
}
