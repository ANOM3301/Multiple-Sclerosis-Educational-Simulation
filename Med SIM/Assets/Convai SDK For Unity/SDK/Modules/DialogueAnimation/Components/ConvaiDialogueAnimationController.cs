using Convai.Domain.Embodiment.Modules;
using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Semantics;
using Convai.Domain.Logging;
using Convai.Modules.DialogueAnimation.Core;
using Convai.Modules.DialogueAnimation.Profiles;
using Convai.Modules.DialogueAnimation.Runtime;
using Convai.Modules.DialogueAnimation.Runtime.Driver;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using Convai.Runtime.Logging;
using UnityEngine;
using ContractDefaults = Convai.Modules.DialogueAnimation.Runtime.DialogueAnimatorContract.Defaults;

namespace Convai.Modules.DialogueAnimation.Components
{
    /// <summary>
    ///     Production dialogue animation controller. Owns the four-layer animator stack
    ///     (full-body base idle, masked idle overlay, masked body talk, head talk) and
    ///     delegates per-frame logic to the <see cref="DialogueAnimationOrchestrator" />
    ///     and <see cref="DialogueAnimatorWriter" /> so this component stays a thin
    ///     composition root: serialized configuration, lifecycle wiring, and runtime
    ///     build/teardown only.
    /// </summary>
    [AddComponentMenu("Convai/Embodiment/Dialogue Animation")]
    [DisallowMultipleComponent]
    public sealed partial class ConvaiDialogueAnimationController :
        EmbodimentProfileReceiver<ConvaiDialogueAnimationProfile>,
        IEmbodimentTickable
    {
        [Header("Content")]
        [SerializeField] private DialogueAnimationLibrary _library;
        [SerializeField] private DialogueAnimationRuntimeConfig _config;
        [SerializeField] private CharacterGender _characterGender = CharacterGender.Neutral;

        [Header("Animator Wiring")]
        [SerializeField] private Animator _animatorOverride;

        [SerializeField]
        [Tooltip("Optional contract asset. When assigned, it owns layer indices, state names, and placeholder names.")]
        private DialogueAnimatorContract _contract;

        [SerializeField, Min(0)] private int _baseIdleLayerIndex;

        [Tooltip("Idle overlay layer (ping-pong idle pool). Avatar mask is optional and owned by the Animator Controller.")]
        [SerializeField, Min(0)] private int _idleOverlayLayerIndex = 1;

        [Tooltip("Body talk layer (talk clips for body/gesture motion). Avatar mask is optional and owned by the Animator Controller.")]
        [SerializeField, Min(0)] private int _bodyTalkLayerIndex = 2;

        [Tooltip("Head talk layer (head-only or combined with body talk, per library entry).")]
        [SerializeField, Min(0)] private int _headTalkLayerIndex = 3;

        [Header("Animator State Names")]
        [SerializeField] private string _baseIdleStateName = ContractDefaults.BaseIdleStateName;
        [SerializeField] private string _idleOverlayStateA = ContractDefaults.IdleOverlayStateA;
        [SerializeField] private string _idleOverlayStateB = ContractDefaults.IdleOverlayStateB;
        [SerializeField] private string _bodyTalkStateA = ContractDefaults.BodyTalkStateA;
        [SerializeField] private string _bodyTalkStateB = ContractDefaults.BodyTalkStateB;
        [SerializeField] private string _headTalkStateA = ContractDefaults.HeadTalkStateA;
        [SerializeField] private string _headTalkStateB = ContractDefaults.HeadTalkStateB;

        [Header("Placeholder Clip Names")]
        [SerializeField] private string _basePlaceholderName = ContractDefaults.BasePlaceholderName;
        [SerializeField] private string _idleOverlayPlaceholderA = ContractDefaults.IdleOverlayPlaceholderA;
        [SerializeField] private string _idleOverlayPlaceholderB = ContractDefaults.IdleOverlayPlaceholderB;
        [SerializeField] private string _bodyTalkPlaceholderA = ContractDefaults.BodyTalkPlaceholderA;
        [SerializeField] private string _bodyTalkPlaceholderB = ContractDefaults.BodyTalkPlaceholderB;
        [SerializeField] private string _headTalkPlaceholderA = ContractDefaults.HeadTalkPlaceholderA;
        [SerializeField] private string _headTalkPlaceholderB = ContractDefaults.HeadTalkPlaceholderB;

        [Header("Base Idle Content")]
        [SerializeField] private AnimationClip _foundationIdleClip;

        [Header("Runtime Dependencies")]
        [SerializeField]
        [Tooltip("When enabled, the controller adds the hidden conversation-flow driver at runtime when no dialogue state source exists.")]
        private bool _autoCreateConversationFlow = true;

        private readonly DialogueOverrideControllerOwnership _overrideOwnership = new();
        private DialogueAnimationOrchestrator _orchestrator;
        private DialogueAnimatorWriter _writer;
        private Animator _animator;
        private DialogueAnimatorContractView _contractView;
        private AnimatorSlotOverrider _slotOverrider;
        private AnimationClip _resolvedFoundationClip;
        private AnimatorConductor _conductor;

        private bool _runtimeBuilt;
        private bool _tickRegistered;
        private bool _dependenciesHooked;
        private bool _speechEnergyProviderHooked;

        EmbodimentTickPhase IEmbodimentTickable.Phase => EmbodimentTickPhase.Expression;

        public DialogueAnimationLibrary Library => _library;
        public DialogueAnimationRuntimeConfig Config => _config;
        public DialogueAnimatorContract Contract => _contract;
        public CharacterGender CharacterGender => _characterGender;
        public bool HasValidIdleLibrary => _library != null && _library.HasAnyValidIdle();

        public int LastIdleIndex => _orchestrator?.LastIdleIndex ?? -1;
        public int LastTalkIndex => _orchestrator?.ActiveTalk.LibraryIndex ?? -1;

        public AnimationClip CurrentFoundationIdleClip => _resolvedFoundationClip;
        public AnimationClip CurrentIdleOverlayClip => _orchestrator?.CurrentIdleOverlayClip;
        public AnimationClip CurrentBodyTalkClip => _orchestrator?.CurrentBodyTalkClip;
        public AnimationClip CurrentTalkClip => _orchestrator?.ActiveTalk.Clip;

        public float CurrentHeadTalkLayerWeight => _writer?.CurrentHeadTalkLayerWeight ?? 0f;
        public float CurrentBodyTalkLayerWeight => _writer?.CurrentBodyTalkLayerWeight ?? 0f;
        public float CurrentBaseIdleLayerWeight => _writer?.CurrentBaseIdleLayerWeight ?? 1f;
        public float CurrentIdleOverlayLayerWeight => _writer?.CurrentIdleOverlayLayerWeight ?? 0f;

        /// <summary>Strongest talk-layer contribution for HUDs.</summary>
        public float CurrentTalkLayerWeight =>
            Mathf.Max(CurrentHeadTalkLayerWeight, CurrentBodyTalkLayerWeight);

        public int RuntimeBaseIdleLayerIndex => _contractView.BaseIdleLayerIndex;
        public int RuntimeIdleOverlayLayerIndex => _contractView.IdleOverlayLayerIndex;
        public int RuntimeBodyTalkLayerIndex => _contractView.BodyTalkLayerIndex;
        public int RuntimeHeadTalkLayerIndex => _contractView.HeadTalkLayerIndex;

        public void SetLibrary(DialogueAnimationLibrary library)
        {
            _library = library;
            _orchestrator?.Configure(_library, _config, _characterGender);
        }

        public void SetConfig(DialogueAnimationRuntimeConfig config)
        {
            _config = config;
            _orchestrator?.Configure(_library, _config, _characterGender);
        }

        protected override string ProfileModuleId => ModuleIds.DialogueAnimation;

        protected override System.Func<ConvaiDialogueAnimationProfile> DefaultProfileFactory =>
            ConvaiDialogueAnimationProfile.CreateDefault;

        protected override void OnProfileApplied(ConvaiDialogueAnimationProfile newProfile)
        {
            if (newProfile != null)
                ApplyProfileValues(newProfile);
            else
                ResetProfileValues();

            _orchestrator?.Configure(_library, _config, _characterGender);
            _orchestrator?.ResolveSpeechEnergy(Context);

            if (!UnityEngine.Application.isPlaying || Context == null || !isActiveAndEnabled) return;
            TeardownRuntime();
            BuildRuntime();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!enabled) return;

            if (profile != null)
                ApplyProfileValues(profile);

            HookContextEvents();
            BuildRuntime();
        }

