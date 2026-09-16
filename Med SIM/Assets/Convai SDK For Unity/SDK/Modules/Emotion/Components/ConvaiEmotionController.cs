using System;
using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Modules;
using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Taxonomy;
using Convai.Domain.DomainEvents.Runtime;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.Emotion;
using Convai.Domain.EventSystem;
using Convai.Domain.Logging;
using Convai.Modules.Emotion.Core;
using Convai.Modules.Emotion.Outputs;
using Convai.Modules.Emotion.Profiles;
using Convai.Modules.Emotion.Taxonomy;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using Convai.Runtime.Components;
using Convai.Runtime.Emotion;
using UnityEngine;

namespace Convai.Modules.Emotion.Components
{
    /// <summary>
    ///     MonoBehaviour front-end for the Emotion module. Consumes server emotion events,
    ///     smooths scores through an <see cref="EmotionScoreAccumulator" />, optionally applies
    ///     neutral alternation, and dispatches the composed reading to the registered output
    ///     bindings on the <see cref="ConvaiEmotionProfile" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Registers itself as the authoritative <see cref="IEmotionStateSource" /> for
    ///         the character, and when a blendshape binding is active, as the
    ///         <see cref="IEmotionMouthWeightProvider" /> for LipSync handoff.
    ///     </para>
    /// </remarks>
    [AddComponentMenu("Convai/Embodiment/Emotion Controller")]
    [DisallowMultipleComponent]
    public sealed class ConvaiEmotionController : EmbodimentProfileReceiver<ConvaiEmotionProfile>,
        IEmotionStateSource,
        IEmotionMouthWeightProvider,
        IEmbodimentTickable,
        IEmotionDetectionModeSource
    {
        [Header("Detection")]
        [SerializeField]
        [Tooltip("Whether this character requests emotion detection on connect, and which provider. " +
                 "Off sends no request, so no bot-emotion frames arrive (removing this component has the same effect). " +
                 "NRCLex and LLM select the detection provider. This Inspector setting is the source of truth and " +
                 "overrides any backend emotion configuration.")]
        private EmotionDetectionMode detectionMode = EmotionDetectionMode.Nrclex;

        [Header("Overrides")]
        [SerializeField]
        [Tooltip("When enabled, the pipeline ignores server events and holds the locked emotion.")]
        private bool lockEmotion;

        [SerializeField, Tooltip("Canonical taxonomy label applied when Lock Emotion is enabled.")]
        private string lockedEmotionLabel = "neutral";

        [SerializeField, Range(0f, 1f)]
        private float lockedIntensity = 1f;

        private ConvaiCharacter _character;
        private ConvaiEmotionProfile _effectiveProfile;
        private EmotionTaxonomyAsset _effectiveTaxonomy;
        private bool _createdSyntheticTaxonomy;

        private EmotionScoreAccumulator _accumulator;
        private NeutralAlternator _alternator;

        private readonly List<IEmotionOutputBinding> _activeBindings = new(2);
        private BlendshapeEmotionBinding _activeBlendshapeBinding;

        private readonly Dictionary<string, float> _currentScoresSnapshot =
            new(StringComparer.OrdinalIgnoreCase);

        private SubscriptionToken _emotionToken;
        private SubscriptionToken _speechToken;
        private SubscriptionToken _sessionToken;
        private IEventHub _subscribedEventHub;
        private bool _dependenciesChangedHandlerRegistered;
        private bool _rigBindingChangedHandlerRegistered;
        private bool _warnedAboutUnscopedEmotionEvent;

        private EmotionReading _currentReading = EmotionReading.Neutral;
        private string _lastDominantLabel;
        private float _dominantHoldSeconds;
        private bool _isCharacterSpeaking;

        /// <inheritdoc />
        public EmotionReading Current => _currentReading;

        /// <summary>
        ///     Emotion detection mode requested for this character. Resolved at connect time to
        ///     decide whether the backend runs detection and which provider it uses.
        /// </summary>
        public EmotionDetectionMode EmotionDetectionMode => detectionMode;

        /// <summary>Canonical emotion label after taxonomy resolution, smoothing, and profile composition.</summary>
        public string CurrentResolvedEmotion => _currentReading.DominantLabel;

        /// <summary>Composed normalized intensity [0, 1] for <see cref="CurrentResolvedEmotion" />.</summary>
        public float CurrentNormalizedIntensity => _currentReading.DominantScore;

        /// <inheritdoc />
        EmbodimentTickPhase IEmbodimentTickable.Phase => EmbodimentTickPhase.Cognition;

        /// <inheritdoc />
        protected override string ProfileModuleId => ModuleIds.Emotion;

        /// <inheritdoc />
        protected override System.Func<ConvaiEmotionProfile> DefaultProfileFactory => ConvaiEmotionProfile.CreateDefault;

        /// <inheritdoc />
        protected override void OnProfileApplied(ConvaiEmotionProfile newProfile)
        {
            _effectiveProfile = null;

            if (!isActiveAndEnabled) return;
            RebuildPipeline();
        }

        /// <inheritdoc />
        public bool TryGetMouthWeight(BlendshapeTargetKey key, out float weight)
        {
            if (_activeBlendshapeBinding != null)
                return _activeBlendshapeBinding.TryGetMouthWeight(key, out weight);
            weight = 0f;
            return false;
        }

        protected override void Awake()
        {
            base.Awake();
            _character = GetComponentInParent<ConvaiCharacter>(true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!enabled) return;

            BuildPipeline();
            Context.DependenciesPopulated += HandleDependenciesPopulated;
            _dependenciesChangedHandlerRegistered = true;
            Context.RigBindingChanged += HandleRigBindingChanged;
            _rigBindingChangedHandlerRegistered = true;
            SubscribeToEventHub();

            Context.RegisterEmotionStateSource(this);
            // Always register; TryGetMouthWeight returns false when no blendshape binding is
            // active so the controller is observable but contributes nothing.
            Context.RegisterEmotionMouthProvider(this);

            Context.EnsureTickScheduler()?.Register(this);
        }

        protected override void OnDisable()
        {
            if (_dependenciesChangedHandlerRegistered && Context != null)
            {
                Context.DependenciesPopulated -= HandleDependenciesPopulated;
                _dependenciesChangedHandlerRegistered = false;
            }

            if (_rigBindingChangedHandlerRegistered && Context != null)
            {
                Context.RigBindingChanged -= HandleRigBindingChanged;
                _rigBindingChangedHandlerRegistered = false;
            }

            UnsubscribeFromEventHub();
            Context?.UnregisterEmotionStateSource(this);
            Context?.UnregisterEmotionMouthProvider(this);
            Context?.TickScheduler?.Unregister(this);

            TeardownPipeline();

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ReleaseSyntheticAssets();
            base.OnDestroy();
        }

        void IEmbodimentTickable.EmbodimentTick(float deltaTime)
        {
            if (_accumulator == null) return;

            if (lockEmotion)
            {
                _accumulator.SetImmediateEmotion(lockedEmotionLabel, lockedIntensity);
            }
            else
            {
                _accumulator.Tick(deltaTime);
                _alternator?.Tick(deltaTime);
            }

            _accumulator.GetDominant(out string dominantLabel, out float dominantScore);
            UpdateDominantHold(dominantLabel, deltaTime);

            CopyOutputScoresSnapshot();
            float alternationFactor = _alternator?.Factor ?? 0f;

            for (int i = 0; i < _activeBindings.Count; i++)
                _activeBindings[i].Apply(_currentScoresSnapshot, alternationFactor);

            float mouthInfluence = ResolveMouthInfluence(dominantLabel, dominantScore, alternationFactor);
            _currentReading = new EmotionReading(
                dominantLabel,
                dominantScore,
                _currentScoresSnapshot,
                mouthInfluence,
                _dominantHoldSeconds);
        }

        /// <summary>
        ///     Sets an explicit override emotion, bypassing server events until
        ///     <see cref="ClearEmotionOverride" /> is called.
        /// </summary>
        public void SetEmotionOverride(string label, float score)
        {
            if (_accumulator == null) return;
            _accumulator.SetTargetEmotion(label, score);
        }

        /// <summary>Clears any override and restores neutral state until the next server event.</summary>
        public void ClearEmotionOverride()
        {
            if (_accumulator == null) return;
            _accumulator.SetTargetEmotion(_effectiveTaxonomy?.Neutral?.Label ?? EmotionReading.NeutralLabel, 0f);
        }

        /// <summary>Locks the character to a specific emotion until <see cref="UnlockEmotion" /> is called.</summary>
        public void LockEmotion(string label, float intensity = 1f)
        {
            lockEmotion = true;
            lockedEmotionLabel = label;
            lockedIntensity = Mathf.Clamp01(intensity);
            _accumulator?.SetImmediateEmotion(lockedEmotionLabel, lockedIntensity);
        }

        /// <summary>Releases a previous <see cref="LockEmotion" /> call.</summary>
        public void UnlockEmotion()
        {
            lockEmotion = false;
        }

        private void BuildPipeline()
        {
            ReleaseSyntheticTaxonomyIfOwned();
            _effectiveProfile = ResolveProfile();
            if (_effectiveProfile == null) return; // Awake not called yet (ExecuteAlways race)
            _effectiveTaxonomy = _effectiveProfile.ResolveTaxonomyOrDefault(out _createdSyntheticTaxonomy);

            _accumulator = new EmotionScoreAccumulator(_effectiveTaxonomy,
                _effectiveProfile.LerpSpeed, _effectiveProfile.DecaySpeed);
            _accumulator.ConfigureMicroBurst(
                _effectiveProfile.MicroBurstEnabled,
                _effectiveProfile.MicroBurstDuration,
                _effectiveProfile.MicroBurstOvershoot,
                _effectiveProfile.MicroBurstThreshold);

            if (_effectiveProfile.NeutralAlternationEnabled)
            {
                _alternator = new NeutralAlternator(
                _effectiveProfile.AlternationMinInterval,
                _effectiveProfile.AlternationMaxInterval,
                _effectiveProfile.AlternationBlendDuration,
                _effectiveProfile.AlternateOnlyWhileTalking,
                DeterministicEmbodimentRandom.CreateSeed(this, 0xE4010A11u));
            }

            RebuildOutputBindings();
        }

        private void RebuildOutputBindings()
        {
            for (int i = 0; i < _activeBindings.Count; i++)
                _activeBindings[i].Unbind(this);

            _activeBindings.Clear();
            _activeBlendshapeBinding = null;

            BlendshapeEmotionBinding blendshape = _effectiveProfile.CreateBlendshapeRuntimeBinding();
            if (blendshape != null && HasAnyAuthoredSlot(blendshape.Slots, blendshapeMode: true))
            {
                IStandardRigBinding rigBinding = Context?.EnsureRigBinding();
                FacialBlendshapeCompositorHost compositor = Context?.EnsureCompositor();
                blendshape.Bind(this, _effectiveTaxonomy,
                    rigBinding, null, compositor);
                _activeBindings.Add(blendshape);
                _activeBlendshapeBinding = blendshape;
            }

            AnimatorParameterEmotionBinding animator = _effectiveProfile.CreateAnimatorRuntimeBinding();
            if (animator != null && HasAnyAuthoredSlot(animator.Slots, blendshapeMode: false))
            {
                AnimatorConductor conductor = Context?.EnsureAnimatorConductor();
                animator.Bind(this, _effectiveTaxonomy,
                    null, conductor, null);
                _activeBindings.Add(animator);
            }
        }

        private void TeardownPipeline()
        {
            for (int i = 0; i < _activeBindings.Count; i++)
                _activeBindings[i].Unbind(this);

            _activeBindings.Clear();
            _activeBlendshapeBinding = null;
            _accumulator?.Reset();
            _accumulator = null;
            _alternator?.Reset();
            _alternator = null;
            _currentReading = EmotionReading.Neutral;
            _currentScoresSnapshot.Clear();
            _dominantHoldSeconds = 0f;
            _lastDominantLabel = null;
        }

        private void SubscribeToEventHub()
        {
            IEventHub hub = Context?.EventHub;
            if (ReferenceEquals(_subscribedEventHub, hub)) return;

            UnsubscribeFromEventHub();
            if (hub == null) return;

            _emotionToken = hub.Subscribe<CharacterEmotionChanged>(OnEmotionChanged);
            _speechToken = hub.Subscribe<CharacterSpeechStateChanged>(OnSpeechStateChanged);
            _sessionToken = hub.Subscribe<SessionStateChanged>(OnSessionStateChanged);
            _subscribedEventHub = hub;
        }

        private void UnsubscribeFromEventHub()
        {
            IEventHub hub = _subscribedEventHub;
            if (hub == null) return;

            if (_emotionToken != default) hub.Unsubscribe(_emotionToken);
            if (_speechToken != default) hub.Unsubscribe(_speechToken);
            if (_sessionToken != default) hub.Unsubscribe(_sessionToken);

            _emotionToken = default;
            _speechToken = default;
            _sessionToken = default;
            _subscribedEventHub = null;
        }

        private void OnEmotionChanged(CharacterEmotionChanged evt)
        {
            if (_accumulator == null || _effectiveTaxonomy == null) return;
            if (!MatchesCharacter(evt.CharacterId)) return;
            if (lockEmotion) return;

            if (!_effectiveTaxonomy.TryResolve(evt.Emotion, out EmotionDescriptor descriptor))
            {
                Debug.LogWarning(
                    $"[ConvaiEmotionController] Unknown backend emotion label '{evt.Emotion}'. Falling back to neutral. Add it as a taxonomy alias if this label is expected.",
                    this);
                descriptor = _effectiveTaxonomy.Neutral;
            }

            float normalized = Mathf.Clamp01(
                evt.NormalizedIntensity + (_effectiveProfile != null ? _effectiveProfile.IntensityOffset : 0f));

            if (descriptor.IsNeutral)
                _accumulator.SetTargetEmotion(descriptor.Label, 0f);
            else
                _accumulator.SetTargetEmotion(descriptor.Label, normalized);
        }

        private void OnSpeechStateChanged(CharacterSpeechStateChanged evt)
        {
            if (!MatchesCharacter(evt.CharacterId)) return;
            _isCharacterSpeaking = evt.IsSpeaking;
            _alternator?.SetTalkingState(evt.IsSpeaking);
        }

        private void OnSessionStateChanged(SessionStateChanged evt)
        {
            if (evt.NewState != SessionState.Disconnected && evt.NewState != SessionState.Error) return;
            if (lockEmotion) return;

            _accumulator?.Reset();
        }

        private bool MatchesCharacter(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                WarnUnscopedEmotionEventOnce();
                return false;
            }

            if (_character == null || string.IsNullOrWhiteSpace(_character.CharacterId)) return false;
            return string.Equals(_character.CharacterId, characterId, StringComparison.OrdinalIgnoreCase);
        }

