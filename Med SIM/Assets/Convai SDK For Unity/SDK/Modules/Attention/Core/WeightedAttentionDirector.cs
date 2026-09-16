using System.Collections.Generic;
using Convai.Domain.Embodiment.Readings;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Attention.Core
{
    /// <summary>
    ///     Pure-POCO attention arbiter. Selects a single focus target from a candidate list
    ///     using a priority + relevance + interest-budget model and produces a smoothed
    ///     <see cref="AttentionReading" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Unlike a pure-priority picker, the director continuously drains interest from
    ///         the currently committed target and restores interest to neglected candidates.
    ///         This produces human-like scanning  -  the character holds gaze but occasionally
    ///         glances at other salient candidates before returning. The author controls the
    ///         decay and recovery rates via <see cref="AttentionTimings" />.
    ///     </para>
    ///     <para>
    ///         The director is stateless with respect to Unity objects  -  it only caches
    ///         interest by integer key (instance-id or hashed debug name), so destroyed
    ///         transforms are automatically pruned via stale entries decaying naturally.
    ///     </para>
    /// </remarks>
    internal sealed class WeightedAttentionDirector
    {
        private readonly Dictionary<int, float> _interest = new(8);
        private readonly List<int> _staleKeys = new(8);

        private AttentionCandidate _currentCandidate;
        private int _currentKey;
        private bool _hasCurrent;
        private float _commitment;
        private float _holdTimer;
        private float _continuousHoldElapsed;
        private Vector3 _smoothedPoint;
        private bool _hasSmoothed;
        private int _generationId;

        /// <summary>Latest reading produced by <see cref="Tick" />.</summary>
        public AttentionReading Current { get; private set; } = AttentionReading.Empty;

        /// <summary>
        ///     Advances the director by one frame with the given candidate list.
        /// </summary>
        /// <param name="candidates">
        ///     Candidates for this frame, pre-scored by providers. The director does not
        ///     mutate the list. May be empty for "no candidates this frame".
        /// </param>
        /// <param name="timings">Tuning values; typically sourced from a profile SO.</param>
        /// <param name="deltaTime">Unscaled or scaled delta time (up to caller).</param>
        public AttentionReading Tick(
            IReadOnlyList<AttentionCandidate> candidates,
            in AttentionTimings timings,
            float deltaTime)
        {
            int bestIndex = SelectBestCandidate(candidates);
            RecoverNeglectedInterest(candidates, bestIndex, timings, deltaTime);
            PruneStaleInterest(candidates);

            bool hasBest = bestIndex >= 0;
            if (hasBest)
            {
                UpdateOnValidTarget(candidates, bestIndex, timings, deltaTime);
            }
            else
            {
                UpdateOnNoTarget(timings, deltaTime);
            }

            UpdateCommitmentRamp(hasBest, timings, deltaTime);
            UpdateSmoothedPoint(hasBest, hasBest ? candidates[bestIndex] : default, timings, deltaTime);

            Transform targetTransform = _hasCurrent ? _currentCandidate.Target : null;
            Current = new AttentionReading(
                isValid: _commitment > 0.0001f && _hasSmoothed,
                target: targetTransform,
                smoothedPoint: _smoothedPoint,
                commitment: _commitment,
                targetGenerationId: _generationId);
            return Current;
        }

        /// <summary>Clears all internal state; call when the character is disabled or re-bound.</summary>
        public void Reset()
        {
            _interest.Clear();
            _currentCandidate = default;
            _currentKey = 0;
            _hasCurrent = false;
            _commitment = 0f;
            _holdTimer = 0f;
            _continuousHoldElapsed = 0f;
            _smoothedPoint = default;
            _hasSmoothed = false;
            Current = AttentionReading.Empty;
        }

        private int SelectBestCandidate(IReadOnlyList<AttentionCandidate> candidates)
        {
            int bestIndex = -1;
            float bestScore = float.NegativeInfinity;
            int bestPriority = int.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                AttentionCandidate c = candidates[i];
                if (c.Relevance <= 0f) continue;

                float interest = GetOrInitInterest(CandidateKey(c));
                // Priority is the primary sort criterion; within the same priority tier
                // relevance × interest determines the winner (enabling natural gaze scanning
                // between equal-priority candidates via interest-budget cycling).
                float score = c.Relevance * interest;
                bool wins = c.Priority > bestPriority ||
                            (c.Priority == bestPriority && score > bestScore);
                if (wins)
                {
                    bestScore = score;
                    bestPriority = c.Priority;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void RecoverNeglectedInterest(
            IReadOnlyList<AttentionCandidate> candidates,
            int bestIndex,
            in AttentionTimings timings,
            float deltaTime)
        {
            if (timings.InterestRecoveryPerSecond <= 0f) return;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (i == bestIndex) continue;

                int key = CandidateKey(candidates[i]);
                float interest = GetOrInitInterest(key);
                interest = Mathf.Min(1f, interest + timings.InterestRecoveryPerSecond * deltaTime);
                _interest[key] = interest;
            }
        }

        private void UpdateOnValidTarget(
            IReadOnlyList<AttentionCandidate> candidates,
            int chosenIndex,
            in AttentionTimings timings,
            float deltaTime)
        {
            AttentionCandidate chosen = candidates[chosenIndex];
            int key = CandidateKey(chosen);
            bool changedTarget = !_hasCurrent || key != _currentKey;

            if (changedTarget)
            {
                _currentCandidate = chosen;
                _currentKey = key;
                _hasCurrent = true;
                _continuousHoldElapsed = 0f;
                _generationId++;
            }
            else
            {
                _continuousHoldElapsed += deltaTime;
            }

            float interest = GetOrInitInterest(key);
            interest = Mathf.Max(0f, interest - timings.InterestDecayPerSecond * deltaTime);

            // Force a break when we exceed the continuous hold budget or fall below
            // the interest floor  -  selection will pick a different candidate next frame.
            bool forceBreak =
                _continuousHoldElapsed > timings.MaxContinuousHoldSeconds ||
                interest <= timings.InterestBreakThreshold;

            if (forceBreak)
            {
                if (HasAlternativeCandidate(candidates, chosenIndex, key))
                {
                    interest = 0f;
                    _hasCurrent = false;
                    _currentKey = 0;
                }
                else
                {
                    interest = Mathf.Max(interest, timings.InterestBreakThreshold);
                    _continuousHoldElapsed = Mathf.Min(_continuousHoldElapsed, timings.MaxContinuousHoldSeconds);
                }
            }

            _interest[key] = interest;
            _holdTimer = 0f;
        }

        private void UpdateOnNoTarget(in AttentionTimings timings, float deltaTime)
        {
            _holdTimer += deltaTime;
            if (_holdTimer > timings.FocusLossHoldSeconds)
            {
                _hasCurrent = false;
                _currentKey = 0;
            }
        }

        private void UpdateCommitmentRamp(bool hasBest, in AttentionTimings timings, float deltaTime)
        {
            bool inActiveWindow = hasBest || _hasCurrent;
            float targetCommitment = inActiveWindow ? 1f : 0f;
            float rampSeconds = targetCommitment > _commitment
                ? timings.CommitmentAcquireSeconds
                : timings.CommitmentReleaseSeconds;
            float rampRate = rampSeconds > 0.0001f ? 1f / rampSeconds : 1e6f;
            _commitment = Mathf.MoveTowards(_commitment, targetCommitment, rampRate * deltaTime);
        }

        private void UpdateSmoothedPoint(
            bool hasBest,
            in AttentionCandidate chosen,
            in AttentionTimings timings,
            float deltaTime)
        {
            if (hasBest)
            {
                Vector3 target = chosen.WorldPoint + timings.FocusOffset;
                if (!_hasSmoothed)
                {
                    _smoothedPoint = target;
                    _hasSmoothed = true;
                }
                else
                {
                    float k = 1f - Mathf.Exp(-timings.FocusPositionLerpSpeed * deltaTime);
                    _smoothedPoint = Vector3.Lerp(_smoothedPoint, target, k);
                }
            }
            else if (_commitment <= 0.0001f)
            {
                _hasSmoothed = false;
            }
        }

        private void PruneStaleInterest(IReadOnlyList<AttentionCandidate> candidates)
        {
            if (_interest.Count == 0) return;

            _staleKeys.Clear();
            foreach (KeyValuePair<int, float> kvp in _interest)
                _staleKeys.Add(kvp.Key);

            for (int i = 0; i < candidates.Count; i++)
                _staleKeys.Remove(CandidateKey(candidates[i]));

            if (_hasCurrent)
                _staleKeys.Remove(_currentKey);

            for (int i = 0; i < _staleKeys.Count; i++)
                _interest.Remove(_staleKeys[i]);
        }

        private static bool HasAlternativeCandidate(
            IReadOnlyList<AttentionCandidate> candidates,
            int chosenIndex,
            int chosenKey)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (i == chosenIndex) continue;
                AttentionCandidate candidate = candidates[i];
                if (candidate.Relevance <= 0f) continue;
                if (CandidateKey(candidate) != chosenKey)
                    return true;
            }

            return false;
        }

        private float GetOrInitInterest(int key)
        {
            if (_interest.TryGetValue(key, out float interest)) return interest;
            _interest[key] = 1f;
            return 1f;
        }

        private static int CandidateKey(in AttentionCandidate candidate)
        {
            if (candidate.Target != null) return candidate.Target.GetEntityId().GetHashCode();
            if (!string.IsNullOrEmpty(candidate.DebugName))
                return DeterministicEmbodimentRandom.StableStringHash(candidate.DebugName);
            // Fall-back: quantized world position hash (stable across identical points).
            Vector3 p = candidate.WorldPoint;
            int h = 17;
            h = h * 31 + Mathf.RoundToInt(p.x * 1000f);
            h = h * 31 + Mathf.RoundToInt(p.y * 1000f);
            h = h * 31 + Mathf.RoundToInt(p.z * 1000f);
            return h;
        }
    }
}
