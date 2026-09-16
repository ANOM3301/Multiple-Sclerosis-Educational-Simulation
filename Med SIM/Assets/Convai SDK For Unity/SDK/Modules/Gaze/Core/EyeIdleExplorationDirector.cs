using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Gaze.Core
{
    /// <summary>
    ///     Picks the eye's idle exploration target (yaw / pitch offset relative to the head
    ///     forward) and re-samples it on the configured cadence. Pure logic; the actuator
    ///     applies the offset by composing it onto the resolved head reference frame.
    /// </summary>
    internal sealed class EyeIdleExplorationDirector
    {
        private const float DefaultIntervalSeconds = 1.5f;

        private Vector2 _angles;
        private float _nextSampleTime;

        /// <summary>Currently chosen yaw/pitch (degrees).</summary>
        public Vector2 Angles => _angles;

        /// <summary>Resets state and schedules the next sample relative to <paramref name="now" />.</summary>
        public void Reset(ConvaiGazeEyeProfile profile, float now, ref DeterministicEmbodimentRandom rng)
        {
            _angles = Vector2.zero;
            _nextSampleTime = now + SampleInterval(profile, ref rng);
        }

        /// <summary>
        ///     Advances the director: clears angles when not active, samples a new target when
        ///     <paramref name="now" /> reaches the next-sample window, otherwise leaves state alone.
        /// </summary>
        public void Tick(
            ConvaiGazeEyeProfile profile,
            float now,
            bool isActive,
            ref DeterministicEmbodimentRandom rng)
        {
            if (profile == null || !profile.EnableIdleExploration)
            {
                _angles = Vector2.zero;
                return;
            }

            if (!isActive)
            {
                _angles = Vector2.zero;
                _nextSampleTime = now + SampleInterval(profile, ref rng);
                return;
            }

            if (now < _nextSampleTime) return;

            _angles = SampleAngles(profile, ref rng);
            _nextSampleTime = now + SampleInterval(profile, ref rng);
        }

        /// <summary>
        ///     Composes the chosen yaw/pitch onto <paramref name="referenceForward" />, weighted
        ///     by <see cref="ConvaiGazeEyeProfile.IdleExplorationWeight" />.
        /// </summary>
        public Vector3 ResolveDirection(
            Transform reference,
            Vector3 referenceForward,
            ConvaiGazeEyeProfile profile)
        {
            if (referenceForward.sqrMagnitude <= 1e-6f)
                return Vector3.forward;

            Vector3 baseForward = referenceForward.normalized;
            if (reference == null || profile == null || _angles.sqrMagnitude <= 1e-6f)
                return baseForward;

            Quaternion delta =
                Quaternion.AngleAxis(_angles.x, reference.up) *
                Quaternion.AngleAxis(_angles.y, reference.right);
            Vector3 sampledForward = (delta * baseForward).normalized;
            return Vector3.Slerp(baseForward, sampledForward, Mathf.Clamp01(profile.IdleExplorationWeight)).normalized;
        }

        private static float SampleInterval(ConvaiGazeEyeProfile profile, ref DeterministicEmbodimentRandom rng)
        {
            if (profile == null) return DefaultIntervalSeconds;
            return rng.Range(profile.IdleExplorationIntervalMin, profile.IdleExplorationIntervalMax);
        }

        private static Vector2 SampleAngles(ConvaiGazeEyeProfile profile, ref DeterministicEmbodimentRandom rng)
        {
            if (profile == null) return Vector2.zero;
            if (rng.Value <= profile.IdleRecenteringChance) return Vector2.zero;

            float yaw = rng.Range(-profile.IdleExplorationHorizontalDegrees, profile.IdleExplorationHorizontalDegrees);
            float pitch = rng.Range(-profile.IdleExplorationUpDegrees, profile.IdleExplorationDownDegrees);
            Vector2 sampled = new(yaw, pitch);
            float centerBias = profile.IdleExplorationCenterBias * rng.Value;
            return Vector2.Lerp(sampled, Vector2.zero, centerBias);
        }
    }
}
