using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Emotion.Core
{
    /// <summary>
    ///     Periodically alternates a blend factor between 0 (full emotion) and 1 (neutral) to
    ///     create more life-like facial animation.
    /// </summary>
    /// <remarks>
    ///     The alternator runs a repeating cycle:
    ///     <list type="number">
    ///         <item>Hold current state for a random duration within [minInterval, maxInterval].</item>
    ///         <item>Smoothly transition to the opposite state over <see cref="BlendDuration" /> seconds.</item>
    ///         <item>Repeat.</item>
    ///     </list>
    ///     Can be gated so it only alternates while the character is speaking.
    /// </remarks>
    public sealed class NeutralAlternator
    {
        private float _minInterval;
        private float _maxInterval;
        private float _blendDuration;
        private bool _onlyWhileTalking;

        private float _holdTimer;
        private float _holdDuration;
        private bool _targetIsNeutral;

        private float _currentFactor;
        private float _targetFactor;

        private bool _isTalking;
        private DeterministicEmbodimentRandom _random;

        /// <summary>
        ///     Creates a new alternator with the specified timing parameters.
        /// </summary>
        /// <param name="minInterval">Minimum hold time before alternating (seconds).</param>
        /// <param name="maxInterval">Maximum hold time before alternating (seconds).</param>
        /// <param name="blendDuration">Duration of the smooth transition between states (seconds).</param>
        /// <param name="onlyWhileTalking">When true, the alternator only runs while the character is speaking.</param>
        public NeutralAlternator(
            float minInterval = 3f,
            float maxInterval = 7f,
            float blendDuration = 1f,
            bool onlyWhileTalking = false,
            uint seed = 0u)
        {
            _minInterval = minInterval;
            _maxInterval = maxInterval;
            _blendDuration = Mathf.Max(0.01f, blendDuration);
            _onlyWhileTalking = onlyWhileTalking;
            _random = new DeterministicEmbodimentRandom(seed);

            _holdDuration = SampleHoldDuration();
            _holdTimer = 0f;
            _targetIsNeutral = false;
            _currentFactor = 0f;
            _targetFactor = 0f;
        }

        /// <summary>
        ///     Current blend factor: 0 = full emotion, 1 = fully neutral.
        ///     Apply this as a multiplier that reduces emotion intensity.
        /// </summary>
        public float Factor => _currentFactor;

        /// <summary>Whether the alternator is currently active (running its cycle).</summary>
        public bool IsActive { get; private set; }

        /// <summary>
        ///     Reconfigures the alternator at runtime.
        /// </summary>
        public void Configure(float minInterval, float maxInterval, float blendDuration, bool onlyWhileTalking)
        {
            _minInterval = minInterval;
            _maxInterval = maxInterval;
            _blendDuration = Mathf.Max(0.01f, blendDuration);
            _onlyWhileTalking = onlyWhileTalking;
        }

        /// <summary>
        ///     Notifies the alternator that the character's speech state changed.
        /// </summary>
        public void SetTalkingState(bool isTalking)
        {
            _isTalking = isTalking;
        }

        /// <summary>
        ///     Advances the alternator by <paramref name="deltaTime" /> seconds.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            bool shouldRun = !_onlyWhileTalking || _isTalking;

            if (shouldRun)
            {
                IsActive = true;
                _holdTimer += deltaTime;

                if (_holdTimer >= _holdDuration)
                {
                    _holdTimer = 0f;
                    _holdDuration = SampleHoldDuration();
                    _targetIsNeutral = !_targetIsNeutral;
                    _targetFactor = _targetIsNeutral ? 1f : 0f;
                }
            }
            else
            {
                IsActive = false;
                _targetFactor = 0f;
            }

            // Smooth transition toward target factor using exponential approach
            float speed = 1f / _blendDuration;
            float factor = 1f - Mathf.Exp(-speed * deltaTime);
            _currentFactor += (_targetFactor - _currentFactor) * factor;

            if (Mathf.Abs(_currentFactor - _targetFactor) < 0.001f)
                _currentFactor = _targetFactor;
        }

        /// <summary>
        ///     Immediately resets the alternator to its initial state (factor = 0, full emotion).
        /// </summary>
        public void Reset()
        {
            _holdTimer = 0f;
            _holdDuration = SampleHoldDuration();
            _targetIsNeutral = false;
            _currentFactor = 0f;
            _targetFactor = 0f;
            IsActive = false;
        }

        private float SampleHoldDuration() =>
            _random.Range(_minInterval, _maxInterval);
    }
}
