#if CONVAI_ANIMATION_RIGGING
using Convai.Domain.Embodiment.Readings;
using Convai.Runtime.Embodiment;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Convai.Modules.DialogueAnimation.Rigging
{
    /// <summary>
    ///     Drives Animation-Rigging <see cref="MultiAimConstraint" /> targets and weights from
    ///     the current <see cref="Convai.Domain.Embodiment.Interfaces.IGazeIntentProvider" />
    ///     reading.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This component is the AAA-quality alternative to the procedural
    ///         <c>ConvaiHeadLookActuator</c> / <c>ConvaiEyeGazeActuator</c> components: instead of
    ///         writing bone rotations directly in <c>LateUpdate</c>, it drives Animation
    ///         Rigging constraints that are evaluated inside the animation graph's IK pass.
    ///         This produces natural blending with whatever pose the Animator has produced
    ///         and eliminates the ordering fragility of <c>LateUpdate</c>-based bone writes.
    ///     </para>
    ///     <para>
    ///         The bridge only compiles when the Unity package
    ///         <c>com.unity.animation.rigging</c> is present (<c>CONVAI_ANIMATION_RIGGING</c>
    ///         scripting define). Users can keep the procedural actuators as a fallback for
    ///         projects that do not include the package.
    ///     </para>
    /// </remarks>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(Convai.Runtime.Embodiment.EmbodimentExecutionOrders.EyeGaze)]
    public sealed class AnimationRiggingGazeBridge : MonoBehaviour
    {
        [Header("Constraints")]
        [Tooltip("Head multi-aim constraint driven by head-committed gaze weight.")]
        [SerializeField] private MultiAimConstraint _headConstraint;

        [Tooltip("Eye multi-aim constraint driven by eye-committed gaze weight.")]
        [SerializeField] private MultiAimConstraint _eyeConstraint;

        [Header("Target Pivot")]
        [Tooltip("Transform that the bridge teleports to GazeIntent.WorldTargetPoint each " +
                 "frame. Both constraints' source objects should reference this transform.")]
        [SerializeField] private Transform _gazeTargetPivot;

        [Header("Smoothing")]
        [Tooltip("Smoothing half-life, in seconds, applied to constraint weights. Lower " +
                 "values react faster; higher values feel more graceful.")]
        [SerializeField, Range(0.01f, 1f)] private float _weightSmoothingHalfLife = 0.12f;

        [Header("Behavior")]
        [Tooltip("When the gaze target pivot has no valid world target, the bridge parks it " +
                 "at this local offset from the character root so the constraint sources stay " +
                 "addressable.")]
        [SerializeField] private Vector3 _parkLocalOffset = new(0f, 1.6f, 1.2f);

        private EmbodimentContext _context;
        private float _smoothedHeadWeight;
        private float _smoothedEyeWeight;

        /// <summary>Smoothed head constraint weight currently in use.</summary>
        public float CurrentHeadWeight => _smoothedHeadWeight;

        /// <summary>Smoothed eye constraint weight currently in use.</summary>
        public float CurrentEyeWeight => _smoothedEyeWeight;

        /// <summary>Transform the bridge moves to the current gaze target point.</summary>
        public Transform GazeTargetPivot => _gazeTargetPivot;

        private void OnEnable()
        {
            if (!EmbodimentContext.TryResolveFor(this, out _context))
            {
                enabled = false;
            }
        }

        private void OnDisable()
        {
            _smoothedHeadWeight = 0f;
            _smoothedEyeWeight = 0f;
            ApplyWeights();
        }

        private void LateUpdate()
        {
            if (_context == null) return;

            GazeIntent intent = _context.GazeIntentProvider?.Current ?? GazeIntent.Relaxed;
            UpdatePivot(intent);

            float targetEye = Mathf.Clamp01(intent.OverallWeight * intent.EyeShare);
            float targetHead = Mathf.Clamp01(intent.OverallWeight * (1f - intent.EyeShare));

            float alpha = ComputeSmoothingAlpha(Time.deltaTime, _weightSmoothingHalfLife);
            _smoothedHeadWeight = Mathf.Lerp(_smoothedHeadWeight, targetHead, alpha);
            _smoothedEyeWeight = Mathf.Lerp(_smoothedEyeWeight, targetEye, alpha);

            ApplyWeights();
        }

        private void UpdatePivot(in GazeIntent intent)
        {
            if (_gazeTargetPivot == null) return;

            if (intent.OverallWeight > 0.0001f &&
                intent.WorldTargetPoint != Vector3.zero)
            {
                _gazeTargetPivot.position = intent.WorldTargetPoint;
            }
            else if (_context.CharacterRoot != null)
            {
                _gazeTargetPivot.position = _context.CharacterRoot.TransformPoint(_parkLocalOffset);
            }
        }

        private void ApplyWeights()
        {
            if (_headConstraint != null) _headConstraint.weight = _smoothedHeadWeight;
            if (_eyeConstraint != null) _eyeConstraint.weight = _smoothedEyeWeight;
        }

        private static float ComputeSmoothingAlpha(float deltaTime, float halfLifeSeconds)
        {
            if (halfLifeSeconds <= 0f) return 1f;
            return 1f - Mathf.Pow(0.5f, deltaTime / halfLifeSeconds);
        }
    }
}
#endif
