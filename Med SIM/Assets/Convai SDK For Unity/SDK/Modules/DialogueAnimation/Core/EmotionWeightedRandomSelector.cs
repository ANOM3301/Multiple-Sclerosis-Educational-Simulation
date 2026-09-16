using System.Collections.Generic;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Default AAA-quality selector: filters candidates by character gender (when the
    ///     character is <see cref="CharacterGender.Neutral" />, every clip entry is
    ///     eligible regardless of its own gender tag), biases the
    ///     draw toward clips whose authored affinities match the current emotion bucket,
    ///     and avoids replaying the most recent clip unless it is the only option.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The selector is deterministic for a given
    ///         <see cref="VariantSelectionContext.Seed" />. The unit draw uses
    ///         <see cref="DeterministicEmbodimentRandom.UnitDrawFromLcgSeed" /> so the LCG
    ///         matches other behavior systems ; Unity's global <c>Random</c> is avoided so callers can make
    ///         parallel selections thread-safely and tests can reproduce sequences.
    ///     </para>
    ///     <para>
    ///         Selection weight formula for a candidate:
    ///         <code>
    ///             weight = entry.SelectionWeight
    ///                    * (1 + matchBonus * context.EmotionBiasStrength)
    ///                    * lastPlayedPenalty
    ///         </code>
    ///         <c>matchBonus</c> is <c>1</c> when any of the entry's affinity tags matches
    ///         the current emotion bucket (or when the entry is untagged and the character
    ///         is neutral), and <c>0</c> otherwise. <c>lastPlayedPenalty</c> is
    ///         <c>0.01</c> for the last-played entry (effectively excluded unless it is the
    ///         sole option) and <c>1</c> otherwise.
    ///     </para>
    /// </remarks>
    public sealed class EmotionWeightedRandomSelector : IDialogueVariantSelector
    {
        private const float LastPlayedPenalty = 0.01f;

        private readonly List<int> _scratchIndices = new(8);
        private readonly List<float> _scratchWeights = new(8);

        /// <inheritdoc />
        public bool TrySelect(
            IReadOnlyList<DialogueClipEntry> candidates,
            in VariantSelectionContext context,
            out int selectedIndex,
            out DialogueClipEntry selected)
        {
            selectedIndex = -1;
            selected = default;

            if (candidates == null || candidates.Count == 0)
                return false;

            _scratchIndices.Clear();
            _scratchWeights.Clear();

            DialogueEmotionAffinity targetAffinity = DialogueEmotionAffinityMapping.Resolve(context.Emotion);

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                DialogueClipEntry entry = candidates[i];
                if (!entry.IsValid) continue;
                if (!IsGenderEligible(entry.Gender, context.CharacterGender)) continue;

                float weight = ComputeWeight(in entry, targetAffinity, context.EmotionBiasStrength);
                if (i == context.LastPlayedIndex)
                    weight *= LastPlayedPenalty;

                if (weight <= 0f) continue;

                _scratchIndices.Add(i);
                _scratchWeights.Add(weight);
                totalWeight += weight;
            }

            if (_scratchIndices.Count == 0 || totalWeight <= 0f)
                return false;

            float unit = DeterministicEmbodimentRandom.UnitDrawFromLcgSeed(context.Seed);
            float threshold = unit * totalWeight;
            float accumulator = 0f;

            for (int i = 0; i < _scratchIndices.Count; i++)
            {
                accumulator += _scratchWeights[i];
                if (accumulator >= threshold)
                {
                    selectedIndex = _scratchIndices[i];
                    selected = candidates[selectedIndex];
                    return true;
                }
            }

            // Floating-point safety net: fall back to the last eligible candidate.
            selectedIndex = _scratchIndices[_scratchIndices.Count - 1];
            selected = candidates[selectedIndex];
            return true;
        }

        private static bool IsGenderEligible(CharacterGender entryGender, CharacterGender characterGender)
        {
            if (entryGender == CharacterGender.Neutral) return true;
            if (characterGender == CharacterGender.Neutral) return true;
            return entryGender == characterGender;
        }

        private static float ComputeWeight(
            in DialogueClipEntry entry,
            DialogueEmotionAffinity targetAffinity,
            float emotionBiasStrength)
        {
            float baseWeight = entry.SelectionWeight;

            if (emotionBiasStrength <= 0f)
                return baseWeight;

            bool matches = AffinityMatches(entry.PreferredEmotions, targetAffinity);
            float bonus = matches ? 1f : 0f;
            return baseWeight * (1f + bonus * emotionBiasStrength);
        }

        private static bool AffinityMatches(
            DialogueEmotionAffinity[] preferred,
            DialogueEmotionAffinity target)
        {
            if (preferred == null || preferred.Length == 0)
                return target == DialogueEmotionAffinity.Neutral;

            for (int i = 0; i < preferred.Length; i++)
                if (preferred[i] == target) return true;

            return false;
        }

    }
}
