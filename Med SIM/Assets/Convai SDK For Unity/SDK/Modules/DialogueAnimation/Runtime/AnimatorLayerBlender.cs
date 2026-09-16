using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime
{
    /// <summary>
    ///     Owns the smoothed 0..1 weight of a single <see cref="Animator" /> layer and
    ///     applies it through a caller-supplied writer every tick.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The blender has exactly one responsibility: given a target weight and a
    ///         fade duration, ease toward the target at a deterministic, framerate-
    ///         independent rate. Ownership of the layer (registration with the
    ///         <c>AnimatorConductor</c>, layer index bookkeeping) stays with the controller.
    ///     </para>
    ///     <para>
    ///         The fade curve is linear to keep layer-weight arithmetic composable with
    ///         other animation math. Custom easing is left to the controller if a particular
    ///         sample requires it.
    ///     </para>
    /// </remarks>
    public sealed class AnimatorLayerBlender
    {
        private float _currentWeight;
        private float _targetWeight;
        private float _slewRate;

        /// <summary>Current (possibly mid-fade) layer weight.</summary>
        public float CurrentWeight => _currentWeight;

        /// <summary>Target the blender is fading toward.</summary>
        public float TargetWeight => _targetWeight;

        /// <summary>
        ///     <c>true</c> while the current weight has not yet reached the target within
        ///     a single-frame epsilon.
        /// </summary>
        public bool IsFading => !Mathf.Approximately(_currentWeight, _targetWeight);

        /// <summary>Seeds the blender with a specific starting weight without a fade.</summary>
        public void ForceWeight(float weight)
        {
            _currentWeight = Mathf.Clamp01(weight);
            _targetWeight = _currentWeight;
            _slewRate = 0f;
        }

        /// <summary>
        ///     Requests a fade toward <paramref name="target" /> over
        ///     <paramref name="fadeSeconds" />. Calling with the same target re-arms the
        ///     slew rate so a shorter <paramref name="fadeSeconds" /> can accelerate the
        ///     in-flight fade.
        /// </summary>
        public void SetTarget(float target, float fadeSeconds)
        {
            _targetWeight = Mathf.Clamp01(target);

            if (fadeSeconds <= 0f)
            {
                _currentWeight = _targetWeight;
                _slewRate = 0f;
                return;
            }

            float delta = Mathf.Abs(_targetWeight - _currentWeight);
            _slewRate = delta / fadeSeconds;
        }

        /// <summary>
        ///     Advances the fade by <paramref name="deltaTime" /> seconds and returns the
        ///     new current weight. The caller is responsible for writing the returned value
        ///     into the animator (usually via the <c>AnimatorConductor</c>).
        /// </summary>
        public float Tick(float deltaTime)
        {
            if (Mathf.Approximately(_currentWeight, _targetWeight))
            {
                _currentWeight = _targetWeight;
                return _currentWeight;
            }

            float step = _slewRate > 0f ? _slewRate * deltaTime : Mathf.Abs(_targetWeight - _currentWeight);
            _currentWeight = Mathf.MoveTowards(_currentWeight, _targetWeight, step);
            return _currentWeight;
        }
    }
}
