using Convai.Domain.Embodiment.Readings;

namespace Convai.Domain.Embodiment.Interfaces
{
    /// <summary>
    ///     Combined gaze intent broadcast to eye, head, and upper-body actuators.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Implemented by <c>Convai.Modules.Gaze</c>'s coordinator after combining the
    ///         current <see cref="IAttentionSource" /> with the current
    ///         <see cref="IConversationFlowSource" /> to decide both <em>where</em> to look
    ///         and <em>how committed</em> the look should be. Actuators read this instead of
    ///         directly composing attention and dialogue-phase themselves.
    ///     </para>
    /// </remarks>
    public interface IGazeIntentProvider
    {
        /// <summary>Latest gaze intent for the current frame.</summary>
        GazeIntent Current { get; }
    }
}
