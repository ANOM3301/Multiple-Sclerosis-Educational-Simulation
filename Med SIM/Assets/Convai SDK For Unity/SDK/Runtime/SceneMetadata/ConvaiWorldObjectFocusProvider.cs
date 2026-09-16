using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Runtime.SceneMetadata
{
    /// <summary>
    ///     Exposes a <see cref="ConvaiObjectMetadata" /> as an attention candidate so gaze and
    ///     attention directors can target authored world objects by name.
    /// </summary>
    [AddComponentMenu("Convai/World Object Focus Provider")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ConvaiObjectMetadata))]
    public sealed class ConvaiWorldObjectFocusProvider : MonoBehaviour, IFocusTargetProvider
    {
        [SerializeField] private int _priority = 10;

        [SerializeField, Range(0f, 1f)] private float _baseRelevance = 0.75f;

        [SerializeField, Min(0f)] private float _maxDistance = 12f;

        [SerializeField, Min(0f)] private float _fullRelevanceDistance = 4f;

        private ConvaiObjectMetadata _metadata;
        private Transform _targetTransform;

        public int Priority => _priority;

        private void Awake()
        {
            _metadata = GetComponent<ConvaiObjectMetadata>();
            _targetTransform = transform;
        }

        public bool TryGetCandidate(Transform characterRoot, out AttentionCandidate candidate)
        {
            candidate = default;
            if (_metadata == null || !_metadata.IsValid || _targetTransform == null)
                return false;

            Vector3 worldPoint = _targetTransform.position;
            float distance = characterRoot != null
                ? Vector3.Distance(characterRoot.position, worldPoint)
                : 0f;
            if (distance > _maxDistance) return false;

            float relevance = _baseRelevance;
            if (_maxDistance > _fullRelevanceDistance)
            {
                float normalized = 1f - Mathf.InverseLerp(_fullRelevanceDistance, _maxDistance, distance);
                relevance *= normalized;
            }

            if (relevance <= 0f) return false;

            candidate = new AttentionCandidate(
                _priority,
                relevance,
                _targetTransform,
                worldPoint,
                _metadata.ObjectName);
            return true;
        }
    }
}
