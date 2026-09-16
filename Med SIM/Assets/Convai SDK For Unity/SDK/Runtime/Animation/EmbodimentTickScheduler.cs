using System.Collections.Generic;
using UnityEngine;

namespace Convai.Runtime.Animation
{
    /// <summary>
    ///     Drives <see cref="IEmbodimentTickable" /> instances in a deterministic
    ///     cognition -> expression -> finalize order every frame.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Replaces ad-hoc <c>[DefaultExecutionOrder]</c> sprinkling. Each embodiment module
    ///         component registers itself on <c>OnEnable</c> and unregisters on
    ///         <c>OnDisable</c>; the scheduler guarantees every registered tickable runs
    ///         exactly once per frame in its declared phase.
    ///     </para>
    ///     <para>
    ///         Execution order is pinned: scheduler runs with
    ///         <see cref="DefaultExecutionOrder" /> <c>18000</c>, which is ordered before the
    ///         <see cref="AnimatorConductor" /> at <c>19000</c> and the
    ///         <see cref="FacialBlendshapeCompositorHost" /> at <c>20000</c>. Concretely:
    ///         Unity Update (all MonoBehaviours) -> scheduler tick -> animator resampling ->
    ///         compositor flush.
    ///     </para>
    ///     <para>
    ///         The scheduler is tolerant to registration churn during a tick ; tickables may
    ///         register or unregister mid-tick; changes apply on the next frame.
    ///     </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(Convai.Runtime.Embodiment.EmbodimentExecutionOrders.TickScheduler)]
    [AddComponentMenu("")]
    public sealed class EmbodimentTickScheduler : MonoBehaviour
    {
        private readonly List<IEmbodimentTickable> _cognition = new(8);
        private readonly List<IEmbodimentTickable> _expression = new(8);
        private readonly List<IEmbodimentTickable> _finalize = new(4);

        private readonly HashSet<IEmbodimentTickable> _cognitionSet = new();
        private readonly HashSet<IEmbodimentTickable> _expressionSet = new();
        private readonly HashSet<IEmbodimentTickable> _finalizeSet = new();

        private bool _isIterating;
        private readonly List<RegistrationChange> _pendingChanges = new();

        /// <summary>Locates or creates a scheduler on the supplied component's character root.</summary>
        public static EmbodimentTickScheduler GetOrCreate(Component context)
        {
            if (context == null) return null;

            EmbodimentTickScheduler existing = context.GetComponentInParent<EmbodimentTickScheduler>(true);
            if (existing != null) return existing;

            if (!UnityEngine.Application.isPlaying) return null;
            EmbodimentTickScheduler created = context.gameObject.AddComponent<EmbodimentTickScheduler>();
            created.hideFlags = Convai.Runtime.Embodiment.EmbodimentContext.RuntimeInfrastructureHideFlags();
            return created;
        }

        private void Awake()
        {
            hideFlags = Convai.Runtime.Embodiment.EmbodimentContext.RuntimeInfrastructureHideFlags();
        }

        /// <summary>Registers a tickable so it is included in subsequent frames.</summary>
        public void Register(IEmbodimentTickable tickable)
        {
            if (tickable == null) return;

            if (_isIterating)
            {
                _pendingChanges.Add(new RegistrationChange(tickable, add: true));
                return;
            }

            List<IEmbodimentTickable> bucket = GetBucket(tickable.Phase);
            HashSet<IEmbodimentTickable> set = GetBucketSet(tickable.Phase);
            if (set.Add(tickable))
                bucket.Add(tickable);
        }

        /// <summary>Unregisters a previously registered tickable. Safe to call from <c>OnDisable</c>.</summary>
        public void Unregister(IEmbodimentTickable tickable)
        {
            if (tickable == null) return;

            if (_isIterating)
            {
                _pendingChanges.Add(new RegistrationChange(tickable, add: false));
                return;
            }

            List<IEmbodimentTickable> bucket = GetBucket(tickable.Phase);
            HashSet<IEmbodimentTickable> set = GetBucketSet(tickable.Phase);
            set.Remove(tickable);
            bucket.Remove(tickable);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            TickBucket(_cognition, deltaTime);
            TickBucket(_expression, deltaTime);
            TickBucket(_finalize, deltaTime);
            FlushPendingChanges();
        }

        private void TickBucket(List<IEmbodimentTickable> bucket, float deltaTime)
        {
            _isIterating = true;
            try
            {
                for (int i = 0; i < bucket.Count; i++)
                {
                    IEmbodimentTickable tickable = bucket[i];
                    if (tickable == null) continue;
                    try
                    {
                        tickable.EmbodimentTick(deltaTime);
                    }
                    catch (System.Exception ex)
                    {
                        // Never let one misbehaving module kill the whole frame.
                        Debug.LogException(ex, this);
                    }
                }
            }
            finally
            {
                _isIterating = false;
            }
        }

        private void FlushPendingChanges()
        {
            if (_pendingChanges.Count == 0) return;

            for (int i = 0; i < _pendingChanges.Count; i++)
            {
                RegistrationChange change = _pendingChanges[i];
                List<IEmbodimentTickable> bucket = GetBucket(change.Tickable.Phase);
                HashSet<IEmbodimentTickable> set = GetBucketSet(change.Tickable.Phase);
                if (change.Add)
                {
                    if (set.Add(change.Tickable))
                        bucket.Add(change.Tickable);
                }
                else
                {
                    set.Remove(change.Tickable);
                    bucket.Remove(change.Tickable);
                }
            }
            _pendingChanges.Clear();
        }

        private List<IEmbodimentTickable> GetBucket(EmbodimentTickPhase phase)
        {
            return phase switch
            {
                EmbodimentTickPhase.Cognition => _cognition,
                EmbodimentTickPhase.Expression => _expression,
                EmbodimentTickPhase.Finalize => _finalize,
                _ => _expression
            };
        }

        private HashSet<IEmbodimentTickable> GetBucketSet(EmbodimentTickPhase phase)
        {
            return phase switch
            {
                EmbodimentTickPhase.Cognition => _cognitionSet,
                EmbodimentTickPhase.Expression => _expressionSet,
                EmbodimentTickPhase.Finalize => _finalizeSet,
                _ => _expressionSet
            };
        }

        private readonly struct RegistrationChange
        {
            public RegistrationChange(IEmbodimentTickable tickable, bool add)
            {
                Tickable = tickable;
                Add = add;
            }

            public IEmbodimentTickable Tickable { get; }
            public bool Add { get; }
        }
    }
}
