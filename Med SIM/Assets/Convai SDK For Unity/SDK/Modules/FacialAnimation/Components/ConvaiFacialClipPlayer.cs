using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Modules;
using Convai.Modules.FacialAnimation.Core;
using Convai.Modules.FacialAnimation.Profiles;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.FacialAnimation.Components
{
    /// <summary>
    ///     Plays a <see cref="ConvaiFacialAnimationProfile" /> at runtime, submitting the evaluated
    ///     curves to the shared <see cref="FacialBlendshapeCompositorHost" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         General bindings feed the <see cref="FacialBlendshapeLayers.FacialClipGeneral" />
    ///         layer; mouth bindings feed <see cref="FacialBlendshapeLayers.FacialClipMouth" />.
    ///         The facial composition profile on the host decides how these are blended
    ///         against lip-sync and emotion.
    ///     </para>
    ///     <para>
    ///         The source automatically discovers skinned meshes under the character root and
    ///         resolves blendshape indices by name on enable. Adding new meshes at runtime
    ///         requires a manual <see cref="Rebind" /> call.
    ///     </para>
    /// </remarks>
    [AddComponentMenu("Convai/Embodiment/Facial Clip Player")]
    [DisallowMultipleComponent]
    public sealed class ConvaiFacialClipPlayer : EmbodimentProfileReceiver<ConvaiFacialAnimationProfile>,
        IEmbodimentTickable,
        IFacialBlendshapeSource
    {
        [SerializeField, Range(0f, 2f)]
        [Tooltip("Playback speed multiplier.")]
        private float playbackSpeed = 1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Global weight multiplier applied on top of the profile weight.")]
        private float globalWeight = 1f;

        [SerializeField]
        [Tooltip("Explicit list of meshes to drive. Empty = auto-discover under character root.")]
        private List<SkinnedMeshRenderer> targetMeshes = new();

        private readonly List<ResolvedFacialBinding> _resolvedBindings = new();
        private readonly List<BlendshapeTargetKey> _generalTargets = new();
        private readonly List<float> _generalWeights = new();
        private readonly List<BlendshapeTargetKey> _mouthTargets = new();
        private readonly List<float> _mouthWeights = new();
        private float _time;
        private bool _rigBindingChangedHandlerRegistered;

        /// <inheritdoc />
        public Component SourceComponent => this;

        /// <inheritdoc />
        public string SourceName => "ConvaiFacialClipPlayer";

        /// <inheritdoc />
        EmbodimentTickPhase IEmbodimentTickable.Phase => EmbodimentTickPhase.Expression;

        /// <inheritdoc />
        protected override string ProfileModuleId => ModuleIds.BakedFacialClip;

        /// <inheritdoc />
        protected override System.Func<ConvaiFacialAnimationProfile> DefaultProfileFactory => () => null;

        /// <summary>Resets playback to time zero.</summary>
        public void ResetTime() => _time = 0f;

        /// <inheritdoc />
        protected override void OnProfileApplied(ConvaiFacialAnimationProfile newProfile)
        {
            _time = 0f;
            ResolveBindings();
        }

        /// <summary>Re-resolves mesh targets and blendshape indices.</summary>
        public void Rebind() => ResolveBindings();

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!enabled) return;

            Context.EnsureCompositor();
            ResolveBindings();
            Context.RigBindingChanged += HandleRigBindingChanged;
            _rigBindingChangedHandlerRegistered = true;
            Context.EnsureTickScheduler()?.Register(this);
        }

        protected override void OnDisable()
        {
            if (_rigBindingChangedHandlerRegistered && Context != null)
            {
                Context.RigBindingChanged -= HandleRigBindingChanged;
                _rigBindingChangedHandlerRegistered = false;
            }

            Context?.TickScheduler?.Unregister(this);
            _resolvedBindings.Clear();

            base.OnDisable();
        }

        void IEmbodimentTickable.EmbodimentTick(float deltaTime)
        {
            if (profile == null || _resolvedBindings.Count == 0) return;
            FacialBlendshapeCompositorHost compositor = Context?.EnsureCompositor();
            if (compositor == null) return;

            _time = profile.WrapTime(_time + deltaTime * Mathf.Max(0f, playbackSpeed));
            float profileScale = Mathf.Max(0f, profile.WeightMultiplier) / 100f;
            float sourceGlobalScale = Mathf.Max(0f, globalWeight);

            _generalTargets.Clear();
            _generalWeights.Clear();
            _mouthTargets.Clear();
            _mouthWeights.Clear();

            for (int i = 0; i < _resolvedBindings.Count; i++)
            {
                ResolvedFacialBinding b = _resolvedBindings[i];
                if (!b.Key.IsValid) continue;
                float sourceWeight = b.Curve != null ? b.Curve.Evaluate(_time) : 0f;
                float weightScale = b.WeightSettings.ResolveScale(profileScale, sourceGlobalScale);
                float weight = sourceWeight * weightScale;
                if (weight <= 0f) continue;

                if (b.IsMouth)
                {
                    _mouthTargets.Add(b.Key);
                    _mouthWeights.Add(weight);
                }
                else
                {
                    _generalTargets.Add(b.Key);
                    _generalWeights.Add(weight);
                }
            }

            if (_generalTargets.Count > 0)
                compositor.SubmitLayer(
                    this, FacialBlendshapeLayers.FacialClipGeneral,
                    _generalTargets, _generalWeights, _generalTargets.Count);

            if (_mouthTargets.Count > 0)
                compositor.SubmitLayer(
                    this, FacialBlendshapeLayers.FacialClipMouth,
                    _mouthTargets, _mouthWeights, _mouthTargets.Count);
        }

        private void ResolveBindings()
        {
            _resolvedBindings.Clear();
            if (profile == null) return;

            IReadOnlyList<SkinnedMeshRenderer> meshes = DiscoverMeshes();
            if (meshes.Count == 0) return;

            IReadOnlyList<ConvaiFacialAnimationProfile.CurveBinding> bindings = profile.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                ConvaiFacialAnimationProfile.CurveBinding binding = bindings[i];
                if (string.IsNullOrEmpty(binding.BlendshapeName)) continue;
                ConvaiFacialAnimationProfile.BindingWeightSettings weightSettings =
                    profile.ResolveWeightSettings(binding.BlendshapeName);

                for (int m = 0; m < meshes.Count; m++)
                {
                    SkinnedMeshRenderer mesh = meshes[m];
                    if (mesh == null || mesh.sharedMesh == null) continue;

                    int idx = mesh.sharedMesh.GetBlendShapeIndex(binding.BlendshapeName);
                    if (idx < 0) continue;

                    _resolvedBindings.Add(new ResolvedFacialBinding(
                        new BlendshapeTargetKey(mesh, idx),
                        binding.Curve,
                        binding.IsMouth,
                        weightSettings));
                }
            }
        }

        private IReadOnlyList<SkinnedMeshRenderer> DiscoverMeshes()
        {
            if (targetMeshes != null && targetMeshes.Count > 0) return targetMeshes;
            IStandardRigBinding rigBinding = Context?.EnsureRigBinding();
            if (rigBinding != null &&
                rigBinding.FacialMeshes != null &&
                rigBinding.FacialMeshes.Count > 0)
            {
                return rigBinding.FacialMeshes;
            }

            Transform root = Context != null ? Context.CharacterRoot : transform;
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        private void HandleRigBindingChanged(IStandardRigBinding rigBinding)
        {
            ResolveBindings();
        }
    }
}
