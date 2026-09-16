using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Modules;
using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Gaze.Components
{
    /// <summary>
    ///     Combines the current <see cref="IAttentionSource" /> target with the current
    ///     <see cref="IConversationFlowSource" /> state to produce a
    ///     <see cref="GazeIntent" /> consumed by eye and head actuators.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Centralizing this blend in a single authoritative coordinator prevents eye and
    ///         head actuators from inventing their own (potentially conflicting) policies for
    ///         how aggressively the character should commit to a target during each dialogue
    ///         phase.
    ///     </para>
    ///     <para>
    ///         When no <see cref="IConversationFlowSource" /> is registered, dialogue state is
    ///         treated as <see cref="DialogueState.Idle" /> for policy lookup. Add
    ///         <c>ConvaiConversationFlowController</c> (hidden or manual) or another flow source when
    ///         gaze weighting must react to Speaking / Reacting. This coordinator does not
    ///         request auto-provisioning of the flow driver so eye/head-only stacks stay minimal.
    ///     </para>
    /// </remarks>
    [AddComponentMenu("Convai/Embodiment/Gaze Coordinator")]
    [DisallowMultipleComponent]
    public sealed class ConvaiGazeCoordinator : EmbodimentProfileReceiver<ConvaiGazeCoordinationProfile>,
        IGazeIntentProvider,
        IEmbodimentTickable
    {
        private float _smoothedOverallWeight;
        private float _smoothedEyeShare = 1f;
        private Vector3 _lastTargetPoint;
        private bool _hasTarget;

        /// <inheritdoc />
        public GazeIntent Current { get; private set; } = GazeIntent.Relaxed;

        /// <inheritdoc />
        EmbodimentTickPhase IEmbodimentTickable.Phase => EmbodimentTickPhase.Cognition;

        /// <inheritdoc />
        protected override string ProfileModuleId => ModuleIds.GazeCoordination;

        /// <inheritdoc />
        protected override System.Func<ConvaiGazeCoordinationProfile> DefaultProfileFactory => ConvaiGazeCoordinationProfile.CreateDefault;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!enabled) return;

            Context.RegisterGazeIntentProvider(this);
            Context.EnsureTickScheduler()?.Register(this);
        }

        protected override void OnDisable()
        {
            Context?.UnregisterGazeIntentProvider(this);
            Context?.TickScheduler?.Unregister(this);
            Current = GazeIntent.Relaxed;
            _smoothedOverallWeight = 0f;
            _smoothedEyeShare = 1f;
            _hasTarget = false;

            base.OnDisable();
        }

        void IEmbodimentTickable.EmbodimentTick(float deltaTime)
        {
            if (Context == null) return;

            DialogueState state = Context.ConversationFlowSource?.Current.Primary ?? DialogueState.Idle;
            ConvaiGazeCoordinationProfile p = EffectiveProfile;
            Transform reference = GazeReferenceFrame.Resolve(Context, transform);
            p.GetStatePolicy(
                state,
                out float targetOverall,
                out float targetEyeShare,
                out bool suppressAttentionTarget);

            AttentionReading attention = Context.AttentionSource?.Current ?? AttentionReading.Empty;
            if (attention.IsValid && !suppressAttentionTarget)
            {
                _lastTargetPoint = attention.SmoothedPoint;
                _hasTarget = true;
                targetOverall *= attention.Commitment;
            }
            else
            {
                _hasTarget = false;
                targetOverall = 0f;
            }

            float weightAlpha = 1f - Mathf.Exp(-Mathf.Max(0f, p.WeightBlendSpeed) * deltaTime);
            float shareAlpha = 1f - Mathf.Exp(-Mathf.Max(0f, p.EyeShareBlendSpeed) * deltaTime);

            _smoothedOverallWeight += (targetOverall - _smoothedOverallWeight) * weightAlpha;
            _smoothedEyeShare += (targetEyeShare - _smoothedEyeShare) * shareAlpha;

            Current = new GazeIntent(
                ResolveIntentTargetPoint(reference),
                _smoothedOverallWeight,
                _smoothedEyeShare);
        }

        private Vector3 ResolveIntentTargetPoint(Transform reference)
        {
            // During target suppression / loss, preserve the last valid gaze point while the
            // authority weight decays. This avoids snapping eyes/head toward the rig root before
            // idle exploration takes over.
            if (_hasTarget || _smoothedOverallWeight > 0.0001f)
                return _lastTargetPoint;

            return ResolveRelaxedTargetPoint(reference);
        }

        private Vector3 ResolveRelaxedTargetPoint(Transform reference)
        {
            Vector3 forward = reference != null ? reference.forward : transform.forward;
            if (forward.sqrMagnitude <= 1e-6f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            if (Context?.RigBinding != null &&
                Context.RigBinding.TryGetBone(StandardBone.Head, out Transform head) &&
                head != null)
            {
                return head.position + forward * 2f;
            }

            Vector3 origin = reference != null ? reference.position : transform.position;
            Vector3 up = reference != null ? reference.up : transform.up;
            if (up.sqrMagnitude <= 1e-6f)
                up = Vector3.up;
            else
                up.Normalize();

            return origin + up * 1.6f + forward * 2f;
        }
    }
}
