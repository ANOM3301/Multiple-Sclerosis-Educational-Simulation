using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Animation;
using UnityEngine;

namespace Convai.Modules.Gaze.Core
{
    /// <summary>
    ///     Owns the blink and eyelid-follow blendshape submissions for the eye gaze pass.
    ///     Caches resolved <see cref="BlendshapeTargetKey" /> lists from the rig binding so
    ///     each frame just needs to refresh weights, not re-resolve targets.
    /// </summary>
    internal sealed class EyeBlendshapeWriter
    {
        private const float SubmitEpsilon = 0.0001f;

        private readonly List<BlendshapeTargetKey> _blinkTargets = new(2);
        private readonly List<float> _blinkWeights = new(2);
        private readonly EyeBlendshapeTargets _leftTargets = new();
        private readonly EyeBlendshapeTargets _rightTargets = new();
        private readonly List<BlendshapeTargetKey> _eyelidFrameTargets = new(16);
        private readonly List<float> _eyelidFrameWeights = new(16);

        private EyelidFollowState _leftFollow;
        private EyelidFollowState _rightFollow;

        /// <summary>
        ///     Re-resolves blink and eyelid blendshape targets from <paramref name="rigBinding" />.
        ///     Clears all caches first so a rebind on a different mesh produces correct keys.
        /// </summary>
        public void Bind(IStandardRigBinding rigBinding)
        {
            ClearTargets();
            if (rigBinding == null) return;

            CollectTargets(rigBinding, StandardBlendshape.EyeBlinkLeft, _blinkTargets);
            CollectTargets(rigBinding, StandardBlendshape.EyeBlinkRight, _blinkTargets);

            CollectTargets(rigBinding, StandardBlendshape.EyeBlinkLeft, _leftTargets.Blink);
            CollectTargets(rigBinding, StandardBlendshape.EyeLookDownLeft, _leftTargets.LookDown);
            CollectTargets(rigBinding, StandardBlendshape.EyeLookUpLeft, _leftTargets.LookUp);
            CollectTargets(rigBinding, StandardBlendshape.EyeSquintLeft, _leftTargets.Squint);
            CollectTargets(rigBinding, StandardBlendshape.EyeWideLeft, _leftTargets.Wide);
            CollectTargets(rigBinding, StandardBlendshape.EyeUpperLidDownLeft, _leftTargets.UpperLidDown);
            CollectTargets(rigBinding, StandardBlendshape.EyeUpperLidUpLeft, _leftTargets.UpperLidUp);
            CollectTargets(rigBinding, StandardBlendshape.EyeLowerLidUpLeft, _leftTargets.LowerLidUp);

            CollectTargets(rigBinding, StandardBlendshape.EyeBlinkRight, _rightTargets.Blink);
            CollectTargets(rigBinding, StandardBlendshape.EyeLookDownRight, _rightTargets.LookDown);
            CollectTargets(rigBinding, StandardBlendshape.EyeLookUpRight, _rightTargets.LookUp);
            CollectTargets(rigBinding, StandardBlendshape.EyeSquintRight, _rightTargets.Squint);
            CollectTargets(rigBinding, StandardBlendshape.EyeWideRight, _rightTargets.Wide);
            CollectTargets(rigBinding, StandardBlendshape.EyeUpperLidDownRight, _rightTargets.UpperLidDown);
            CollectTargets(rigBinding, StandardBlendshape.EyeUpperLidUpRight, _rightTargets.UpperLidUp);
            CollectTargets(rigBinding, StandardBlendshape.EyeLowerLidUpRight, _rightTargets.LowerLidUp);
        }

        /// <summary>Clears every cached target list (used on rig rebind / disable).</summary>
        public void ClearTargets()
        {
            _blinkTargets.Clear();
            _leftTargets.Clear();
            _rightTargets.Clear();
            _eyelidFrameTargets.Clear();
            _eyelidFrameWeights.Clear();
        }

        /// <summary>Resets the per-eye eyelid-follow smoothing state.</summary>
        public void ResetEyelidFollow()
        {
            _leftFollow = default;
            _rightFollow = default;
            _eyelidFrameTargets.Clear();
            _eyelidFrameWeights.Clear();
        }

        /// <summary>
        ///     Submits a uniform blink weight (0..100) to all collected blink blendshape
        ///     targets. No-op when <paramref name="compositor" /> is null or no blink targets
        ///     were resolved.
        /// </summary>
        public void SubmitBlink(FacialBlendshapeCompositorHost compositor, IFacialBlendshapeSource owner, float weight)
        {
            if (compositor == null || owner == null) return;
            if (_blinkTargets.Count == 0) return;

            _blinkWeights.Clear();
            for (int i = 0; i < _blinkTargets.Count; i++) _blinkWeights.Add(weight);

            compositor.SubmitLayer(
                owner, FacialBlendshapeLayers.Eyes, _blinkTargets, _blinkWeights, _blinkTargets.Count);
        }

        /// <summary>
        ///     Submits the smoothed eyelid-follow shapes for both eyes derived from this
        ///     frame's applied yaw/pitch deltas. No-op when eyelid follow is disabled in the
        ///     profile or no targets were resolved.
        /// </summary>
        public void SubmitEyelidFollow(
            FacialBlendshapeCompositorHost compositor,
            IFacialBlendshapeSource owner,
            ConvaiGazeEyeProfile profile,
            Vector2 leftAngles,
            Vector2 rightAngles,
            float deltaTime)
        {
            if (profile == null || !profile.EnableEyelidFollow)
            {
                ResetEyelidFollow();
                return;
            }
            if (compositor == null || owner == null) return;

            _eyelidFrameTargets.Clear();
            _eyelidFrameWeights.Clear();

            float alpha = 1f - Mathf.Exp(-Mathf.Max(0.1f, profile.EyelidFollowSharpness) * deltaTime);
            AppendEyelidFollow(_leftTargets, leftAngles, profile, alpha, ref _leftFollow);
            AppendEyelidFollow(_rightTargets, rightAngles, profile, alpha, ref _rightFollow);

            if (_eyelidFrameTargets.Count == 0) return;

            compositor.SubmitLayer(
                owner,
                FacialBlendshapeLayers.Eyes,
                _eyelidFrameTargets,
                _eyelidFrameWeights,
                _eyelidFrameTargets.Count);
        }