        private void WarnUnscopedEmotionEventOnce()
        {
            if (_warnedAboutUnscopedEmotionEvent) return;
            _warnedAboutUnscopedEmotionEvent = true;
            Context?.Logger.WithTag(nameof(ConvaiEmotionController))?.Warning(
                "Ignored an emotion event without a character id. " +
                "Character-scoped emotion events must include a character id to avoid cross-character expression bleed.");
        }

        private void HandleDependenciesPopulated()
        {
            SubscribeToEventHub();
            Context?.EnsureTickScheduler()?.Register(this);
            RebuildPipeline();
        }

        private void HandleRigBindingChanged(IStandardRigBinding rigBinding)
        {
            if (_effectiveProfile == null || _effectiveTaxonomy == null) return;

            // Mouth provider registration is permanent for the controller's lifetime; the
            // current TryGetMouthWeight result tracks _activeBlendshapeBinding directly.
            RebuildOutputBindings();
        }

        private void RebuildPipeline()
        {
            TeardownPipeline();
            BuildPipeline();
        }

        private ConvaiEmotionProfile ResolveProfile()
        {
            _effectiveProfile = EffectiveProfile;
            return _effectiveProfile;
        }

        private void ReleaseSyntheticAssets()
        {
            ReleaseSyntheticTaxonomyIfOwned();
        }

