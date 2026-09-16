namespace Convai.Runtime.Animation
{
    /// <summary>
    ///     Phase a tickable registers for within the <see cref="EmbodimentTickScheduler" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Phases execute strictly in enum-declared order each frame. Within a phase,
    ///         individual registrations run in insertion order, which is deterministic and
    ///         independent of Unity's <see cref="UnityEngine.DefaultExecutionOrder" /> lottery.
    ///     </para>
    /// </remarks>
    public enum EmbodimentTickPhase
    {
        /// <summary>
        ///     Cognition step ; conversation flow, attention, emotion directors sample
        ///     signals and update their readings.
        /// </summary>
        Cognition = 0,

        /// <summary>
        ///     Expression step ; gaze, body, facial actuators translate cognition readings
        ///     into bone / blendshape / animator-parameter writes.
        /// </summary>
        Expression = 1,

        /// <summary>
        ///     Late finalization ; facial compositor and animator conductor finalize their
        ///     writes after both cognition and expression have settled.
        /// </summary>
        Finalize = 2
    }

    /// <summary>
    ///     Contract for any embodiment module component that wants to be ticked in a
    ///     deterministic order by <see cref="EmbodimentTickScheduler" /> rather than relying on
    ///     Unity's per-component Update / LateUpdate ordering.
    /// </summary>
    public interface IEmbodimentTickable
    {
        /// <summary>Phase this tickable runs in.</summary>
        EmbodimentTickPhase Phase { get; }

        /// <summary>
        ///     Per-frame tick. Called with <see cref="UnityEngine.Time.deltaTime" /> unless
        ///     the scheduler is configured otherwise (e.g. during edit-mode preview).
        /// </summary>
        void EmbodimentTick(float deltaTime);
    }
}