        private void AppendEyelidFollow(
            EyeBlendshapeTargets targets,
            Vector2 appliedAngles,
            ConvaiGazeEyeProfile profile,
            float alpha,
            ref EyelidFollowState state)
        {
            float yaw = appliedAngles.x;
            float pitch = appliedAngles.y;
            float down = EvaluateRange(pitch, profile.DownwardLidStartDegrees, profile.DownwardLidFullDegrees);
            float up = EvaluateRange(-pitch, profile.UpwardLidStartDegrees, profile.UpwardLidFullDegrees);
            float strain = EvaluateRange(
                Mathf.Abs(yaw) + Mathf.Abs(pitch) * 0.65f,
                profile.ExtremeGazeSquintStartDegrees,
                profile.ExtremeGazeSquintFullDegrees);

            float targetUpperDown = down * profile.DownwardUpperLidMaxWeight;
            float targetBlinkFallback = down * profile.DownwardBlinkFallbackMaxWeight;
            float targetLookDown = down * profile.DownwardLookShapeMaxWeight;
            float targetLowerUp = down * profile.DownwardLowerLidMaxWeight;
            float targetUpperUp = up * profile.UpwardEyeWideMaxWeight;
            float targetWide = targets.HasUpperLidUp ? 0f : up * profile.UpwardEyeWideMaxWeight;
            float targetLookUp = up * profile.UpwardLookShapeMaxWeight;
            float targetSquint = Mathf.Max(
                strain * profile.ExtremeGazeSquintMaxWeight,
                targets.HasLowerLidUp ? 0f : targetLowerUp);

            state.UpperLidDown = Mathf.Lerp(state.UpperLidDown, targetUpperDown, alpha);
            state.BlinkFallback = Mathf.Lerp(state.BlinkFallback, targetBlinkFallback, alpha);
            state.LookDown = Mathf.Lerp(state.LookDown, targetLookDown, alpha);
            state.LowerLidUp = Mathf.Lerp(state.LowerLidUp, targetLowerUp, alpha);
            state.UpperLidUp = Mathf.Lerp(state.UpperLidUp, targetUpperUp, alpha);
            state.Wide = Mathf.Lerp(state.Wide, targetWide, alpha);
            state.LookUp = Mathf.Lerp(state.LookUp, targetLookUp, alpha);
            state.Squint = Mathf.Lerp(state.Squint, targetSquint, alpha);

            AppendWeightedTargets(targets.UpperLidDown, state.UpperLidDown);
            AppendWeightedTargets(targets.Blink, state.BlinkFallback);
            AppendWeightedTargets(targets.LookDown, state.LookDown);
            AppendWeightedTargets(targets.LowerLidUp, state.LowerLidUp);
            AppendWeightedTargets(targets.UpperLidUp, state.UpperLidUp);
            AppendWeightedTargets(targets.Wide, state.Wide);
            AppendWeightedTargets(targets.LookUp, state.LookUp);
            AppendWeightedTargets(targets.Squint, state.Squint);
        }

        private void AppendWeightedTargets(IReadOnlyList<BlendshapeTargetKey> targets, float weight)
        {
            if (targets == null || targets.Count == 0) return;
            if (weight <= SubmitEpsilon) return;

            for (int i = 0; i < targets.Count; i++)
            {
                _eyelidFrameTargets.Add(targets[i]);
                _eyelidFrameWeights.Add(weight);
            }
        }

        private static float EvaluateRange(float value, float start, float full)
        {
            if (value <= start) return 0f;
            if (value >= full) return 1f;
            return Mathf.InverseLerp(start, full, value);
        }

        private static void CollectTargets(
            IStandardRigBinding rigBinding,
            StandardBlendshape semantic,
            List<BlendshapeTargetKey> destination)
        {
            BlendshapeTargetCollector.CollectAllMatches(rigBinding, semantic, destination);
        }

        private sealed class EyeBlendshapeTargets
        {
            public readonly List<BlendshapeTargetKey> Blink = new(4);
            public readonly List<BlendshapeTargetKey> LookDown = new(4);
            public readonly List<BlendshapeTargetKey> LookUp = new(4);
            public readonly List<BlendshapeTargetKey> Squint = new(4);
            public readonly List<BlendshapeTargetKey> Wide = new(4);
            public readonly List<BlendshapeTargetKey> UpperLidDown = new(4);
            public readonly List<BlendshapeTargetKey> UpperLidUp = new(4);
            public readonly List<BlendshapeTargetKey> LowerLidUp = new(4);

            public bool HasUpperLidUp => UpperLidUp.Count > 0;
            public bool HasLowerLidUp => LowerLidUp.Count > 0;

            public void Clear()
            {
                Blink.Clear();
                LookDown.Clear();
                LookUp.Clear();
                Squint.Clear();
                Wide.Clear();
                UpperLidDown.Clear();
                UpperLidUp.Clear();
                LowerLidUp.Clear();
            }
        }

        private struct EyelidFollowState
        {
            public float UpperLidDown;
            public float BlinkFallback;
            public float LookDown;
            public float LowerLidUp;
            public float UpperLidUp;
            public float Wide;
            public float LookUp;
            public float Squint;
        }
    }
}
