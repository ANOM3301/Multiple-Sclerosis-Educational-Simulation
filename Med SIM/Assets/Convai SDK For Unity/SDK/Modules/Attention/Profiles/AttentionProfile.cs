using Convai.Modules.Attention.Core;
using UnityEngine;

namespace Convai.Modules.Attention.Profiles
{
    /// <summary>
    ///     ScriptableObject authoring of <see cref="Core.WeightedAttentionDirector" />
    ///     parameters.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Attention authors a "committed but not creepy" gaze behavior by distributing
    ///         attention across candidates using a per-target budget and an interest-decay
    ///         model. Stoic NPCs hold gaze longer with low decay; curious NPCs break gaze
    ///         frequently with higher decay.
    ///     </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Convai/Embodiment/Attention Profile",
        fileName = "ConvaiAttentionProfile")]
    public sealed class ConvaiAttentionProfile : ScriptableObject
    {
        [Header("Commitment")]
        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Seconds needed to ramp commitment from 0 -> 1 after acquiring a new target.")]
        private float commitmentAcquireSeconds = 0.3f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Seconds needed to decay commitment 1 -> 0 after losing a target.")]
        private float commitmentReleaseSeconds = 0.5f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Grace period after target loss during which the character keeps looking at the last known point.")]
        private float focusLossHoldSeconds = 0.25f;

        [Header("Budget")]
        [SerializeField, Range(0.5f, 20f)]
        [Tooltip("Seconds a target may hold attention continuously before interest decay takes over.")]
        private float maxContinuousHoldSeconds = 5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Interest floor  -  attention below this value forces a break to another candidate.")]
        private float interestBreakThreshold = 0.15f;

        [SerializeField, Range(0.05f, 5f)]
        [Tooltip("Rate at which continuous attention drains interest (per second).")]
        private float interestDecayPerSecond = 0.15f;

        [SerializeField, Range(0.05f, 5f)]
        [Tooltip("Rate at which neglected candidates recover interest (per second).")]
        private float interestRecoveryPerSecond = 0.25f;

        [Header("Smoothing")]
        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("Rate at which the smoothed focus point follows the selected candidate (exponential).")]
        private float focusPositionLerpSpeed = 10f;

        [SerializeField]
        [Tooltip("World-space offset applied to the resolved focus point (e.g. eye height lift).")]
        private Vector3 focusOffset = new(0f, 0.05f, 0f);

        [Header("Default Focus Provider")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Base relevance used by the auto-created camera/player focus provider.")]
        private float defaultFocusBaseRelevance = 0.8f;

        [SerializeField, Min(0f)]
        [Tooltip("Vertical lift for explicit default focus targets. Camera targets stay at camera height.")]
        private float defaultFocusTargetHeadHeight = 0f;

        [SerializeField, Min(0f)]
        [Tooltip("Distance where default focus fades to zero.")]
        private float defaultFocusMaxDistance = 8f;

        [SerializeField, Min(0f)]
        [Tooltip("Distance where default focus is at full relevance.")]
        private float defaultFocusFullRelevanceDistance = 3f;

        public float CommitmentAcquireSeconds => commitmentAcquireSeconds;
        public float CommitmentReleaseSeconds => commitmentReleaseSeconds;
        public float FocusLossHoldSeconds => focusLossHoldSeconds;
        public float MaxContinuousHoldSeconds => maxContinuousHoldSeconds;
        public float InterestBreakThreshold => interestBreakThreshold;
        public float InterestDecayPerSecond => interestDecayPerSecond;
        public float InterestRecoveryPerSecond => interestRecoveryPerSecond;
        public float FocusPositionLerpSpeed => focusPositionLerpSpeed;
        public Vector3 FocusOffset => focusOffset;
        public float DefaultFocusBaseRelevance => defaultFocusBaseRelevance;
        public float DefaultFocusTargetHeadHeight => defaultFocusTargetHeadHeight;
        public float DefaultFocusMaxDistance => defaultFocusMaxDistance;
        public float DefaultFocusFullRelevanceDistance => defaultFocusFullRelevanceDistance;

        private void OnValidate()
        {
            commitmentAcquireSeconds = Mathf.Max(0.1f, commitmentAcquireSeconds);
            commitmentReleaseSeconds = Mathf.Max(commitmentAcquireSeconds, commitmentReleaseSeconds);
            focusLossHoldSeconds = Mathf.Max(0f, focusLossHoldSeconds);
            maxContinuousHoldSeconds = Mathf.Max(0.5f, maxContinuousHoldSeconds);
            interestBreakThreshold = Mathf.Clamp01(interestBreakThreshold);
            interestDecayPerSecond = Mathf.Max(0.05f, interestDecayPerSecond);
            interestRecoveryPerSecond = Mathf.Max(0.05f, interestRecoveryPerSecond);
            focusPositionLerpSpeed = Mathf.Max(0.1f, focusPositionLerpSpeed);
            defaultFocusBaseRelevance = Mathf.Clamp01(defaultFocusBaseRelevance);
            defaultFocusTargetHeadHeight = Mathf.Max(0f, defaultFocusTargetHeadHeight);
            defaultFocusMaxDistance = Mathf.Max(0f, defaultFocusMaxDistance);
            defaultFocusFullRelevanceDistance = Mathf.Clamp(
                defaultFocusFullRelevanceDistance, 0f, defaultFocusMaxDistance);
        }

        /// <summary>Builds a timings struct from the authored values.</summary>
        public AttentionTimings ToTimings() => new(
            commitmentAcquireSeconds,
            commitmentReleaseSeconds,
            focusLossHoldSeconds,
            maxContinuousHoldSeconds,
            interestBreakThreshold,
            interestDecayPerSecond,
            interestRecoveryPerSecond,
            focusPositionLerpSpeed,
            focusOffset);

        /// <summary>Creates a runtime default profile (used when no asset is wired up).</summary>
        public static ConvaiAttentionProfile CreateDefault()
        {
            ConvaiAttentionProfile instance = CreateInstance<ConvaiAttentionProfile>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }

        /// <summary>
        ///     Conversational tuning: committed eye contact that naturally breaks every ~5
        ///     seconds, recovers interest in neglected candidates within a few seconds, and
        ///     lifts the focus point to roughly eye height above the source.
        /// </summary>
        public static ConvaiAttentionProfile CreateConversationalPreset()
        {
            ConvaiAttentionProfile instance = CreateInstance<ConvaiAttentionProfile>();
            instance.commitmentAcquireSeconds = 0.35f;
            instance.commitmentReleaseSeconds = 0.55f;
            instance.focusLossHoldSeconds = 0.3f;
            instance.maxContinuousHoldSeconds = 5.5f;
            instance.interestBreakThreshold = 0.18f;
            instance.interestDecayPerSecond = 0.16f;
            instance.interestRecoveryPerSecond = 0.28f;
            instance.focusPositionLerpSpeed = 9f;
            instance.focusOffset = new Vector3(0f, 0.06f, 0f);
            instance.defaultFocusBaseRelevance = 0.8f;
            instance.defaultFocusTargetHeadHeight = 0f;
            instance.defaultFocusMaxDistance = 8f;
            instance.defaultFocusFullRelevanceDistance = 3f;
            return instance;
        }
    }
}