        protected override void OnDisable()
        {
            TeardownRuntime();
            UnhookContextEvents();
            base.OnDisable();
        }

        void IEmbodimentTickable.EmbodimentTick(float deltaTime)
        {
            if (!_runtimeBuilt) return;

            DialogueStateReading state =
                Context?.ConversationFlowSource?.Current ?? DialogueStateReading.Idle;
            EmotionReading emotion =
                Context?.EmotionStateSource?.Current ?? EmotionReading.Neutral;

            DialogueAnimationInputs inputs = new(state.Primary, in emotion, deltaTime);
            DialogueAnimationCommands commands = _orchestrator.Tick(in inputs, Context, HasValidIdleLibrary);
            _writer.Apply(in commands, deltaTime, this, _config);
        }

        private void BuildRuntime()
        {
            if (!UnityEngine.Application.isPlaying) return;
            if (_runtimeBuilt) return;

            EnsureConversationFlowSource();
            if (!ResolveAnimator()) return;

            _contractView = ResolveContractView();
            DialogueAnimatorLayerSet layers = _contractView.ToLayerSet();

            if (!DialogueRuntimeBuilder.TryBuild(
                    _animator,
                    in _contractView,
                    in layers,
                    _foundationIdleClip,
                    _library,
                    out DialogueRuntimeArtifacts artifacts,
                    out string warning))
            {
                if (!string.IsNullOrEmpty(warning))
                {
                    ConvaiLogger.Warning(
                        $"'{name}' {warning}",
                        LogCategory.Character);
                }
                return;
            }

            _slotOverrider = artifacts.SlotOverrider;
            _overrideOwnership.Install(_animator, artifacts.OverrideController);

            if (!ApplyFoundationBaseIdle(artifacts.FoundationClip))
            {
                TeardownRuntime();
                return;
            }

            DialogueState initialPrimary =
                Context?.ConversationFlowSource?.Current.Primary ?? DialogueState.Idle;
            bool initialSpeaking = DialogueAnimationStateMachine.IsTalkingState(initialPrimary);

            _orchestrator ??= new DialogueAnimationOrchestrator(name);
            _orchestrator.Configure(_library, _config, _characterGender);
            _orchestrator.BindRuntimeArtifacts(
                _animator,
                artifacts.IdleOverlayPingPong,
                artifacts.HeadTalkPingPong,
                artifacts.BodyTalkPingPong,
                _contractView.IdleOverlayLayerIndex);
            _orchestrator.Initialize(initialPrimary, ResolveSeed(), Context);

            _conductor = Context?.EnsureAnimatorConductor();
            _writer ??= new DialogueAnimatorWriter();
            _writer.Bind(_animator, _conductor, in layers, this, initialSpeaking, _config, HasValidIdleLibrary);
            _writer.WriteZeroedTalkWeights(this);

            EmbodimentTickScheduler scheduler = Context?.EnsureTickScheduler();
            if (scheduler != null)
            {
                scheduler.Register(this);
                _tickRegistered = true;
            }

            _runtimeBuilt = true;

            if (HasValidIdleLibrary)
                _orchestrator.TryPlayInitialIdle();
        }

