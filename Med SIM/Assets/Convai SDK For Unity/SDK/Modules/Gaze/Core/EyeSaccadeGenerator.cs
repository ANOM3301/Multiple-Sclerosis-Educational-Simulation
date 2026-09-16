using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Gaze.Core
{
    /// <summary>
    ///     Generates the per-frame saccade micro-offset added to the eye's yaw / pitch
    ///     deltas. Owns its own elapsed / interval / cycle state so the actuator never
    ///     touches saccade timing directly.
    /// </summary>
    /// <remarks>
    ///     Saccade phase modulates a sine envelope across <see cref="ConvaiGazeEyeProfile.SaccadeDuration" />,
    ///     yielding a smooth pulse that returns to zero. Intervals between pulses are
    ///     drawn from <c>SaccadeIntervalMean ± SaccadeIntervalJitter</c>.
    /// </remarks>
    internal sealed class EyeSaccadeGenerator
    {
        private float _elapsed;
        private float _interval = 1.8f;
        private Vector2 _currentOffset;
        private Vector2 _activeTarget;
        private float _activeRemaining;

        /// <summary>Saccade offset applied to (yaw, pitch) this frame.</summary>
        public Vector2 CurrentOffset => _currentOffset;

        /// <summary>
        ///     Resets timer + active cycle. Re-seeds <see cref="CurrentOffset" /> to zero and
        ///     samples the next interval from the profile mean.
        /// </summary>
        public void Reset(ConvaiGazeEyeProfile profile)
        {
            _elapsed = 0f;
            _interval = profile != null ? profile.SaccadeIntervalMean : 1.8f;
            _activeRemaining = 0f;
            _currentOffset = Vector2.zero;
        }

        /// <summary>
        ///     Advances the saccade state by <paramref name="deltaTime" />. When saccades are
        ///     disabled in the profile, clears the offset and skips work.
        /// </summary>
        public void Tick(ConvaiGazeEyeProfile profile, float deltaTime, ref DeterministicEmbodimentRandom rng)
        {
            if (profile == null || !profile.EnableSaccades)
            {
                _currentOffset = Vector2.zero;
                return;
            }

            _elapsed += deltaTime;

            if (_activeRemaining > 0f)
            {
                _activeRemaining -= deltaTime;
                float t = Mathf.Clamp01(_activeRemaining / Mathf.Max(0.01f, profile.SaccadeDuration));
                _currentOffset = Vector2.Lerp(Vector2.zero, _activeTarget, Mathf.Sin(t * Mathf.PI));
                if (_activeRemaining <= 0f)
                    _currentOffset = Vector2.zero;
            }

            if (_elapsed >= _interval && _activeRemaining <= 0f)
            {
                _elapsed = 0f;
                _interval = SampleInterval(profile, ref rng);
                _activeTarget = new Vector2(
                    rng.Range(-profile.SaccadeMaxDegrees, profile.SaccadeMaxDegrees),
                    rng.Range(-profile.SaccadeMaxDegrees, profile.SaccadeMaxDegrees) * 0.5f);
                _activeRemaining = profile.SaccadeDuration;
            }
        }

        private static float SampleInterval(ConvaiGazeEyeProfile profile, ref DeterministicEmbodimentRandom rng) =>
            Mathf.Max(0.05f,
                profile.SaccadeIntervalMean + rng.Range(-profile.SaccadeIntervalJitter, profile.SaccadeIntervalJitter));
    }
}
