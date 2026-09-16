using UnityEngine;

namespace Convai.Domain.Embodiment.Readings
{
    /// <summary>
    ///     Immutable snapshot of the character's current attention target as decided by
    ///     <see cref="IAttentionSource" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The reading expresses <em>where</em> the character wants its focus to be, not
    ///         what its eyes/head are actually doing — gaze actuators turn the reading into
    ///         bone and blendshape writes.
    ///     </para>
    ///     <para>
    ///         <see cref="Target" /> may be <c>null</c> even when <see cref="IsValid" /> is
    ///         true (for example, a world-space point of interest with no backing transform);
    ///         consumers should prefer <see cref="SmoothedPoint" /> when performing math and
    ///         only use <see cref="Target" /> for parenting/following behavior.
    ///     </para>
    /// </remarks>
    public readonly struct AttentionReading
    {
        /// <summary>Whether the reading represents a usable attention target this frame.</summary>
        public bool IsValid { get; }

        /// <summary>
        ///     Optional transform the character is attending to. May be <c>null</c> when the
        ///     focus is a world-space point without a scene object. Can become destroyed
        ///     between frames; always null-check before dereferencing.
        /// </summary>
        public Transform Target { get; }

        /// <summary>
        ///     Smoothed world-space point the character is attending to. Expressed in world
        ///     space; gaze actuators are responsible for projecting it into rig-local space.
        /// </summary>
        public Vector3 SmoothedPoint { get; }

        /// <summary>
        ///     Normalized commitment in <c>[0, 1]</c>. <c>0</c> means the character is
        ///     disengaged (no visible gaze commitment), <c>1</c> means fully committed. The
        ///     attention director ramps this up and down so consumers cross-fade naturally.
        /// </summary>
        public float Commitment { get; }

        /// <summary>
        ///     Stable identifier for the current target that changes whenever attention moves
        ///     to a different candidate. Gaze actuators can use it to detect re-targets and
        ///     trigger micro-saccades at the transition instant without comparing transform
        ///     references.
        /// </summary>
        public int TargetGenerationId { get; }

        public AttentionReading(
            bool isValid,
            Transform target,
            Vector3 smoothedPoint,
            float commitment,
            int targetGenerationId)
        {
            IsValid = isValid;
            Target = target;
            SmoothedPoint = smoothedPoint;
            Commitment = commitment < 0f ? 0f : commitment > 1f ? 1f : commitment;
            TargetGenerationId = targetGenerationId;
        }

        /// <summary>Disengaged reading — no attention target.</summary>
        public static AttentionReading Empty => new(false, null, Vector3.zero, 0f, 0);
    }
}