        private void TeardownRuntime()
        {
            if (_tickRegistered)
            {
                Context?.TickScheduler?.Unregister(this);
                _tickRegistered = false;
            }

            _writer?.Unbind(this);
            _orchestrator?.Reset();
            _resolvedFoundationClip = null;
            _overrideOwnership.RestoreAndDestroyIfOwned();
            _slotOverrider = null;
            _conductor = null;
            _runtimeBuilt = false;
        }

        private bool ResolveAnimator()
        {
            if (_animator != null) return true;

            _animator = _animatorOverride != null
                ? _animatorOverride
                : GetComponentInChildren<Animator>(true);

            return _animator != null;
        }

        private void EnsureConversationFlowSource()
        {
            if (!_autoCreateConversationFlow) return;
            if (Context == null || Context.ConversationFlowSource != null) return;
            if (!UnityEngine.Application.isPlaying) return;

            Context.MarkConversationFlowDriverDemanded();
            Context.TryEnsureConversationFlowSource();
        }

        private uint ResolveSeed()
        {
            uint baseSeed = _config != null ? _config.DeterministicSeed : 0xC0B1AEu;
            unchecked
            {
                uint mixed = DeterministicEmbodimentRandom.CreateSeed(this, baseSeed);
                return mixed != 0u ? mixed : baseSeed;
            }
        }

