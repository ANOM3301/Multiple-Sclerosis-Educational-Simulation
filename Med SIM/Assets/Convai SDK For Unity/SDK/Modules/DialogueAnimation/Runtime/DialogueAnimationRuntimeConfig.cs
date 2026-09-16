using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime
{
    /// <summary>
    ///     Tunable timing, layer weights, and selector coefficients consumed by
    ///     <c>ConvaiDialogueAnimationController</c>. Exposed as a ScriptableObject so multiple
    ///     characters can share identical feel without duplicating inspector values.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The config is intentionally separate from
    ///         <see cref="Core.DialogueAnimationLibrary" />: a library is content (which
    ///         clips are available), whereas a config is behavior (timing, layer weights,
    ///         selection bias). Splitting them lets designers swap one without touching the other.
    ///     </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "DialogueAnimationRuntimeConfig",
        menuName = "Convai/Embodiment/Dialogue Animation Runtime Config",
        order = 121)]
    public sealed class DialogueAnimationRuntimeConfig : ScriptableObject
    {
        [Header("Animator layer weights")]
        [Tooltip("Layer weight written each tick to the base (foundation idle) layer index on the controller.")]
        [Range(0f, 1f)]
        [SerializeField] private float _baseLayerWeight = 1f;

        [Tooltip("Layer weight written each tick to the idle overlay layer when not in Speaking/Reacting.")]
        [Range(0f, 1f)]
        [SerializeField] private float _idleOverlayLayerWeight = 1f;

        [Tooltip("Idle overlay layer weight while in Speaking or Reacting (e.g. lower so talk layers read clearer).")]
        [Range(0f, 1f)]
        [SerializeField] private float _idleOverlayLayerWeightWhileSpeaking = 1f;

        [Tooltip("Seconds to ease the idle overlay layer weight when switching between idle and speaking targets.")]
        [Range(0.1f, 4f)]
        [SerializeField] private float _idleOverlayWeightBlendSeconds = 0.95f;

        [Header("Idle Rotation")]
        [Tooltip("Minimum time in seconds an idle variant is held before becoming eligible " +
                 "for rotation.")]
        [Range(2f, 30f)]
        [SerializeField] private float _idleMinHoldSeconds = 8f;

        [Tooltip("Maximum time in seconds an idle variant is held. Beyond this the controller " +
                 "always rotates to a new variant on the next tick.")]
        [Range(3f, 60f)]
        [SerializeField] private float _idleMaxHoldSeconds = 20f;

        [Tooltip("Default idle-to-idle crossfade duration in seconds. Applied when the " +
                 "library and clip entries leave the crossfade unspecified.")]
        [Range(0.15f, 3f)]
        [SerializeField] private float _idleCrossFadeDuration = 0.95f;

        [Tooltip("When enabled, idle overlay variants keep rotating on schedule while the " +
                 "character is speaking. Talk motion uses the dedicated body talk layer, so idle " +
                 "and talk masks stay independent. When disabled, idle rotation pauses during speech.")]
        [SerializeField] private bool _rotateIdleOverlayWhileSpeaking;

        [Header("Body talk layer")]
        [Tooltip("Fade-in to peak weight when body talk is active.")]
        [Range(0.1f, 4f)]
        [SerializeField] private float _bodyTalkLayerFadeInSeconds = 0.95f;

        [Tooltip("Fade-out from peak when body talk ends.")]
        [Range(0.1f, 4f)]
        [SerializeField] private float _bodyTalkLayerFadeOutSeconds = 1.05f;

        [Tooltip("Peak weight while speaking when the body talk layer is used (0..1).")]
        [Range(0f, 1f)]
        [SerializeField] private float _bodyTalkLayerPeakWeight = 1f;

        [Header("Head talk layer")]
        [Tooltip("Fade-in to peak weight when head talk is active.")]
        [Range(0.1f, 4f)]
        [SerializeField] private float _headTalkLayerFadeInSeconds = 0.95f;

        [Tooltip("Fade-out from peak when head talk ends.")]
        [Range(0.1f, 4f)]
        [SerializeField] private float _headTalkLayerFadeOutSeconds = 1.05f;

        [Tooltip("Peak weight while speaking when the head talk layer is used (0..1).")]
        [Range(0f, 1f)]
        [SerializeField] private float _headTalkLayerPeakWeight = 1f;

        [Header("Talk crossfades")]
        [Tooltip("Default crossfade duration in seconds between talk variants when the " +
                 "character picks a new one mid-turn.")]
        [Range(0.15f, 3f)]
        [SerializeField] private float _talkCrossFadeDuration = 0.75f;

        [Header("Speech Energy")]
        [Tooltip("When enabled, the live LipSync blendshape signal modulates talk-layer weight.")]
        [SerializeField] private bool _useLipSyncSpeechEnergy = true;

        [Tooltip("Window length in seconds used when computing windowed RMS speech energy.")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float _speechEnergyWindowSeconds = 0.08f;

        [Tooltip("Scalar gain applied to raw LipSync blendshape energy.")]
        [Range(0.1f, 10f)]
        [SerializeField] private float _speechEnergyGain = 1.25f;

        [Tooltip("Lowest talk-layer scale while Speaking, even when speech energy is low.")]
        [Range(0f, 1f)]
        [SerializeField] private float _speechEnergyMinimumTalkLayerScale = 0.65f;

        [Tooltip(
            "When LipSync speech energy drives talk layers: default on so talk-layer scaling does not " +
            "depend on a facial compositor / IDialoguePhaseProvider. Disable to also take " +
            "IDialoguePhaseProvider.SpeechBlendFactor into account when a compositor adapter is present.")]
        [SerializeField] private bool _ignoreFacialDialoguePhaseForTalkLayerScale = true;

        [Header("Selection")]
        [Tooltip("Strength of the emotion-bias bonus applied by the selector. 0 disables " +
                 "bias (pure weight-based draws); higher values make emotion-tagged clips " +
                 "dominate when the character's mood matches.")]
        [Range(0f, 5f)]
        [SerializeField] private float _emotionBiasStrength = 1.5f;

        [Tooltip("Starting seed for the deterministic selection stream. The controller " +
                 "advances this internally; expose it here for test reproducibility.")]
        [SerializeField] private uint _deterministicSeed = 0xC0B1AEu;

        [Tooltip("Reserved for test tooling. Runtime selection uses a per-character " +
                 "deterministic stream seeded from DeterministicSeed and the character hierarchy.")]
        [SerializeField] private bool _deterministicSelectionForTests;

        [Header("Idle blend safety")]
        [Tooltip("When enabled, idle overlay rotations wait until the active looping clip " +
                 "is near its cycle wrap (or until the grace period elapses). Reduces swapping " +
                 "mid-stroke on looping idles. Non-looping clips ignore the phase gate.")]
        [SerializeField] private bool _idleBlendGateNearLoopWrap = true;

        [Tooltip("For looping clips, normalized-time window at the start AND end of each " +
                 "cycle where a rotation is allowed (e.g. 0.12 = first and last 12%).")]
        [Range(0.02f, 0.45f)]
        [SerializeField] private float _idleLoopWrapWindowFraction = 0.12f;

        [Tooltip("After the scheduled rotation time plus this many seconds, allow a " +
                 "rotation even outside the wrap window so playback cannot stall.")]
        [Range(0.5f, 30f)]
        [SerializeField] private float _idleBlendGateGraceSeconds = 4f;

        /// <summary>Weight applied to the base layer each tick.</summary>
        public float BaseLayerWeight => Mathf.Clamp01(_baseLayerWeight);

        /// <summary>Weight applied to the idle overlay layer when not talking.</summary>
        public float IdleOverlayLayerWeight => Mathf.Clamp01(_idleOverlayLayerWeight);

        /// <summary>Weight applied to the idle overlay layer during Speaking / Reacting.</summary>
        public float IdleOverlayLayerWeightWhileSpeaking =>
            Mathf.Clamp01(_idleOverlayLayerWeightWhileSpeaking);

        /// <summary>Duration in seconds to ease idle overlay layer weight between idle and speaking targets.</summary>
        public float IdleOverlayWeightBlendSeconds =>
            Mathf.Clamp(_idleOverlayWeightBlendSeconds, 0.05f, 4f);

        /// <summary>Minimum idle hold, clamped to the serialized range.</summary>
        public float IdleMinHoldSeconds => Mathf.Clamp(_idleMinHoldSeconds, 2f, 30f);

        /// <summary>Maximum idle hold, always &gt;= <see cref="IdleMinHoldSeconds" />.</summary>
        public float IdleMaxHoldSeconds => Mathf.Max(IdleMinHoldSeconds + 0.5f, _idleMaxHoldSeconds);

        /// <summary>Default idle-to-idle crossfade duration in seconds.</summary>
        public float IdleCrossFadeDuration => Mathf.Clamp(_idleCrossFadeDuration, 0.05f, 3f);

        /// <summary>
        ///     When <c>true</c>, idle overlay rotation continues during speaking/reacting.
        /// </summary>
        public bool RotateIdleOverlayWhileSpeaking => _rotateIdleOverlayWhileSpeaking;

        /// <summary>Head talk layer fade-in duration in seconds.</summary>
        public float HeadTalkLayerFadeInSeconds =>
            Mathf.Clamp(_headTalkLayerFadeInSeconds, 0.05f, 4f);

        /// <summary>Head talk layer fade-out duration in seconds.</summary>
        public float HeadTalkLayerFadeOutSeconds =>
            Mathf.Clamp(_headTalkLayerFadeOutSeconds, 0.05f, 4f);

        /// <summary>Body talk layer fade-in duration in seconds.</summary>
        public float BodyTalkLayerFadeInSeconds =>
            Mathf.Clamp(_bodyTalkLayerFadeInSeconds, 0.05f, 4f);

        /// <summary>Body talk layer fade-out duration in seconds.</summary>
        public float BodyTalkLayerFadeOutSeconds =>
            Mathf.Clamp(_bodyTalkLayerFadeOutSeconds, 0.05f, 4f);

        /// <summary>Default crossfade between talk variants.</summary>
        public float TalkCrossFadeDuration => Mathf.Clamp(_talkCrossFadeDuration, 0.05f, 3f);

        /// <summary>Whether LipSync-derived speech energy modulates talk-layer weight.</summary>
        public bool UseLipSyncSpeechEnergy => _useLipSyncSpeechEnergy;

        /// <summary>Windowed RMS window length for LipSync-derived speech energy.</summary>
        public float SpeechEnergyWindowSeconds => Mathf.Clamp(_speechEnergyWindowSeconds, 0.02f, 0.5f);

        /// <summary>Gain applied to raw LipSync blendshape energy before clamping.</summary>
        public float SpeechEnergyGain => Mathf.Max(0.01f, _speechEnergyGain);

        /// <summary>Minimum talk-layer scale while Speaking / Reacting.</summary>
        public float SpeechEnergyMinimumTalkLayerScale => Mathf.Clamp01(_speechEnergyMinimumTalkLayerScale);

        /// <summary>
        ///     When <c>true</c>, talk-layer speech scaling uses only <see cref="ISpeechEnergyProvider" />
        ///     and does not incorporate <see cref="IDialoguePhaseProvider.SpeechBlendFactor" /> from
        ///     the facial compositor path.
        /// </summary>
        public bool IgnoreFacialDialoguePhaseForTalkLayerScale => _ignoreFacialDialoguePhaseForTalkLayerScale;

        /// <summary>Peak weight for the head talk layer while speaking (0 = never blend in).</summary>
        public float HeadTalkLayerPeakWeight => Mathf.Clamp01(_headTalkLayerPeakWeight);

        /// <summary>Peak weight for the body talk layer while speaking (0 = never blend in).</summary>
        public float BodyTalkLayerPeakWeight => Mathf.Clamp01(_bodyTalkLayerPeakWeight);

        /// <summary>Emotion bias strength for the selector.</summary>
        public float EmotionBiasStrength => Mathf.Max(0f, _emotionBiasStrength);

        /// <summary>Starting seed for deterministic clip rotation.</summary>
        public uint DeterministicSeed => _deterministicSeed == 0u ? 0xC0B1AEu : _deterministicSeed;

        /// <summary>
        ///     Reserved for test tooling. Variant selection is reproducible per character
        ///     regardless of this flag.
        /// </summary>
        public bool DeterministicSelectionForTests => _deterministicSelectionForTests;

        /// <summary>When true, idle rotations prefer loop wrap windows on looping clips.</summary>
        public bool IdleBlendGateNearLoopWrap => _idleBlendGateNearLoopWrap;

        /// <summary>Normalized fraction at each end of a loop where rotation is allowed.</summary>
        public float IdleLoopWrapWindowFraction =>
            Mathf.Clamp(_idleLoopWrapWindowFraction, 0.02f, 0.45f);

        /// <summary>Seconds after the scheduled rotation before forcing outside the wrap window.</summary>
        public float IdleBlendGateGraceSeconds =>
            Mathf.Clamp(_idleBlendGateGraceSeconds, 0.5f, 30f);

        private void OnValidate()
        {
            if (_idleMaxHoldSeconds < _idleMinHoldSeconds + 0.5f)
                _idleMaxHoldSeconds = _idleMinHoldSeconds + 0.5f;
        }
    }
}
