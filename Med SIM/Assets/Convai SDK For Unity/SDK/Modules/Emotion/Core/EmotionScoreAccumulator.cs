using System;
using System.Collections.Generic;
using Convai.Domain.Embodiment.Taxonomy;
using UnityEngine;

namespace Convai.Modules.Emotion.Core
{
    /// <summary>
    ///     Pure-POCO per-emotion score smoothing and micro-expression burst processor
    ///     backed by a taxonomy-driven design.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Maintains three score tables: a target table written by callers
    ///         (e.g. a server event), a current table updated each tick by exponential
    ///         smoothing, and a public output table that additionally folds in micro-expression
    ///         overshoot.
    ///     </para>
    ///     <para>
    ///         Tables are keyed by canonical lowercase label from the supplied
    ///         <see cref="IEmotionTaxonomy" />. Allocation-free in steady state.
    ///     </para>
    /// </remarks>
    internal sealed class EmotionScoreAccumulator
    {
        private readonly IEmotionTaxonomy _taxonomy;
        private readonly Dictionary<string, float> _targetScores;
        private readonly Dictionary<string, float> _currentScores;
        private readonly Dictionary<string, float> _previousTargets;
        private readonly Dictionary<string, float> _outputScores;

        private float _lerpSpeed;
        private float _decaySpeed;

        private bool _microBurstEnabled;
        private float _microBurstDuration = 0.25f;
        private float _microBurstOvershoot = 1.4f;
        private float _microBurstThreshold = 0.15f;
        private string _burstLabel;
        private float _burstTimeRemaining;

        public EmotionScoreAccumulator(IEmotionTaxonomy taxonomy, float lerpSpeed = 5f, float decaySpeed = 2f)
        {
            _taxonomy = taxonomy ?? throw new ArgumentNullException(nameof(taxonomy));
            _lerpSpeed = Mathf.Max(0.01f, lerpSpeed);
            _decaySpeed = Mathf.Max(0.01f, decaySpeed);

            int capacity = taxonomy.Emotions.Count;
            _targetScores = new Dictionary<string, float>(capacity, StringComparer.OrdinalIgnoreCase);
            _currentScores = new Dictionary<string, float>(capacity, StringComparer.OrdinalIgnoreCase);
            _previousTargets = new Dictionary<string, float>(capacity, StringComparer.OrdinalIgnoreCase);
            _outputScores = new Dictionary<string, float>(capacity, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < taxonomy.Emotions.Count; i++)
            {
                string label = taxonomy.Emotions[i].Label;
                _targetScores[label] = 0f;
                _currentScores[label] = 0f;
                _previousTargets[label] = 0f;
                _outputScores[label] = 0f;
            }
        }

        /// <summary>Read-only view of the per-emotion output scores for the current frame.</summary>
        public IReadOnlyDictionary<string, float> OutputScores => _outputScores;

        /// <summary>Updates the interpolation speed at runtime.</summary>
        public void SetLerpSpeed(float lerpSpeed) => _lerpSpeed = Mathf.Max(0.01f, lerpSpeed);

        /// <summary>Updates the decay speed applied when targets are zero.</summary>
        public void SetDecaySpeed(float decaySpeed) => _decaySpeed = Mathf.Max(0.01f, decaySpeed);

        /// <summary>Configures micro-expression burst behavior.</summary>
        public void ConfigureMicroBurst(bool enabled, float duration, float overshoot, float threshold)
        {
            _microBurstEnabled = enabled;
            _microBurstDuration = Mathf.Max(0.05f, duration);
            _microBurstOvershoot = Mathf.Max(1f, overshoot);
            _microBurstThreshold = Mathf.Clamp01(threshold);
        }

        /// <summary>
        ///     Sets a single emotion as the active target. All others decay toward zero.
        ///     Triggers a micro-burst when the delta exceeds <c>_microBurstThreshold</c>.
        /// </summary>
        public void SetTargetEmotion(string canonicalLabel, float score)
        {
            float clamped = Mathf.Clamp01(score);
            string neutral = _taxonomy.Neutral.Label;

            for (int i = 0; i < _taxonomy.Emotions.Count; i++)
            {
                string label = _taxonomy.Emotions[i].Label;
                float newTarget = string.Equals(label, canonicalLabel, StringComparison.OrdinalIgnoreCase)
                    ? clamped
                    : 0f;

                MaybeTriggerBurst(label, newTarget, neutral);
                _previousTargets[label] = newTarget;
                _targetScores[label] = newTarget;
            }
        }

