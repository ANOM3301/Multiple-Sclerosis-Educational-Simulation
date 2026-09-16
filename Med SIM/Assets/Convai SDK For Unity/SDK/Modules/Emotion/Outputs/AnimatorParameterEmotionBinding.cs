using System;
using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Taxonomy;
using Convai.Runtime.Animation;
using UnityEngine;

namespace Convai.Modules.Emotion.Outputs
{
    /// <summary>
    ///     Drives float animator parameters from composed emotion scores via the single-writer
    ///     <see cref="AnimatorConductor" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Ownership is registered during <see cref="Bind" />. Attempts to drive a
    ///         parameter already owned by another module are rejected and logged so the
    ///         author can resolve the conflict.
    ///     </para>
    /// </remarks>
    [Serializable]
    public sealed class AnimatorParameterEmotionBinding : IEmotionOutputBinding
    {
        [SerializeField, Tooltip("Per-emotion slots. Only slots with a non-empty AnimatorParameterName produce writes.")]
        private List<EmotionSlotBinding> slots = new();

        private readonly Dictionary<string, string> _labelToParameter = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _labelToMultiplier = new(StringComparer.OrdinalIgnoreCase);
        private AnimatorConductor _conductor;
        private UnityEngine.Object _owner;
        private bool _bound;

        public IReadOnlyList<EmotionSlotBinding> Slots => slots;

        /// <summary>
        ///     Replaces the authored slot list. Intended for preset factories and editor
        ///     tooling; prefer the inspector for one-off authoring.
        /// </summary>
        public void SetSlots(IReadOnlyList<EmotionSlotBinding> newSlots)
        {
            slots.Clear();
            if (newSlots == null) return;
            for (int i = 0; i < newSlots.Count; i++)
            {
                EmotionSlotBinding slot = newSlots[i];
                if (slot != null) slots.Add(slot);
            }
        }

        /// <summary>
        ///     Creates an ephemeral runtime copy that owns its own registration maps. This
        ///     prevents multiple characters from sharing mutable binding state through the
        ///     same authoring asset.
        /// </summary>
        public AnimatorParameterEmotionBinding CreateRuntimeCopy()
        {
            var copy = new AnimatorParameterEmotionBinding();
            if (slots == null) return copy;

            var clonedSlots = new List<EmotionSlotBinding>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                EmotionSlotBinding slot = slots[i];
                if (slot != null) clonedSlots.Add(slot.Clone());
            }

            copy.SetSlots(clonedSlots);
            return copy;
        }

        /// <inheritdoc />
        public void Bind(
            UnityEngine.Object owner,
            IEmotionTaxonomy taxonomy,
            IStandardRigBinding rig,
            AnimatorConductor conductor,
            FacialBlendshapeCompositorHost compositor)
        {
            Unbind(owner);

            _owner = owner;
            _conductor = conductor;
            if (_conductor == null || taxonomy == null || slots == null) return;

            for (int i = 0; i < slots.Count; i++)
            {
                EmotionSlotBinding slot = slots[i];
                if (slot == null) continue;
                if (string.IsNullOrWhiteSpace(slot.AnimatorParameterName)) continue;
                if (string.IsNullOrWhiteSpace(slot.EmotionLabel)) continue;
                if (!taxonomy.TryResolve(slot.EmotionLabel, out EmotionDescriptor descriptor)) continue;

                string parameter = slot.AnimatorParameterName.Trim();
                if (!_conductor.RegisterParameter(owner, parameter, AnimatorParameterType.Float))
                    continue;

                _labelToParameter[descriptor.Label] = parameter;
                _labelToMultiplier[descriptor.Label] = slot.WeightMultiplier;
            }

            _bound = _labelToParameter.Count > 0;
        }

        /// <inheritdoc />
        public void Apply(IReadOnlyDictionary<string, float> scores, float neutralAlternationFactor)
        {
            if (!_bound || _conductor == null || _owner == null || scores == null) return;

            float intensityAttenuation = 1f - Mathf.Clamp01(neutralAlternationFactor);

            foreach (KeyValuePair<string, string> kvp in _labelToParameter)
            {
                scores.TryGetValue(kvp.Key, out float score);
                float multiplier = _labelToMultiplier.TryGetValue(kvp.Key, out float m) ? m : 1f;
                float value = Mathf.Clamp01(score * multiplier * intensityAttenuation);
                _conductor.WriteFloat(_owner, kvp.Value, value);
            }
        }

        /// <inheritdoc />
        public void Unbind(UnityEngine.Object owner)
        {
            if (_conductor != null && _owner != null)
            {
                foreach (string parameter in _labelToParameter.Values)
                    _conductor.UnregisterParameter(_owner, parameter);
            }

            _labelToParameter.Clear();
            _labelToMultiplier.Clear();
            _bound = false;
            _conductor = null;
            _owner = null;
        }
    }
}
