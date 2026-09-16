using Convai.Domain.Embodiment.Semantics;
using Convai.Domain.Logging;
using Convai.Modules.DialogueAnimation.Runtime;
using Convai.Modules.DialogueAnimation.Runtime.Driver;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using Convai.Runtime.Logging;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Stateful dialogue animation brain. Folds per-frame
    ///     <see cref="DialogueAnimationInputs" /> through an internal state machine, drives the
    ///     talk-variant starter on talk transitions, runs the idle rotation scheduler, samples
    ///     speech energy, and emits a <see cref="DialogueAnimationCommands" /> payload for the
    ///     <see cref="DialogueAnimatorWriter" /> to apply.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The orchestrator owns no animator references; the controller binds the runtime
    ///         artifacts (ping-pongs, override controller) once during <c>BuildRuntime</c> via
    ///         <see cref="BindRuntimeArtifacts" /> and they stay valid until <see cref="Reset" />.
    ///     </para>
    /// </remarks>
    internal sealed class DialogueAnimationOrchestrator
    {
        private readonly DialogueAnimationStateMachine _stateMachine = new();
        private readonly DialogueAnimationClipPicker _picker = new();
        private readonly DialogueIdleRotationScheduler _idleScheduler = new();
        private readonly DialogueSpeechEnergyResolver _speechEnergyResolver = new();
        private readonly string _ownerName;

        private DialogueAnimationLibrary _library;
        private DialogueAnimationRuntimeConfig _config;
        private CharacterGender _gender = CharacterGender.Neutral;
        private IDialogueVariantSelector _selector;
        private DeterministicEmbodimentRandom _rng;

        private AnimatorStatePingPong _idleOverlayPingPong;
        private AnimatorStatePingPong _headTalkPingPong;
        private AnimatorStatePingPong _bodyTalkPingPong;
        private int _lastIdleIndex = -1;

        public DialogueAnimationOrchestrator(string ownerName)
        {
            _ownerName = ownerName;
        }

        /// <summary>Idle clip picker exposed for diagnostics + initial-idle helpers.</summary>
        public DialogueAnimationClipPicker Picker => _picker;

        /// <summary>Talk session currently driving the talk layers (sentinel when none).</summary>
        public ref readonly DialogueTalkSession ActiveTalk => ref _stateMachine.ActiveTalk;

        /// <summary>Library index of the most recent idle pick (sentinel <c>-1</c> when none).</summary>
        public int LastIdleIndex => _lastIdleIndex;

        /// <summary>Clip currently bound to the idle overlay ping-pong (or <c>null</c>).</summary>
        public AnimationClip CurrentIdleOverlayClip => _idleOverlayPingPong?.CurrentClip;

        /// <summary>Clip currently bound to the body-talk ping-pong (or <c>null</c>).</summary>
        public AnimationClip CurrentBodyTalkClip => _bodyTalkPingPong?.CurrentClip;

        /// <summary>Clip currently bound to the head-talk ping-pong (or <c>null</c>).</summary>
        public AnimationClip CurrentHeadTalkClip => _headTalkPingPong?.CurrentClip;

        /// <summary>Currently resolved speech energy provider (never <c>null</c>).</summary>
        public ISpeechEnergyProvider SpeechEnergyProvider => _speechEnergyResolver.Provider;

        /// <summary>Replaces the authored content + gender used for variant selection.</summary>
        public void Configure(
            DialogueAnimationLibrary library,
            DialogueAnimationRuntimeConfig config,
            CharacterGender gender)
        {
            _library = library;
            _config = config;
            _gender = gender;
            _lastIdleIndex = -1;
            _picker.ResetPickIndices();
        }

        /// <summary>Wires the animator artifacts produced by <see cref="DialogueRuntimeBuilder" />.</summary>
        public void BindRuntimeArtifacts(
            Animator animator,
            AnimatorStatePingPong idleOverlayPingPong,
            AnimatorStatePingPong headTalkPingPong,
            AnimatorStatePingPong bodyTalkPingPong,
            int idleOverlayLayerIndex)
        {
            _idleOverlayPingPong = idleOverlayPingPong;
            _headTalkPingPong = headTalkPingPong;
            _bodyTalkPingPong = bodyTalkPingPong;
            _idleScheduler.Bind(animator, idleOverlayPingPong, idleOverlayLayerIndex);
        }

        /// <summary>
        ///     Seeds the orchestrator for a fresh <c>BuildRuntime</c>: resets the state machine,
        ///     RNG, idle-rotation hold window, and speech-energy resolver. The selector defaults
        ///     to <see cref="EmotionWeightedRandomSelector" />.
        /// </summary>
        public void Initialize(
            DialogueState initialPrimary,
            uint seed,
            EmbodimentContext context)
        {
            _selector = new EmotionWeightedRandomSelector();
            _stateMachine.Reset(initialPrimary);
            _rng = new DeterministicEmbodimentRandom(seed);
            _lastIdleIndex = -1;
            _idleScheduler.StartHoldWindow(ref _rng, _config);
            _speechEnergyResolver.Resolve(_config, context);
        }

        /// <summary>
        ///     Plays the initial idle clip immediately (zero fade) without consuming a rotation
        ///     slot. Returns whether a clip was actually crossfaded onto the idle overlay layer.
        /// </summary>
        public bool TryPlayInitialIdle()
        {
            if (_library == null || !_library.HasAnyValidIdle()) return false;
            if (_idleOverlayPingPong == null) return false;
            return _idleScheduler.PlayInitialIdle(
                _library,
                _picker,
                _selector,
                _config,
                _gender,
                ref _rng,
                ref _lastIdleIndex);
        }

        /// <summary>Re-resolves the speech energy provider from <paramref name="context" />.</summary>
        public void ResolveSpeechEnergy(EmbodimentContext context, ISpeechEnergyProvider explicitProvider = null) =>
            _speechEnergyResolver.Resolve(_config, context, explicitProvider);

        /// <summary>
        ///     Folds a single frame: advances state machine, kicks talk variants on
        ///     <c>StartTalk</c>, restarts the idle hold window on <c>StopTalk</c>, runs the idle
        ///     scheduler when the gating policy allows, and produces the
        ///     <see cref="DialogueAnimationCommands" /> payload for the writer.
        /// </summary>
        public DialogueAnimationCommands Tick(
            in DialogueAnimationInputs inputs,
            EmbodimentContext context,
            bool hasValidIdleLibrary)
        {
            _speechEnergyResolver.Sample(inputs.DeltaTime);

            DialogueAnimationTransition transition = _stateMachine.Process(inputs.Primary);
            if (transition == DialogueAnimationTransition.StartTalk)
                StartTalkVariant(in inputs);
            else if (transition == DialogueAnimationTransition.StopTalk)
                _idleScheduler.StartHoldWindow(ref _rng, _config);

            bool speaking = DialogueAnimationStateMachine.IsTalkingState(inputs.Primary);
            float speechLayerScale = speaking
                ? _speechEnergyResolver.ComputeLayerScale(_config, context)
                : 1f;

            bool rotateIdleThisFrame = !speaking
                || (_config != null && _config.RotateIdleOverlayWhileSpeaking);
            if (rotateIdleThisFrame)
            {
                _idleScheduler.Tick(
                    inputs.DeltaTime,
                    in inputs.Emotion,
                    _library,
                    _picker,
                    _selector,
                    _config,
                    _gender,
                    ref _rng,
                    ref _lastIdleIndex);
            }

            return new DialogueAnimationCommands(
                _stateMachine.ActiveTalk,
                speaking,
                speechLayerScale,
                hasValidIdleLibrary);
        }

        /// <summary>Tears down per-build state (artifacts, scheduler bindings, speech provider).</summary>
        public void Reset()
        {
            _idleScheduler.Reset();
            _speechEnergyResolver.ResetToNull();
            _stateMachine.Reset();
            _stateMachine.ClearActiveTalk();
            _idleOverlayPingPong = null;
            _headTalkPingPong = null;
            _bodyTalkPingPong = null;
            _lastIdleIndex = -1;
            _selector = null;
        }

        private void StartTalkVariant(in DialogueAnimationInputs inputs)
        {
            DialogueTalkSession session = DialogueTalkVariantStarter.Start(
                _library,
                _picker,
                _headTalkPingPong,
                _bodyTalkPingPong,
                _config,
                _selector,
                _gender,
                ref _rng,
                in inputs.Emotion);

            _stateMachine.SetActiveTalk(session);

            if (_library != null && _library.HasAnyValidTalk() && !session.IsActive)
            {
                ConvaiLogger.Warning(
                    $"'{_ownerName}' could not start any talk variant " +
                    "(check Talk pool coverage: need head-eligible and/or BodyOnly entries).",
                    LogCategory.Character);
            }
        }
    }
}
