using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Owns the idle overlay layer-weight blender and applies the static base layer
    ///     weight + smoothed idle overlay layer weight from
    ///     <see cref="DialogueAnimationRuntimeConfig" /> through the
    ///     <see cref="DialogueConductorBinding" /> each tick.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The driver fades between the speaking and idle overlay weight targets only
    ///         when the configured target actually changes, so a stable conversation phase
    ///         does not re-arm the blender every frame.
    ///     </para>
    /// </remarks>
    internal sealed class DialogueIdleLayerWeightDriver
    {
        private const float DefaultBlendSeconds = 0.95f;

        private AnimatorLayerBlender _idleOverlayWeightBlender;
        private float _lastConfigTarget = float.NaN;

        public float CurrentBaseWeight { get; private set; } = 1f;

        public float CurrentIdleOverlayWeight => _idleOverlayWeightBlender?.CurrentWeight ?? 0f;

        public void Initialize(bool initialSpeaking, DialogueAnimationRuntimeConfig config, bool hasValidIdleLibrary)
        {
            float initial = ResolveTarget(initialSpeaking, config, hasValidIdleLibrary);
            _idleOverlayWeightBlender = new AnimatorLayerBlender();
            _idleOverlayWeightBlender.ForceWeight(initial);
            _lastConfigTarget = initial;
            CurrentBaseWeight = config != null ? config.BaseLayerWeight : 1f;
        }

        public void Reset()
        {
            _idleOverlayWeightBlender = null;
            _lastConfigTarget = float.NaN;
            CurrentBaseWeight = 1f;
        }

        /// <summary>
        ///     Advances the idle overlay weight blender by <paramref name="deltaTime" /> and
        ///     writes both the static base layer weight and the smoothed idle overlay
        ///     weight through <paramref name="writer" />.
        /// </summary>
        public void Tick(
            float deltaTime,
            bool speaking,
            DialogueAnimationRuntimeConfig config,
            DialogueConductorBinding writer,
            Object owner,
            int baseLayerIndex,
            int idleOverlayLayerIndex,
            bool hasValidIdleLibrary)
        {
            float idleOverlayTarget = ResolveTarget(speaking, config, hasValidIdleLibrary);
            float smoothedIdleOverlay;

            if (_idleOverlayWeightBlender != null)
            {
                if (!Mathf.Approximately(idleOverlayTarget, _lastConfigTarget))
                {
                    _lastConfigTarget = idleOverlayTarget;
                    float blendSec = config != null ? config.IdleOverlayWeightBlendSeconds : DefaultBlendSeconds;
                    _idleOverlayWeightBlender.SetTarget(idleOverlayTarget, blendSec);
                }

                smoothedIdleOverlay = _idleOverlayWeightBlender.Tick(deltaTime);
            }
            else
            {
                smoothedIdleOverlay = idleOverlayTarget;
            }

            float baseW = config != null ? config.BaseLayerWeight : 1f;
            float idleW = Mathf.Clamp01(smoothedIdleOverlay);
            CurrentBaseWeight = baseW;

            writer.WriteWeight(owner, baseLayerIndex, baseW);
            writer.WriteWeight(owner, idleOverlayLayerIndex, idleW);
        }

        private static float ResolveTarget(bool speaking, DialogueAnimationRuntimeConfig config, bool hasValidIdleLibrary)
        {
            if (!hasValidIdleLibrary) return 0f;
            if (config == null) return 1f;
            return Mathf.Clamp01(speaking
                ? config.IdleOverlayLayerWeightWhileSpeaking
                : config.IdleOverlayLayerWeight);
        }
    }
}
