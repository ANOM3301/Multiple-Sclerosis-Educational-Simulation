using UnityEngine;

namespace Convai.Modules.Attention.Core
{
    /// <summary>
    ///     Immutable parameter bundle consumed by the <see cref="WeightedAttentionDirector" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Kept as a <c>readonly struct</c> so the pure-POCO director never references
    ///         <c>ScriptableObject</c>s directly. Profiles build these structs at the start of
    ///         each tick so live-tuning in the inspector applies immediately.
    ///     </para>
    /// </remarks>
    public readonly struct AttentionTimings
    {
        public float CommitmentAcquireSeconds { get; }
        public float CommitmentReleaseSeconds { get; }
        public float FocusLossHoldSeconds { get; }
        public float MaxContinuousHoldSeconds { get; }
        public float InterestBreakThreshold { get; }
        public float InterestDecayPerSecond { get; }
        public float InterestRecoveryPerSecond { get; }
        public float FocusPositionLerpSpeed { get; }
        public Vector3 FocusOffset { get; }

        public AttentionTimings(
            float commitmentAcquireSeconds,
            float commitmentReleaseSeconds,
            float focusLossHoldSeconds,
            float maxContinuousHoldSeconds,
            float interestBreakThreshold,
            float interestDecayPerSecond,
            float interestRecoveryPerSecond,
            float focusPositionLerpSpeed,
            Vector3 focusOffset)
        {
            CommitmentAcquireSeconds = Mathf.Max(0.01f, commitmentAcquireSeconds);
            CommitmentReleaseSeconds = Mathf.Max(0.01f, commitmentReleaseSeconds);
            FocusLossHoldSeconds = Mathf.Max(0f, focusLossHoldSeconds);
            MaxContinuousHoldSeconds = Mathf.Max(0.1f, maxContinuousHoldSeconds);
            InterestBreakThreshold = Mathf.Clamp01(interestBreakThreshold);
            InterestDecayPerSecond = Mathf.Max(0f, interestDecayPerSecond);
            InterestRecoveryPerSecond = Mathf.Max(0f, interestRecoveryPerSecond);
            FocusPositionLerpSpeed = Mathf.Max(0f, focusPositionLerpSpeed);
            FocusOffset = focusOffset;
        }
    }
}