        /// <summary>
        ///     Sets target scores for multiple emotions at once. Any emotion absent from the
        ///     input is set to zero.
        /// </summary>
        public void SetTargetEmotions(IReadOnlyDictionary<string, float> scores)
        {
            string neutral = _taxonomy.Neutral.Label;

            for (int i = 0; i < _taxonomy.Emotions.Count; i++)
            {
                string label = _taxonomy.Emotions[i].Label;
                float newTarget = scores != null && scores.TryGetValue(label, out float s)
                    ? Mathf.Clamp01(s)
                    : 0f;

                MaybeTriggerBurst(label, newTarget, neutral);
                _previousTargets[label] = newTarget;
                _targetScores[label] = newTarget;
            }
        }

        /// <summary>
        ///     Snaps all three tables to a single emotion immediately (used for previews and
        ///     "lock emotion" overrides).
        /// </summary>
        public void SetImmediateEmotion(string canonicalLabel, float score)
        {
            float clamped = Mathf.Clamp01(score);
            for (int i = 0; i < _taxonomy.Emotions.Count; i++)
            {
                string label = _taxonomy.Emotions[i].Label;
                float value = string.Equals(label, canonicalLabel, StringComparison.OrdinalIgnoreCase)
                    ? clamped
                    : 0f;
                _targetScores[label] = value;
                _currentScores[label] = value;
                _outputScores[label] = value;
                _previousTargets[label] = value;
            }
            _burstLabel = null;
            _burstTimeRemaining = 0f;
        }

        /// <summary>Advances one frame of smoothing and burst animation.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            float lerpAlpha = 1f - Mathf.Exp(-_lerpSpeed * deltaTime);
            float decayAlpha = 1f - Mathf.Exp(-_decaySpeed * deltaTime);

            if (_burstTimeRemaining > 0f)
                _burstTimeRemaining -= deltaTime;

            float burstAlpha = _burstTimeRemaining > 0f
                ? Mathf.Sin((_burstTimeRemaining / _microBurstDuration) * Mathf.PI)
                : 0f;

            for (int i = 0; i < _taxonomy.Emotions.Count; i++)
            {
                string label = _taxonomy.Emotions[i].Label;
                float target = _targetScores[label];
                float current = _currentScores[label];
                float alpha = target > 0.001f ? lerpAlpha : decayAlpha;
                float next = current + (target - current) * alpha;
                if (next < 0.001f && target <= 0f) next = 0f;
                _currentScores[label] = next;

                float output = next;
                if (burstAlpha > 0f && string.Equals(label, _burstLabel, StringComparison.OrdinalIgnoreCase))
                {
                    float overshoot = (_microBurstOvershoot - 1f) * burstAlpha;
                    output = Mathf.Clamp01(next * (1f + overshoot));
                }
                _outputScores[label] = output;
            }
        }

        /// <summary>Returns the dominant non-neutral emotion and its current score.</summary>
        public void GetDominant(out string dominantLabel, out float dominantScore)
        {
            dominantLabel = _taxonomy.Neutral.Label;
            dominantScore = 0f;

            for (int i = 0; i < _taxonomy.Emotions.Count; i++)
            {
                EmotionDescriptor d = _taxonomy.Emotions[i];
                if (d.IsNeutral) continue;

                float value = _currentScores[d.Label];
                if (value > dominantScore)
                {
                    dominantScore = value;
                    dominantLabel = d.Label;
                }
            }
        }

        /// <summary>Zeroes all tables and clears pending bursts.</summary>
        public void Reset()
        {
            for (int i = 0; i < _taxonomy.Emotions.Count; i++)
            {
                string label = _taxonomy.Emotions[i].Label;
                _targetScores[label] = 0f;
                _currentScores[label] = 0f;
                _outputScores[label] = 0f;
                _previousTargets[label] = 0f;
            }
            _burstLabel = null;
            _burstTimeRemaining = 0f;
        }

        private void MaybeTriggerBurst(string label, float newTarget, string neutralLabel)
        {
            if (!_microBurstEnabled) return;
            if (string.Equals(label, neutralLabel, StringComparison.OrdinalIgnoreCase)) return;

            float previous = _previousTargets[label];
            if (newTarget - previous <= _microBurstThreshold) return;

            _burstLabel = label;
            _burstTimeRemaining = _microBurstDuration;
        }
    }
}
