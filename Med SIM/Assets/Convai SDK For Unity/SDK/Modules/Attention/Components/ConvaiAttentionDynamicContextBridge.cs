using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using Convai.Runtime.Animation;
using Convai.Runtime.Components;
using Convai.Runtime;
using Convai.Runtime.DynamicContext;
using Convai.Runtime.Embodiment;
using Convai.Runtime.SceneMetadata;
using UnityEngine;

namespace Convai.Modules.Attention.Components
{
    /// <summary>
    ///     Mirrors the character's current attention target into dynamic context
    ///     <c>current_attention_object</c> for backend action-reference grounding.
    /// </summary>
    [AddComponentMenu("Convai/Embodiment/Attention Dynamic Context Bridge")]
    [DisallowMultipleComponent]
    public sealed class ConvaiAttentionDynamicContextBridge : MonoBehaviour, IEmbodimentTickable
    {
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum attention commitment required before publishing an attention object.")]
        private float _commitmentThreshold = 0.5f;

        private int _lastPublishedGenerationId = int.MinValue;
        private string _lastPublishedObjectName;

        EmbodimentTickPhase IEmbodimentTickable.Phase => EmbodimentTickPhase.Cognition;

        private EmbodimentContext _context;

        private void OnEnable()
        {
            if (!EmbodimentContext.TryResolveFor(this, out _context)) return;
            _context.EnsureTickScheduler()?.Register(this);
        }

        private void OnDisable()
        {
            _context?.TickScheduler?.Unregister(this);
            _lastPublishedGenerationId = int.MinValue;
            _lastPublishedObjectName = null;
        }

        void IEmbodimentTickable.EmbodimentTick(float deltaTime)
        {
            if (!TryEnsureContext()) return;

            ConvaiCharacter character = ResolveCharacter();
            if (character == null) return;

            AttentionReading reading = _context.AttentionSource?.Current ?? AttentionReading.Empty;
            if (!reading.IsValid || reading.Commitment < _commitmentThreshold)
            {
                PublishAttentionClearIfNeeded();
                return;
            }

            if (!ConvaiWorldObjectUtility.TryResolveObjectName(reading.Target, out string objectName))
            {
                PublishAttentionClearIfNeeded();
                return;
            }

            if (reading.TargetGenerationId == _lastPublishedGenerationId &&
                string.Equals(_lastPublishedObjectName, objectName, System.StringComparison.Ordinal))
                return;

            character.DynamicContext.SetCurrentAttentionObject(
                objectName,
                ConvaiRespondMode.Silent);

            _lastPublishedGenerationId = reading.TargetGenerationId;
            _lastPublishedObjectName = objectName;
        }

        private void PublishAttentionClearIfNeeded()
        {
            if (string.IsNullOrEmpty(_lastPublishedObjectName)) return;

            ConvaiCharacter character = ResolveCharacter();
            if (character == null) return;

            character.DynamicContext.ClearCurrentAttentionObject(ConvaiRespondMode.Silent);
            _lastPublishedGenerationId = int.MinValue;
            _lastPublishedObjectName = null;
        }

        private bool TryEnsureContext()
        {
            if (_context != null) return true;
            return EmbodimentContext.TryResolveFor(this, out _context);
        }

        private ConvaiCharacter ResolveCharacter() =>
            _context?.Character ?? GetComponentInParent<ConvaiCharacter>(true);
    }
}
