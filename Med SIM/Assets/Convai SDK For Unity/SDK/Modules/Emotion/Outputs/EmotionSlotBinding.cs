using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convai.Modules.Emotion.Outputs
{
    /// <summary>
    ///     Authoring entry that maps a taxonomy emotion label to a set of output names
    ///     (blendshape names or animator parameter names) with per-slot tuning.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Shared between blendshape and animator bindings. The same slot list can drive
    ///         both simultaneously: the animator binding reads
    ///         <see cref="AnimatorParameterName" /> and the blendshape binding reads
    ///         <see cref="BlendshapeNames" />.
    ///     </para>
    /// </remarks>
    [Serializable]
    public sealed class EmotionSlotBinding
    {
        [SerializeField, Tooltip("Canonical taxonomy label this slot drives. Must match a label in the linked taxonomy.")]
        private string emotionLabel;

        [SerializeField, Tooltip("Animator parameter (float) written when the emotion is active. Leave empty to skip animator output.")]
        private string animatorParameterName;

        [SerializeField, Tooltip("Comma- or newline-separated blendshape names to drive. Leave empty to skip blendshape output.")]
        [TextArea(1, 3)]
        private string blendshapeNames;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Per-slot multiplier applied on top of the emotion score.")]
        private float weightMultiplier = 1f;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Full-intensity blendshape weight (mesh units, 0..100). Final value = score * multiplier * fullWeight.")]
        private float fullBlendshapeWeight = 100f;

        [SerializeField]
        [Tooltip("When true, the slot is treated as a mouth-region output and routes to the compositor's Mouth layer.")]
        private bool isMouthShape;

        public string EmotionLabel => emotionLabel;
        public string AnimatorParameterName => animatorParameterName;
        public string BlendshapeNames => blendshapeNames;
        public float WeightMultiplier => weightMultiplier;
        public float FullBlendshapeWeight => fullBlendshapeWeight;
        public bool IsMouthShape => isMouthShape;

        public EmotionSlotBinding() { }

        /// <summary>
        ///     Construct a slot programmatically. Intended for editor tooling and preset
        ///     factories; runtime authoring should still prefer the inspector.
        /// </summary>
        public EmotionSlotBinding(
            string emotionLabel,
            string animatorParameterName,
            string blendshapeNames,
            float weightMultiplier,
            float fullBlendshapeWeight,
            bool isMouthShape)
        {
            this.emotionLabel = emotionLabel ?? string.Empty;
            this.animatorParameterName = animatorParameterName ?? string.Empty;
            this.blendshapeNames = blendshapeNames ?? string.Empty;
            this.weightMultiplier = Mathf.Clamp(weightMultiplier, 0f, 2f);
            this.fullBlendshapeWeight = Mathf.Clamp(fullBlendshapeWeight, 0f, 100f);
            this.isMouthShape = isMouthShape;
        }

        /// <summary>
        ///     Parses <see cref="BlendshapeNames" /> into a trimmed, de-duplicated list.
        /// </summary>
        public void FillBlendshapeNames(List<string> destination)
        {
            if (destination == null) return;
            destination.Clear();
            if (string.IsNullOrWhiteSpace(blendshapeNames)) return;

            string[] parts = blendshapeNames.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim();
                if (trimmed.Length == 0) continue;
                if (!seen.Add(trimmed)) continue;
                destination.Add(trimmed);
            }
        }

        /// <summary>Creates a value-copy so runtime bindings never mutate shared authoring state.</summary>
        public EmotionSlotBinding Clone() => new(
            emotionLabel,
            animatorParameterName,
            blendshapeNames,
            weightMultiplier,
            fullBlendshapeWeight,
            isMouthShape);
    }
}
