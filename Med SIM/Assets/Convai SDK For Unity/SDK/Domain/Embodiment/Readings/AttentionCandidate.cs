using UnityEngine;

namespace Convai.Domain.Embodiment.Readings
{
    /// <summary>
    ///     Candidate focus target produced by an <see cref="IFocusTargetProvider" /> and fed
    ///     into the attention director for weighted scheduling.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The attention director uses weighted relevance scoring to select natural focus
    ///         targets. Selection is based on a combination of <see cref="Priority" /> (static),
    ///         <see cref="Relevance" /> (dynamic, 0..1), and remaining interest budget. This
    ///         allows the character to glance between multiple equally-interesting targets
    ///         rather than locking on to the first one indefinitely.
    ///     </para>
    /// </remarks>
    public readonly struct AttentionCandidate
    {
        /// <summary>Stable ordering hint; higher priorities outrank lower ones when ties break.</summary>
        public int Priority { get; }

        /// <summary>Current relevance in <c>[0, 1]</c>. Providers can vary this per-frame.</summary>
        public float Relevance { get; }

        /// <summary>Optional transform backing the candidate.</summary>
        public Transform Target { get; }

        /// <summary>World-space point the character should look at.</summary>
        public Vector3 WorldPoint { get; }

        /// <summary>Short identifier used in diagnostics, editor visualizer, and logs.</summary>
        public string DebugName { get; }

        public AttentionCandidate(
            int priority,
            float relevance,
            Transform target,
            Vector3 worldPoint,
            string debugName)
        {
            Priority = priority;
            Relevance = relevance < 0f ? 0f : relevance > 1f ? 1f : relevance;
            Target = target;
            WorldPoint = worldPoint;
            DebugName = debugName;
        }
    }
}