        private void ReleaseSyntheticTaxonomyIfOwned()
        {
            if (!_createdSyntheticTaxonomy || _effectiveTaxonomy == null) return;

            // Destroy() is forbidden in EditMode (e.g. EditMode tests with [ExecuteAlways]).
            if (UnityEngine.Application.isPlaying)
                Destroy(_effectiveTaxonomy);
            else
                DestroyImmediate(_effectiveTaxonomy);

            _effectiveTaxonomy = null;
            _createdSyntheticTaxonomy = false;
        }



        private void CopyOutputScoresSnapshot()
        {
            _currentScoresSnapshot.Clear();
            IReadOnlyDictionary<string, float> source = _accumulator.OutputScores;
            foreach (KeyValuePair<string, float> kvp in source)
                _currentScoresSnapshot[kvp.Key] = kvp.Value;
        }

        private void UpdateDominantHold(string dominantLabel, float deltaTime)
        {
            if (!string.Equals(dominantLabel, _lastDominantLabel, StringComparison.OrdinalIgnoreCase))
            {
                _lastDominantLabel = dominantLabel;
                _dominantHoldSeconds = 0f;
            }
            else
            {
                _dominantHoldSeconds += deltaTime;
            }
        }

        private float ResolveMouthInfluence(string dominantLabel, float dominantScore, float alternationFactor)
        {
            if (_effectiveTaxonomy == null || string.IsNullOrEmpty(dominantLabel)) return 0f;
            if (!_effectiveTaxonomy.TryResolve(dominantLabel, out EmotionDescriptor descriptor)) return 0f;

            float base_ = descriptor.DefaultMouthInfluence * dominantScore;
            return Mathf.Clamp01(base_ * (1f - alternationFactor));
        }

        private static bool HasAnyAuthoredSlot(
            IReadOnlyList<EmotionSlotBinding> slots,
            bool blendshapeMode)
        {
            if (slots == null) return false;
            for (int i = 0; i < slots.Count; i++)
            {
                EmotionSlotBinding slot = slots[i];
                if (slot == null) continue;
                if (string.IsNullOrWhiteSpace(slot.EmotionLabel)) continue;

                bool hasPayload = blendshapeMode
                    ? !string.IsNullOrWhiteSpace(slot.BlendshapeNames)
                    : !string.IsNullOrWhiteSpace(slot.AnimatorParameterName);
                if (hasPayload) return true;
            }
            return false;
        }
    }
}
