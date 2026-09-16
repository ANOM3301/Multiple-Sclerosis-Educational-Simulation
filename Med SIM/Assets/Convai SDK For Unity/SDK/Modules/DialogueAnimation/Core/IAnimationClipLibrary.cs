using System.Collections.Generic;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Read-only access to a character's idle and talk clip pools.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The controller depends on this interface rather than the concrete
    ///         <see cref="DialogueAnimationLibrary" /> so alternative library implementations
    ///         (scripted, procedurally generated, remote-loaded) can be substituted without
    ///         touching the controller.
    ///     </para>
    ///     <para>
    ///         Implementations must return stable references for the duration of a frame ;
    ///         the controller reads the lists once per selection and does not defensively
    ///         copy them.
    ///     </para>
    /// </remarks>
    public interface IAnimationClipLibrary
    {
        /// <summary>Idle pool. Never null.</summary>
        IReadOnlyList<DialogueClipEntry> IdleEntries { get; }

        /// <summary>Talk pool. Never null.</summary>
        IReadOnlyList<DialogueClipEntry> TalkEntries { get; }

        /// <summary>
        ///     Default crossfade duration, in seconds, used when individual entries do not
        ///     specify their own override.
        /// </summary>
        float DefaultCrossFadeDuration { get; }
    }
}
