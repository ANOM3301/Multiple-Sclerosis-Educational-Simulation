using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Gaze.Core
{
    /// <summary>
    ///     Tracks the inter-blink interval and the current blink cycle remaining, exposing the
    ///     blink phase weight (0..1) the actuator submits to the facial compositor each frame.
    /// </summary>
    /// <remarks>
    ///     The scheduler is purely time-based: it has no knowledge of blendshape targets or
    ///     the compositor itself. Pair with <c>EyeBlendshapeWriter</c> to actually push the
    ///     resulting weight to the rig.
    /// </remarks>
    internal sealed class EyeBlinkScheduler
    {
        private float _elapsed;
        private float _interval = 3.2f;
        private float _cycleRemaining;

        /// <summary>Whether a blink cycle is currently in flight.</summary>
        public bool IsBlinking => _cycleRemaining > 0f;

        /// <summary>Time remaining in the current blink cycle (0 when idle).</summary>
        public float CycleRemaining => _cycleRemaining;

        public void Reset(ConvaiGazeEyeProfile profile)
        {
            _elapsed = 0f;
            _interval = profile != null ? profile.BlinkIntervalMean : 3.2f;
            _cycleRemaining = 0f;
        }

        /// <summary>
        ///     Advances the blink timer by <paramref name="deltaTime" />. When blinking is
        ///     disabled in the profile, clears the cycle and skips work.
        /// </summary>
        public void Tick(ConvaiGazeEyeProfile profile, float deltaTime, ref DeterministicEmbodimentRandom rng)
        {
            if (profile == null || !profile.EnableBlink)
            {
                _cycleRemaining = 0f;
                return;
            }

            _elapsed += deltaTime;
            if (_cycleRemaining > 0f)
                _cycleRemaining -= deltaTime;

            if (_elapsed >= _interval && _cycleRemaining <= 0f)
            {
                _elapsed = 0f;
                _interval = SampleInterval(profile, ref rng);
                _cycleRemaining = profile.BlinkCycleDuration;
            }
        }

        /// <summary>
        ///     Returns the current blink weight in the closed interval [0, 100]. Zero when no
        ///     cycle is active; sine-shaped pulse otherwise.
        /// </summary>
        public float EvaluateWeight(ConvaiGazeEyeProfile profile)
        {
            if (_cycleRemaining <= 0f || profile == null) return 0f;
            float phase = 1f - Mathf.Clamp01(_cycleRemaining / Mathf.Max(0.01f, profile.BlinkCycleDuration));
            return Mathf.Sin(phase * Mathf.PI) * 100f;
        }

        private static float SampleInterval(ConvaiGazeEyeProfile profile, ref DeterministicEmbodimentRandom rng) =>
            Mathf.Max(0.5f,
                profile.BlinkIntervalMean + rng.Range(-profile.BlinkIntervalJitter, profile.BlinkIntervalJitter));
    }
}
