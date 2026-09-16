using System;
using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Modules;
using Convai.Domain.Embodiment.Readings;
using Convai.Modules.Attention.Core;
using Convai.Modules.Attention.Profiles;
using Convai.Modules.Attention.Providers;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Attention.Components
{
    /// <summary>
    ///     MonoBehaviour front-end for the Attention module. Discovers all
    ///     <see cref="IFocusTargetProvider" /> instances on the character hierarchy, feeds
    ///     their per-frame candidates into the <see cref="WeightedAttentionDirector" />, and
    ///     exposes the result through <see cref="IAttentionSource" />.
    /// </summary>
    [AddComponentMenu("Convai/Embodiment/Attention Controller")]
    [DisallowMultipleComponent]
    public sealed class ConvaiAttentionController : EmbodimentProfileReceiver<ConvaiAttentionProfile>,
        IAttentionSource,
        IEmbodimentTickable
    {
        [SerializeField]
        [Tooltip("Provider discovery scope: character hierarchy (recommended) or this GameObject only.")]
        private bool discoverProvidersInHierarchy = true;

        [SerializeField]
        [Tooltip("When enabled, adds the hidden default focus provider at runtime if no provider exists.")]
        private bool autoCreateDefaultFocusProvider = true;

        private readonly List<IFocusTargetProvider> _providers = new(4);
        private readonly List<IFocusTargetProvider> _providerScratch = new(8);
        private readonly List<AttentionCandidate> _candidates = new(4);

        private WeightedAttentionDirector _director;
        private DefaultFocusTargetProvider _ownedDefaultFocusProvider;

        /// <inheritdoc />
        public AttentionReading Current => _director?.Current ?? AttentionReading.Empty;

        /// <inheritdoc />
        EmbodimentTickPhase IEmbodimentTickable.Phase => EmbodimentTickPhase.Cognition;

        protected override string ProfileModuleId => ModuleIds.Attention;
        protected override Func<ConvaiAttentionProfile> DefaultProfileFactory => ConvaiAttentionProfile.CreateDefault;

        protected override void OnProfileApplied(ConvaiAttentionProfile newProfile)
        {
            _director?.Reset();
            ConfigureOwnedDefaultFocusProvider();
        }

        protected override void Awake()
        {
            base.Awake();
            _director = new WeightedAttentionDirector();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!enabled) return;

            RefreshProviders();
            EnsureDefaultFocusProviderIfNeeded();
            Context.RegisterAttentionSource(this);
            Context.EnsureTickScheduler()?.Register(this);
        }

        protected override void OnDisable()
        {
            Context?.UnregisterAttentionSource(this);
            Context?.TickScheduler?.Unregister(this);
            _director?.Reset();
            DestroyOwnedDefaultFocusProvider();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            DestroyOwnedDefaultFocusProvider();
            base.OnDestroy();
        }

        /// <summary>Rescans the hierarchy for providers. Call after dynamic provider changes.</summary>
        public void RefreshProviders()
        {
            _providers.Clear();
            Transform root = discoverProvidersInHierarchy
                ? (Context != null ? Context.CharacterRoot : transform.root)
                : transform;

            if (root == null) return;

            _providerScratch.Clear();
            if (discoverProvidersInHierarchy)
                root.GetComponentsInChildren(true, _providerScratch);
            else
                root.GetComponents(_providerScratch);

            for (int i = 0; i < _providerScratch.Count; i++)
                _providers.Add(_providerScratch[i]);

            _providerScratch.Clear();
        }

        private void EnsureDefaultFocusProviderIfNeeded()
        {
            if (!autoCreateDefaultFocusProvider || _providers.Count > 0) return;
            if (Context == null || !UnityEngine.Application.isPlaying) return;

            GameObject owner = Context.CharacterRoot != null
                ? Context.CharacterRoot.gameObject
                : gameObject;

            _ownedDefaultFocusProvider = owner.AddComponent<DefaultFocusTargetProvider>();
            _ownedDefaultFocusProvider.hideFlags = HideFlags.None;
            ConfigureOwnedDefaultFocusProvider();
            RefreshProviders();
        }

        private void ConfigureOwnedDefaultFocusProvider()
        {
            if (_ownedDefaultFocusProvider == null) return;

            ConvaiAttentionProfile p = EffectiveProfile;
            _ownedDefaultFocusProvider.Configure(
                p.DefaultFocusBaseRelevance,
                p.DefaultFocusTargetHeadHeight,
                p.DefaultFocusMaxDistance,
                p.DefaultFocusFullRelevanceDistance);
        }

        void IEmbodimentTickable.EmbodimentTick(float deltaTime)
        {
            if (_director == null) return;

            _candidates.Clear();
            Transform root = Context != null ? Context.CharacterRoot : transform;

            for (int i = 0; i < _providers.Count; i++)
            {
                IFocusTargetProvider provider = _providers[i];
                if (provider == null) continue;
                if (provider.TryGetCandidate(root, out AttentionCandidate candidate))
                    _candidates.Add(candidate);
            }

            AttentionTimings timings = EffectiveProfile.ToTimings();
            _director.Tick(_candidates, in timings, deltaTime);
        }

        private void DestroyOwnedDefaultFocusProvider()
        {
            if (_ownedDefaultFocusProvider == null) return;

            DefaultFocusTargetProvider provider = _ownedDefaultFocusProvider;
            _ownedDefaultFocusProvider = null;
            _providers.Remove(provider);

            if (UnityEngine.Application.isPlaying)
                Destroy(provider);
            else
                DestroyImmediate(provider);
        }
    }
}
