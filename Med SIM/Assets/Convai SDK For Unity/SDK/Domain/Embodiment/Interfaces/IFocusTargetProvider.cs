using Convai.Domain.Embodiment.Readings;
using UnityEngine;

namespace Convai.Domain.Embodiment.Interfaces
{
    /// <summary>
    ///     Extension point for supplying attention candidates to the attention director.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Providers are discovered once during module startup (not per-frame) so users
    ///         are free to implement <see cref="IFocusTargetProvider" /> on any
    ///         <c>MonoBehaviour</c> sitting next to the character. Dynamic providers
    ///         (e.g. a "point at currently-speaking NPC" provider) vary
    ///         <see cref="AttentionCandidate.Relevance" /> per-frame rather than
    ///         registering and unregistering.
    ///     </para>
    ///     <para>
    ///         A provider returning <c>false</c> is treated as "no candidate this frame" and
    ///         is skipped. Providers MUST NOT allocate on the hot path; the director calls
    ///         every registered provider once per frame.
    ///     </para>
    /// </remarks>
    public interface IFocusTargetProvider
    {
        /// <summary>
        ///     Static priority used as a tie-breaker when two candidates have equal
        ///     <see cref="AttentionCandidate.Relevance" />. Higher wins.
        /// </summary>
        int Priority { get; }

        /// <summary>
        ///     Produces an attention candidate for the current frame relative to
        ///     <paramref name="characterRoot" />.
        /// </summary>
        /// <param name="characterRoot">The character's root transform.</param>
        /// <param name="candidate">The produced candidate (meaningful only when the call returns <c>true</c>).</param>
        /// <returns><c>true</c> when a candidate is available this frame.</returns>
        bool TryGetCandidate(Transform characterRoot, out AttentionCandidate candidate);
    }
}
