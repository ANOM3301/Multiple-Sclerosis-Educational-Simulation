using Convai.Domain.Embodiment.Readings;

namespace Convai.Domain.Embodiment.Interfaces
{
    /// <summary>
    ///     Authoritative view of the character's current attention target.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Implemented by <c>Convai.Modules.Attention</c>. Eye and head actuators read
    ///         from this contract instead of the attention module directly so the
    ///         implementation is swappable (e.g. a cinematic override, replay systems, tests).
    ///     </para>
    ///     <para>
    ///         Readings are recomputed each frame; the source does NOT raise change events
    ///         because gaze actuators already poll in their tick. Changes are signaled through
    ///         <see cref="AttentionReading.TargetGenerationId" />.
    ///     </para>
    /// </remarks>
    public interface IAttentionSource
    {
        /// <summary>Latest attention reading sampled at the start of the current frame.</summary>
        AttentionReading Current { get; }
    }
}
