using Convai.Modules.ConversationFlow.Core;
using UnityEngine;

namespace Convai.Modules.ConversationFlow.Profiles
{
    /// <summary>
    ///     ScriptableObject authoring of the conversation-flow state machine's timing
    ///     parameters. One asset per behavior preset.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Timings are exposed as a compact set of named values with sensible ranges.
    ///         Animators who want finer control can author multiple profiles (e.g. "Elder
    ///         NPC" with slower thinking, "Child NPC" with quicker reactions).
    ///     </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Convai/Embodiment/Conversation Flow Profile",
        fileName = "ConvaiConversationFlowProfile")]
    public sealed class ConvaiConversationFlowProfile : ScriptableObject
    {
        [Header("Transition")]
        [SerializeField, Range(0f, 2f)]
        [Tooltip("Duration of the linear crossfade between two states.")]
        private float transitionDuration = 0.25f;

        [Header("Thinking")]
        [SerializeField, Range(0f, 3f)]
        [Tooltip("Minimum duration the character remains in Thinking after the player commits a turn.")]
        private float thinkingMinHold = 0.25f;

        [SerializeField, Range(0.5f, 10f)]
        [Tooltip("Maximum duration the character remains in Thinking before falling back to Attending.")]
        private float thinkingMaxHold = 2.5f;

        [Header("Attending")]
        [SerializeField, Range(0f, 2f)]
        [Tooltip("Grace period after the player stops speaking without committing a turn.")]
        private float attendingGracePeriod = 0.3f;

        [Header("Settling")]
        [SerializeField, Range(0f, 3f)]
        [Tooltip("Duration of the post-turn settle beat before returning to Idle.")]
        private float settlingDuration = 0.6f;

        [Header("Idle Return")]
        [SerializeField, Range(0f, 120f)]
        [Tooltip("Seconds of inactivity before the character cools from Attending back to Idle (e.g. ~60 for a full minute).")]
        private float idleReturnDelay = 60f;

        [Header("Interruption")]
        [SerializeField, Range(0f, 2f)]
        [Tooltip("Duration the character freezes after being interrupted.")]
        private float interruptedFreezeDuration = 0.25f;

        [Header("Energy")]
        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Base energy level emitted during Speaking (controls body language intensity).")]
        private float speakingBaseEnergy = 0.6f;

        /// <summary>Builds a timings struct from the authored values.</summary>
        public ConversationFlowTimings ToTimings() => new(
            transitionDuration,
            thinkingMinHold,
            thinkingMaxHold,
            attendingGracePeriod,
            settlingDuration,
            idleReturnDelay,
            interruptedFreezeDuration,
            speakingBaseEnergy);

        /// <summary>Creates a runtime-default profile instance. Used by drivers with no asset assigned.</summary>
        public static ConvaiConversationFlowProfile CreateDefault()
        {
            ConvaiConversationFlowProfile instance = CreateInstance<ConvaiConversationFlowProfile>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }

        /// <summary>
        ///     Creates a profile tuned for conversational NPC dialogue pacing: gentle
        ///     crossfades, a short thinking beat, tight attending grace, and a clearly
        ///     telegraphed settle before returning to idle.
        /// </summary>
        public static ConvaiConversationFlowProfile CreateConversationalPreset()
        {
            ConvaiConversationFlowProfile instance = CreateInstance<ConvaiConversationFlowProfile>();
            instance.transitionDuration = 0.28f;
            instance.thinkingMinHold = 0.35f;
            instance.thinkingMaxHold = 2.2f;
            instance.attendingGracePeriod = 0.35f;
            instance.settlingDuration = 0.7f;
            instance.idleReturnDelay = 60f;
            instance.interruptedFreezeDuration = 0.22f;
            instance.speakingBaseEnergy = 0.55f;
            return instance;
        }
    }
}