        private DialogueAnimatorContractView ResolveContractView() =>
            DialogueAnimatorContractView.Resolve(
                _contract,
                _baseIdleLayerIndex,
                _idleOverlayLayerIndex,
                _bodyTalkLayerIndex,
                _headTalkLayerIndex,
                _baseIdleStateName,
                _idleOverlayStateA,
                _idleOverlayStateB,
                _bodyTalkStateA,
                _bodyTalkStateB,
                _headTalkStateA,
                _headTalkStateB,
                _basePlaceholderName,
                _idleOverlayPlaceholderA,
                _idleOverlayPlaceholderB,
                _bodyTalkPlaceholderA,
                _bodyTalkPlaceholderB,
                _headTalkPlaceholderA,
                _headTalkPlaceholderB);

        private bool ApplyFoundationBaseIdle(AnimationClip clip)
        {
            if (!_slotOverrider.SetOverride(_contractView.BasePlaceholderName, clip))
            {
                ConvaiLogger.Warning(
                    $"'{name}' could not override base placeholder.",
                    LogCategory.Character);
                return false;
            }

            _resolvedFoundationClip = clip;
            _slotOverrider.ApplyPending();

            int stateHash = Animator.StringToHash(_contractView.BaseIdleStateName);
            _animator.Play(stateHash, _contractView.BaseIdleLayerIndex, 0f);
            return true;
        }

        private void HookContextEvents()
        {
            if (Context == null) return;

            if (!_dependenciesHooked)
            {
                Context.DependenciesPopulated += HandleDependenciesPopulated;
                _dependenciesHooked = true;
            }
            if (!_speechEnergyProviderHooked)
            {
                Context.SpeechEnergyProviderChanged += HandleSpeechEnergyProviderChanged;
                _speechEnergyProviderHooked = true;
            }
        }

        private void UnhookContextEvents()
        {
            if (Context == null) return;

            if (_dependenciesHooked)
            {
                Context.DependenciesPopulated -= HandleDependenciesPopulated;
                _dependenciesHooked = false;
            }
            if (_speechEnergyProviderHooked)
            {
                Context.SpeechEnergyProviderChanged -= HandleSpeechEnergyProviderChanged;
                _speechEnergyProviderHooked = false;
            }
        }

        private void HandleDependenciesPopulated()
        {
            if (!_runtimeBuilt)
            {
                BuildRuntime();
                return;
            }

            if (_conductor == null)
            {
                _conductor = Context?.EnsureAnimatorConductor();
                _writer?.RebindConductor(_animator, _conductor, this);
            }

            if (!_tickRegistered)
            {
                EmbodimentTickScheduler scheduler = Context?.EnsureTickScheduler();
                if (scheduler != null)
                {
                    scheduler.Register(this);
                    _tickRegistered = true;
                }
            }
        }

        private void HandleSpeechEnergyProviderChanged(Convai.Runtime.Animation.ISpeechEnergyProvider provider) =>
            _orchestrator?.ResolveSpeechEnergy(Context, provider);

        private void ApplyProfileValues(ConvaiDialogueAnimationProfile profile)
        {
            _library = profile.Library;
            _config = profile.RuntimeConfig;
            _contract = profile.AnimatorContract;
            _foundationIdleClip = profile.FoundationIdleClip;
            _characterGender = profile.CharacterGender;
            _autoCreateConversationFlow = profile.AutoCreateConversationFlow;
        }

        private void ResetProfileValues()
        {
            _library = null;
            _config = null;
            _contract = null;
            _foundationIdleClip = null;
            _characterGender = CharacterGender.Neutral;
            _autoCreateConversationFlow = true;
        }
    }
}
