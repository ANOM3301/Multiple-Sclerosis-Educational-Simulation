using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convai.Modules.FacialAnimation.Profiles
{
    /// <summary>
    ///     Baked facial animation asset authored or imported from an
    ///     <see cref="AnimationClip" />. Consumed by <c>ConvaiBakedFacialClipSource</c> at runtime
    ///     and written into the <c>FacialClipGeneral</c> / <c>FacialClipMouth</c> layers of
    ///     the shared facial compositor.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Convai/Embodiment/Facial Animation Profile",
        fileName = "ConvaiFacialAnimationProfile")]
    public sealed class ConvaiFacialAnimationProfile : ScriptableObject
    {
        public enum GlobalWeightMode
        {
            MultiplyGlobal = 0,
            BypassGlobal = 1
        }

        public readonly struct BindingWeightSettings
        {
            public BindingWeightSettings(float multiplier, GlobalWeightMode globalWeightMode)
            {
                Multiplier = Mathf.Max(0f, multiplier);
                Mode = globalWeightMode;
            }

            public float Multiplier { get; }
            public GlobalWeightMode Mode { get; }

            public float ResolveScale(float profileScale, float sourceGlobalScale)
            {
                float baseScale = Mathf.Max(0f, profileScale) * Multiplier;
                return Mode == GlobalWeightMode.BypassGlobal
                    ? baseScale
                    : baseScale * Mathf.Max(0f, sourceGlobalScale);
            }
        }

        [Serializable]
        public sealed class CurveBinding
        {
            [Tooltip("Name of the blendshape on the skinned mesh to drive.")]
            [SerializeField] private string blendshapeName;

            [Tooltip("When true, the curve is composed against the facial clip mouth layer (subject to lip-sync crossfade). Otherwise the general layer.")]
            [SerializeField] private bool isMouth;

            [Tooltip("Baked source curve in clip seconds mapped directly to blendshape weight [0,100].")]
            [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 0f);

            public string BlendshapeName => blendshapeName;
            public bool IsMouth => isMouth;
            public AnimationCurve Curve => curve;

            public CurveBinding() { }

            public CurveBinding(string blendshapeName, bool isMouth, AnimationCurve curve)
            {
                this.blendshapeName = blendshapeName ?? string.Empty;
                this.isMouth = isMouth;
                this.curve = CloneCurve(curve);
            }
        }

        [Serializable]
        public sealed class WeightRule
        {
            [Tooltip("Human-readable label for this rule.")]
            [SerializeField] private string label = "Rule";

            [Tooltip("When disabled, this rule is skipped.")]
            [SerializeField] private bool enabled = true;

            [Tooltip("Semicolon-separated blendshape name patterns. First matching enabled rule wins.")]
            [SerializeField] private string patterns = string.Empty;

            [Tooltip("Additional multiplier applied to matching blendshape source weights.")]
            [SerializeField, Min(0f)] private float multiplier = 1f;

            [Tooltip("Controls whether this rule is affected by ConvaiBakedFacialClipSource Global Weight.")]
            [SerializeField] private GlobalWeightMode globalWeightMode = GlobalWeightMode.MultiplyGlobal;

            public string Label => label;
            public bool Enabled => enabled;
            public string Patterns => patterns;
            public float Multiplier => multiplier;
            public GlobalWeightMode GlobalWeightMode => globalWeightMode;

            public WeightRule() { }

            public WeightRule(
                string label,
                string patterns,
                float multiplier,
                GlobalWeightMode globalWeightMode)
            {
                this.label = label ?? string.Empty;
                this.enabled = true;
                this.patterns = patterns ?? string.Empty;
                this.multiplier = Mathf.Max(0f, multiplier);
                this.globalWeightMode = globalWeightMode;
            }

            public bool Matches(string blendshapeName)
            {
                if (!enabled || string.IsNullOrWhiteSpace(blendshapeName) || string.IsNullOrWhiteSpace(patterns))
                    return false;

                string[] parts = patterns.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string pattern = parts[i].Trim();
                    if (string.IsNullOrEmpty(pattern))
                        continue;

                    if (blendshapeName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                return false;
            }

            public BindingWeightSettings ToSettings() =>
                new(multiplier, globalWeightMode);
        }

        [SerializeField, Min(0.01f)]
        [Tooltip("Total clip duration in seconds.")]
        private float durationSeconds = 1f;

        [SerializeField]
        [Tooltip("Whether the clip loops past its duration.")]
        private bool loop = true;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Percent scale applied to baked source weights. 100 preserves source AnimationClip values.")]
        private float weightMultiplier = 100f;

        [SerializeField]
        private List<CurveBinding> bindings = new();

        [SerializeField]
        [Tooltip("Ordered blendshape weight rules. First matching enabled rule wins. Use Bypass Global for blinks or eye motion that must keep source strength while the rest of the clip is faded.")]
        private List<WeightRule> weightRules = CreateDefaultWeightRules();

        public float DurationSeconds => durationSeconds;
        public bool Loop => loop;
        public float WeightMultiplier => weightMultiplier;
        public IReadOnlyList<CurveBinding> Bindings => bindings;
        public IReadOnlyList<WeightRule> WeightRules => weightRules;

        public float WrapTime(float time)
        {
            if (durationSeconds <= 0f) return 0f;
            return loop ? Mathf.Repeat(time, durationSeconds) : Mathf.Clamp(time, 0f, durationSeconds);
        }

        /// <summary>
        ///     Replaces the binding list. Intended for editor/import tooling.
        /// </summary>
        public void SetBindings(IReadOnlyList<CurveBinding> newBindings, float duration, bool isLooping)
        {
            bindings.Clear();
            if (newBindings != null)
            {
                for (int i = 0; i < newBindings.Count; i++)
                {
                    CurveBinding binding = newBindings[i];
                    if (binding == null || string.IsNullOrWhiteSpace(binding.BlendshapeName))
                        continue;

                    bindings.Add(new CurveBinding(
                        binding.BlendshapeName,
                        binding.IsMouth,
                        binding.Curve));
                }
            }
            durationSeconds = Mathf.Max(0.01f, duration);
            loop = isLooping;
        }

        public BindingWeightSettings ResolveWeightSettings(string blendshapeName)
        {
            if (weightRules != null)
            {
                for (int i = 0; i < weightRules.Count; i++)
                {
                    WeightRule rule = weightRules[i];
                    if (rule != null && rule.Matches(blendshapeName))
                        return rule.ToSettings();
                }
            }

            return new BindingWeightSettings(1f, GlobalWeightMode.MultiplyGlobal);
        }

        private void OnEnable()
        {
            EnsureDefaultWeightRules();
        }

        private void OnValidate()
        {
            EnsureDefaultWeightRules();
        }

        private void EnsureDefaultWeightRules()
        {
            if (weightRules == null)
                weightRules = new List<WeightRule>();
            if (weightRules.Count > 0)
                return;

            weightRules.AddRange(CreateDefaultWeightRules());
        }

        private static AnimationCurve CloneCurve(AnimationCurve source)
        {
            if (source == null)
                return AnimationCurve.Linear(0f, 0f, 1f, 0f);

            return new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
        }

        private static List<WeightRule> CreateDefaultWeightRules() => new()
        {
            new WeightRule(
                "Preserve Eye Blink",
                "Eye_Blink;Blink",
                1f,
                GlobalWeightMode.BypassGlobal),
            new WeightRule(
                "Preserve Eye Look",
                "Eye_Look",
                1f,
                GlobalWeightMode.BypassGlobal)
        };
    }
}
