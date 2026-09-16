using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using UnityEngine;

namespace Convai.Tests.EditMode.Fixtures
{
    /// <summary>Settable <see cref="IFocusTargetProvider" /> for unit tests.</summary>
    public sealed class FakeFocusTargetProvider : IFocusTargetProvider
    {
        private readonly List<AttentionCandidate> _candidates = new();

        public int Priority { get; set; } = 0;

        public void AddCandidate(AttentionCandidate candidate) => _candidates.Add(candidate);
        public void ClearCandidates() => _candidates.Clear();

        public bool TryGetCandidate(Transform characterRoot, out AttentionCandidate candidate)
        {
            if (_candidates.Count == 0)
            {
                candidate = default;
                return false;
            }

            candidate = _candidates[0];
            return true;
        }
    }
}
