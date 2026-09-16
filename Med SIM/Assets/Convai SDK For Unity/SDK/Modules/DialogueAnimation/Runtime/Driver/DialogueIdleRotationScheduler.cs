using System.Collections.Generic;
using Convai.Domain.Embodiment.Readings;
using Convai.Modules.DialogueAnimation.Core;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Tracks the idle overlay hold timer and rotates the idle overlay ping-pong to a
    ///     fresh variant when the configured hold window elapses (and, for looping clips, the
    ///     animator is near the cycle wrap or the grace window has expired).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The scheduler owns its scratch list for
    ///         <see cref="Animator.GetCurrentAnimatorClipInfo(int, List{AnimatorClipInfo})" />
    ///         to avoid per-frame allocations. Bind the animator and ping-pong once in
    ///         <c>BuildRuntime</c>; tick every frame; reset on teardown.
    ///     </para>
    /// </remarks>
    internal sealed class DialogueIdleRotationScheduler
    {
        private const float DefaultHoldSeconds = 10f;
        private const float DefaultLoopWrapWindowFraction = 0.12f;

        private readonly List<AnimatorClipInfo> _clipInfoScratch = new(4);

        private Animator _animator;
        private AnimatorStatePingPong _idleOverlayPingPong;
        private int _idleOverlayLayerIndex;

        private float _holdTimer;
        private float _nextRotationAt;

        public void Bind(
            Animator animator,
            AnimatorStatePingPong idleOverlayPingPong,
            int idleOverlayLayerIndex)
        {
            _animator = animator;
            _idleOverlayPingPong = idleOverlayPingPong;
            _idleOverlayLayerIndex = idleOverlayLayerIndex;
        }

        public void Reset()
        {
            _animator = null;
            _idleOverlayPingPong = null;
            _idleOverlayLayerIndex = 0;
            _holdTimer = 0f;
            _nextRotationAt = 0f;
        }

        /// <summary>
        ///     Resets the hold timer and samples a fresh next-rotation time. Call when the
        ///     conversation phase enters a window where idle rotation should restart (e.g. the
        ///     character stops talking).
        /// </summary>
        public void StartHoldWindow(
            ref DeterministicEmbodimentRandom rng,
            DialogueAnimationRuntimeConfig config)
        {
            _holdTimer = 0f;
            _nextRotationAt = SampleNextRotation(ref rng, config);
        }

        /// <summary>
        ///     Plays the initial idle clip immediately (zero fade) without consuming a
        ///     rotation slot. Used by <c>BuildRuntime</c> on initial setup.
        /// </summary>
        public bool PlayInitialIdle(
            DialogueAnimationLibrary library,
            DialogueAnimationClipPicker picker,
            IDialogueVariantSelector selector,
            DialogueAnimationRuntimeConfig config,
            CharacterGender characterGender,
            ref DeterministicEmbodimentRandom rng,
            ref int lastIdleIndex)
        {
            if (library == null || !library.HasAnyValidIdle()) return false;

            EmotionReading neutral = EmotionReading.Neutral;
            if (!picker.TryPickIdle(
                    library.IdleEntries,
                    lastIdleIndex,
                    in neutral,
                    selector,
                    config,
                    characterGender,
                    ref rng,
                    out DialogueClipEntry entry,
                    out int index))
            {
                return false;
            }

            if (!_idleOverlayPingPong.CrossFadeTo(entry.Clip, fadeSeconds: 0f, normalizedOffset: 0f))
                return false;

            lastIdleIndex = index;
            return true;
        }

        /// <summary>
        ///     Advances the hold timer; if the configured rotation window elapsed (and the
        ///     phase gate allows), picks a new idle variant from the library and crossfades
        ///     to it. Returns <c>true</c> when a rotation actually fired.
        /// </summary>
        public bool Tick(
            float deltaTime,
            in EmotionReading emotion,
            DialogueAnimationLibrary library,
            DialogueAnimationClipPicker picker,
            IDialogueVariantSelector selector,
            DialogueAnimationRuntimeConfig config,
            CharacterGender characterGender,
            ref DeterministicEmbodimentRandom rng,
            ref int lastIdleIndex)
        {
            if (library == null || !library.HasAnyValidIdle()) return false;

            _holdTimer += deltaTime;
            if (_holdTimer < _nextRotationAt) return false;

            bool bypassPhaseGate = config == null
                || !config.IdleBlendGateNearLoopWrap
                || _holdTimer >= _nextRotationAt + config.IdleBlendGateGraceSeconds;

            if (!bypassPhaseGate && !CanRotateAtPhase(config))
                return false;

            if (!picker.TryPickIdle(
                    library.IdleEntries,
                    lastIdleIndex,
                    in emotion,
                    selector,
                    config,
                    characterGender,
                    ref rng,
                    out DialogueClipEntry entry,
                    out int index))
            {
                return false;
            }

            if (entry.Clip == null) return false;

            float fade = ResolveFade(entry, config?.IdleCrossFadeDuration ?? library.DefaultCrossFadeDuration);
            if (!_idleOverlayPingPong.CrossFadeTo(entry.Clip, fade))
                return false;

            lastIdleIndex = index;
            _holdTimer = 0f;
            _nextRotationAt = SampleNextRotation(ref rng, config);
            return true;
        }

        private bool CanRotateAtPhase(DialogueAnimationRuntimeConfig config)
        {
            if (_animator == null) return true;
            if (_idleOverlayLayerIndex < 0 || _idleOverlayLayerIndex >= _animator.layerCount) return true;

            if (_animator.IsInTransition(_idleOverlayLayerIndex))
                return false;

            _clipInfoScratch.Clear();
            _animator.GetCurrentAnimatorClipInfo(_idleOverlayLayerIndex, _clipInfoScratch);
            if (_clipInfoScratch.Count == 0 || _clipInfoScratch[0].clip == null)
                return true;

            AnimationClip playing = _clipInfoScratch[0].clip;
            if (!playing.isLooping)
                return true;

            AnimatorStateInfo st = _animator.GetCurrentAnimatorStateInfo(_idleOverlayLayerIndex);
            float n = st.normalizedTime % 1f;
            if (n < 0f) n = 0f;

            float w = config != null ? config.IdleLoopWrapWindowFraction : DefaultLoopWrapWindowFraction;
            return n <= w || n >= 1f - w;
        }

        private static float SampleNextRotation(
            ref DeterministicEmbodimentRandom rng,
            DialogueAnimationRuntimeConfig config)
        {
            if (config == null) return DefaultHoldSeconds;
            return Mathf.Lerp(config.IdleMinHoldSeconds, config.IdleMaxHoldSeconds, rng.Value);
        }

        private static float ResolveFade(in DialogueClipEntry entry, float fallback) =>
            entry.CrossFadeDurationOverride > 0f
                ? entry.CrossFadeDurationOverride
                : Mathf.Max(0f, fallback);
    }
}
