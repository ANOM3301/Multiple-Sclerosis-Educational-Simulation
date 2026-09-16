using System.Collections.Generic;
using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.Emotion.Outputs;
using Convai.Modules.Emotion.Taxonomy;
using UnityEngine;

namespace Convai.Modules.Emotion.Profiles
{
    /// <summary>
    ///     Authoring asset bundling the taxonomy reference, smoothing parameters,
    ///     micro-expression timing, neutral alternation, and one concrete output binding per
    ///     supported target type.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The profile is the single authored surface for emotion behavior. Authors either
    ///         drop a blendshape binding in for face meshes, an animator binding in for
    ///         controller-driven setups, or both simultaneously. The controller discovers
    ///         whichever bindings are populated and skips the empty ones.
    ///     </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Convai/Embodiment/Emotion Profile",
        fileName = "ConvaiEmotionProfile")]
    public sealed class ConvaiEmotionProfile : ScriptableObject
    {
        [Header("Taxonomy")]
        [SerializeField, Tooltip("Emotion vocabulary. When unset, a default Plutchik taxonomy is used.")]
        private EmotionTaxonomyAsset taxonomy;

        [Header("Smoothing")]
        [SerializeField, Range(0.1f, 20f)]
        [Tooltip("Exponential smoothing speed for score interpolation (higher = snappier).")]
        private float lerpSpeed = 5f;

        [SerializeField, Range(0.1f, 20f)]
        [Tooltip("Decay speed applied when target scores are zero.")]
        private float decaySpeed = 2f;

        [SerializeField, Range(-0.25f, 0.25f)]
        [Tooltip("Bias added to server intensity values before normalization (1..3 -> 0..1).")]
        private float intensityOffset;

        [Header("Micro-Expression Burst")]
        [SerializeField]
        [Tooltip("When true, a short overshoot is applied when a new emotion enters above the threshold.")]
        private bool microBurstEnabled = true;

        [SerializeField, Range(0.05f, 1.5f)]
        [Tooltip("Duration of the overshoot envelope in seconds.")]
        private float microBurstDuration = 0.25f;

        [SerializeField, Range(1f, 3f)]
        [Tooltip("Peak overshoot multiplier applied at the envelope apex.")]
        private float microBurstOvershoot = 1.4f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum delta required to trigger a burst.")]
        private float microBurstThreshold = 0.15f;

        [Header("Neutral Alternation")]
        [SerializeField]
        [Tooltip("When true, the active emotion periodically fades toward neutral to avoid frozen faces.")]
        private bool neutralAlternationEnabled = true;

        [SerializeField, Range(0.5f, 30f)]
        [Tooltip("Minimum hold interval before alternating (seconds).")]
        private float alternationMinInterval = 3f;

        [SerializeField, Range(0.5f, 30f)]
        [Tooltip("Maximum hold interval before alternating (seconds).")]
        private float alternationMaxInterval = 7f;

        [SerializeField, Range(0.05f, 5f)]
        [Tooltip("Duration of each crossfade between emotion and neutral (seconds).")]
        private float alternationBlendDuration = 1f;

        [SerializeField]
        [Tooltip("When true, alternation only runs while the character is speaking.")]
        private bool alternateOnlyWhileTalking;

        [Header("Output Bindings")]
        [SerializeField, Tooltip("Blendshape output. Leave slots empty to disable.")]
        private BlendshapeEmotionBinding blendshapeBinding = new();

        [SerializeField, Tooltip("Animator parameter output. Leave slots empty to disable.")]
        private AnimatorParameterEmotionBinding animatorBinding = new();

        public EmotionTaxonomyAsset Taxonomy => taxonomy;
        public float LerpSpeed => lerpSpeed;
        public float DecaySpeed => decaySpeed;
        public float IntensityOffset => intensityOffset;

        public bool MicroBurstEnabled => microBurstEnabled;
        public float MicroBurstDuration => microBurstDuration;
        public float MicroBurstOvershoot => microBurstOvershoot;
        public float MicroBurstThreshold => microBurstThreshold;

        public bool NeutralAlternationEnabled => neutralAlternationEnabled;
        public float AlternationMinInterval => alternationMinInterval;
        public float AlternationMaxInterval => alternationMaxInterval;
        public float AlternationBlendDuration => alternationBlendDuration;
        public bool AlternateOnlyWhileTalking => alternateOnlyWhileTalking;

        public BlendshapeEmotionBinding BlendshapeBinding => blendshapeBinding;
        public AnimatorParameterEmotionBinding AnimatorBinding => animatorBinding;

        /// <summary>
        ///     Creates a character-scoped runtime copy of the authored blendshape binding.
        ///     Runtime state must never live on the shared profile asset itself.
        /// </summary>
        public BlendshapeEmotionBinding CreateBlendshapeRuntimeBinding() =>
            blendshapeBinding != null ? blendshapeBinding.CreateRuntimeCopy() : null;

        /// <summary>
        ///     Creates a character-scoped runtime copy of the authored animator binding.
        ///     Runtime state must never live on the shared profile asset itself.
        /// </summary>
        public AnimatorParameterEmotionBinding CreateAnimatorRuntimeBinding() =>
            animatorBinding != null ? animatorBinding.CreateRuntimeCopy() : null;

        /// <summary>
        ///     Resolves the taxonomy reference, synthesizing a default Plutchik taxonomy when
        ///     the author left the field empty.
        /// </summary>
        public EmotionTaxonomyAsset ResolveTaxonomyOrDefault(out bool synthesized)
        {
            if (taxonomy != null)
            {
                taxonomy.EnsureBuilt();
                synthesized = false;
                return taxonomy;
            }

            synthesized = true;
            return EmotionTaxonomyAsset.CreateDefault();
        }

        private void OnValidate()
        {
            lerpSpeed = Mathf.Max(0.1f, lerpSpeed);
            decaySpeed = Mathf.Max(0.1f, decaySpeed);
            intensityOffset = Mathf.Clamp(intensityOffset, -0.25f, 0.25f);
            microBurstDuration = Mathf.Max(0.05f, microBurstDuration);
            microBurstOvershoot = Mathf.Max(1f, microBurstOvershoot);
            microBurstThreshold = Mathf.Clamp01(microBurstThreshold);
            alternationMinInterval = Mathf.Max(0.5f, alternationMinInterval);
            alternationMaxInterval = Mathf.Max(alternationMinInterval, alternationMaxInterval);
            alternationBlendDuration = Mathf.Max(0.05f, alternationBlendDuration);
        }

        /// <summary>Creates a runtime-default profile instance (used when no asset is wired up).</summary>
        public static ConvaiEmotionProfile CreateDefault()
        {
            ConvaiEmotionProfile instance = CreateInstance<ConvaiEmotionProfile>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }

        /// <summary>
        ///     Creates a profile tuned for conversational character facial expression on a
        ///     <paramref name="rig" />: smoothing and micro-burst parameters favor readable
        ///     emotional transitions, and the blendshape output binding is pre-populated with
        ///     the slot list returned by
        ///     <see cref="Convai.Modules.Emotion.Authoring.RealisticEmotionSlots.Build" />
        ///     for the supplied rig convention.
        /// </summary>
        /// <param name="taxonomy">
        ///     Taxonomy asset the slot labels reference. Usually a Plutchik-style default.
        /// </param>
        /// <param name="rig">Target facial rig convention. Drives the concrete blendshape names.</param>
        public static ConvaiEmotionProfile CreateConversationalPreset(EmotionTaxonomyAsset taxonomy, RigConvention rig)
        {
            ConvaiEmotionProfile instance = CreateInstance<ConvaiEmotionProfile>();
            instance.taxonomy = taxonomy;
            instance.lerpSpeed = 4.5f;
            instance.decaySpeed = 1.8f;
            instance.intensityOffset = 0f;

            instance.microBurstEnabled = true;
            instance.microBurstDuration = 0.28f;
            instance.microBurstOvershoot = 1.35f;
            instance.microBurstThreshold = 0.18f;

            instance.neutralAlternationEnabled = true;
            instance.alternationMinInterval = 3.5f;
            instance.alternationMaxInterval = 8f;
            instance.alternationBlendDuration = 1.1f;
            instance.alternateOnlyWhileTalking = false;

            IReadOnlyList<EmotionSlotBinding> slots = Authoring.RealisticEmotionSlots.Build(rig);
            instance.blendshapeBinding = new BlendshapeEmotionBinding();
            instance.blendshapeBinding.SetSlots(slots);
            instance.animatorBinding = new AnimatorParameterEmotionBinding();
            instance.animatorBinding.SetSlots(BuildAnimatorSlots(slots));

            return instance;
        }

        /// <summary>
        ///     Builds an animator-parameter mirror of the supplied blendshape slot list. Each
        ///     emotion gets a single <c>Emotion_&lt;Label&gt;</c> float parameter; authors can
        ///     rewire the names in the inspector after creation. Mirrors the convention used
        ///     by the realistic preset library.
        /// </summary>
        private static List<EmotionSlotBinding> BuildAnimatorSlots(IReadOnlyList<EmotionSlotBinding> blendshapeSlots)
        {
            var result = new List<EmotionSlotBinding>();
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < blendshapeSlots.Count; i++)
            {
                EmotionSlotBinding source = blendshapeSlots[i];
                if (source == null) continue;
                if (!seen.Add(source.EmotionLabel)) continue;

                string parameter = "Emotion_" + Capitalize(source.EmotionLabel);
                result.Add(new EmotionSlotBinding(
                    emotionLabel: source.EmotionLabel,
                    animatorParameterName: parameter,
                    blendshapeNames: string.Empty,
                    weightMultiplier: source.WeightMultiplier,
                    fullBlendshapeWeight: 100f,
                    isMouthShape: false));
            }
            return result;
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (char.IsUpper(value[0])) return value;
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
