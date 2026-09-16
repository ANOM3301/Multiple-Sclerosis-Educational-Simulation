using System;
using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Modules.FacialAnimation.Core;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.FacialAnimation.Components
{
    /// <summary>
    ///     Plays a serialized <see cref="AnimationClip" /> cache at runtime by sampling baked
    ///     blendshape curves and submitting the result to a registered custom compositor layer.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use this source when the clip reference lives on the component and should work
    ///         in player builds without calling editor-only curve APIs. The editor serializes
    ///         the blendshape curve cache into this component. For reusable clip assets shared
    ///         across characters, prefer baking a <c>ConvaiFacialAnimationProfile</c> and using
    ///         <see cref="BakedFacialClipSource" /> for lower per-frame cost and cleaner preset
    ///         integration.
    ///     </para>
    ///     <para>
    ///         The component registers a custom compositor layer via
    ///         <see cref="FacialBlendshapeCompositorHost.RegisterCustomLayer" /> so its output
    ///         can be composed against the built-in layers without conflicting with baked
    ///         profiles that occupy the <c>FacialClipGeneral</c> / <c>FacialClipMouth</c>
    ///         layers.
    ///     </para>
    /// </remarks>
    [AddComponentMenu("Convai/Embodiment/Facial Clip Runtime Player")]
    [DisallowMultipleComponent]
    public sealed class ConvaiFacialClipRuntimePlayer : MonoBehaviour,
        IEmbodimentTickable,
        IFacialBlendshapeSource
    {
        [Serializable]
        private sealed class CachedCurveBinding
        {
            [SerializeField] private string blendshapeName;
            [SerializeField] private AnimationCurve curve;

            public CachedCurveBinding(string blendshapeName, AnimationCurve curve)
            {
                this.blendshapeName = blendshapeName ?? string.Empty;
                this.curve = CloneCurve(curve);
            }

            public string BlendshapeName => blendshapeName;
            public AnimationCurve Curve => curve;
        }

        [SerializeField]
        [Tooltip("Runtime clip to sample.")]
        private AnimationClip clip;

        [SerializeField, Range(0f, 2f)]
        private float playbackSpeed = 1f;

        [SerializeField]
        private bool loop = true;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Global weight scalar in [0,100].")]
        private float weightMultiplier = 100f;

        [SerializeField]
        [Tooltip("Explicit list of meshes to drive. Empty = auto-discover under character root.")]
        private List<SkinnedMeshRenderer> targetMeshes = new();

        [SerializeField, HideInInspector]
        private AnimationClip cachedClip;

        [SerializeField, HideInInspector]
        private List<CachedCurveBinding> cachedCurveBindings = new();

        private EmbodimentContext _context;
        private int _customLayerId = -1;
        private readonly List<RuntimeCurveBinding> _bindings = new();
        private readonly List<BlendshapeTargetKey> _submissionTargets = new();
        private readonly List<float> _submissionWeights = new();
        private float _time;
        private float _clipLength;
        private bool _rigBindingChangedHandlerRegistered;
        private bool _warnedMissingRuntimeCache;

        /// <inheritdoc />
        public Component SourceComponent => this;

        /// <inheritdoc />
        public string SourceName => "ConvaiFacialClipRuntimePlayer";

        /// <inheritdoc />
        EmbodimentTickPhase IEmbodimentTickable.Phase => EmbodimentTickPhase.Expression;

        /// <summary>
        ///     Assigns a clip and rebinds curves. In player builds the clip must match the
        ///     serialized cache authored for this component.
        /// </summary>
        public void SetClip(AnimationClip newClip)
        {
            clip = newClip;
            _time = 0f;
#if UNITY_EDITOR
            RebuildSerializedCurveCache();
#else
            if (!ReferenceEquals(cachedClip, clip))
            {
                if (cachedCurveBindings == null)
                    cachedCurveBindings = new List<CachedCurveBinding>();
                cachedCurveBindings.Clear();
            }
#endif
            ResolveBindings();
        }

        /// <summary>Re-resolves mesh targets and runtime curve bindings.</summary>
        public void Rebind() => ResolveBindings();

        private void OnEnable()
        {
            if (!EmbodimentContext.TryResolveFor(this, out _context))
            {
                enabled = false;
                return;
            }

            FacialBlendshapeCompositorHost compositor = _context.EnsureCompositor();
            if (compositor != null)
                _customLayerId = compositor.RegisterCustomLayer(SourceName);

            ResolveBindings();
            _context.RigBindingChanged += HandleRigBindingChanged;
            _rigBindingChangedHandlerRegistered = true;
            _context.EnsureTickScheduler()?.Register(this);
        }

        private void OnDisable()
        {
            if (_rigBindingChangedHandlerRegistered && _context != null)
            {
                _context.RigBindingChanged -= HandleRigBindingChanged;
                _rigBindingChangedHandlerRegistered = false;
            }

            _context?.TickScheduler?.Unregister(this);
            if (_context?.Compositor != null && _customLayerId >= 0)
            {
                _context.Compositor.UnregisterCustomLayer(_customLayerId);
                _customLayerId = -1;
            }
            _bindings.Clear();
        }

        void IEmbodimentTickable.EmbodimentTick(float deltaTime)
        {
            if (clip == null || _bindings.Count == 0 || _clipLength <= 0f) return;
            if (_context?.Compositor == null || _customLayerId < 0) return;

            _time += deltaTime * Mathf.Max(0f, playbackSpeed);
            if (loop) _time = Mathf.Repeat(_time, _clipLength);
            else _time = Mathf.Clamp(_time, 0f, _clipLength);

            float maxWeight = Mathf.Clamp(weightMultiplier, 0f, 100f);

            _submissionTargets.Clear();
            _submissionWeights.Clear();

            for (int i = 0; i < _bindings.Count; i++)
            {
                RuntimeCurveBinding b = _bindings[i];
                if (!b.Key.IsValid) continue;
                float sample = b.Curve != null ? b.Curve.Evaluate(_time) : 0f;
                float weight = Mathf.Clamp(sample, 0f, 100f) * (maxWeight / 100f);
                if (weight <= 0f) continue;

                _submissionTargets.Add(b.Key);
                _submissionWeights.Add(weight);
            }

            if (_submissionTargets.Count > 0)
                _context.Compositor.SubmitLayer(
                    this, _customLayerId, _submissionTargets, _submissionWeights, _submissionTargets.Count);
        }

        private void ResolveBindings()
        {
            _bindings.Clear();
            _clipLength = 0f;
            if (clip == null) return;
            _clipLength = clip.length;

#if UNITY_EDITOR
            if (!ReferenceEquals(cachedClip, clip) || cachedCurveBindings == null || cachedCurveBindings.Count == 0)
                RebuildSerializedCurveCache();
#endif

            IReadOnlyList<SkinnedMeshRenderer> meshes = DiscoverMeshes();
            if (cachedCurveBindings == null || cachedCurveBindings.Count == 0)
            {
                WarnMissingRuntimeCacheOnce();
                return;
            }

            for (int i = 0; i < cachedCurveBindings.Count; i++)
            {
                CachedCurveBinding binding = cachedCurveBindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.BlendshapeName)) continue;
                AnimationCurve curve = binding.Curve;
                if (curve == null) continue;

                for (int m = 0; m < meshes.Count; m++)
                {
                    SkinnedMeshRenderer mesh = meshes[m];
                    if (mesh == null || mesh.sharedMesh == null) continue;
                    int idx = mesh.sharedMesh.GetBlendShapeIndex(binding.BlendshapeName);
                    if (idx < 0) continue;
                    _bindings.Add(new RuntimeCurveBinding(new BlendshapeTargetKey(mesh, idx), curve));
                }
            }
        }

        private IReadOnlyList<SkinnedMeshRenderer> DiscoverMeshes()
        {
            if (targetMeshes != null && targetMeshes.Count > 0) return targetMeshes;
            IStandardRigBinding rigBinding = _context?.EnsureRigBinding();
            if (rigBinding != null &&
                rigBinding.FacialMeshes != null &&
                rigBinding.FacialMeshes.Count > 0)
            {
                return rigBinding.FacialMeshes;
            }

            Transform root = _context != null ? _context.CharacterRoot : transform;
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        private void HandleRigBindingChanged(IStandardRigBinding rigBinding)
        {
            ResolveBindings();
        }

        private void WarnMissingRuntimeCacheOnce()
        {
            if (_warnedMissingRuntimeCache) return;
            _warnedMissingRuntimeCache = true;
            Debug.LogWarning(
                "[ConvaiFacialClipRuntimePlayer] No serialized blendshape curve cache is available for this clip. " +
                "Assign the clip in the Inspector or call SetClip in the Editor so curve data is baked into the component before building.",
                this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildSerializedCurveCache();
        }

        private void RebuildSerializedCurveCache()
        {
            cachedClip = clip;
            if (cachedCurveBindings == null)
                cachedCurveBindings = new List<CachedCurveBinding>();
            cachedCurveBindings.Clear();

            if (clip == null) return;

            UnityEditor.EditorCurveBinding[] curveBindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < curveBindings.Length; i++)
            {
                UnityEditor.EditorCurveBinding ecb = curveBindings[i];
                if (string.IsNullOrEmpty(ecb.propertyName)) continue;
                if (!ecb.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)) continue;

                AnimationCurve curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, ecb);
                if (curve == null) continue;

                string blendshapeName = ecb.propertyName.Substring("blendShape.".Length);
                cachedCurveBindings.Add(new CachedCurveBinding(blendshapeName, curve));
            }
        }
#endif

        private static AnimationCurve CloneCurve(AnimationCurve source)
        {
            if (source == null)
                return null;

            return new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
        }

        private readonly struct RuntimeCurveBinding
        {
            public RuntimeCurveBinding(BlendshapeTargetKey key, AnimationCurve curve)
            {
                Key = key;
                Curve = curve;
            }

            public BlendshapeTargetKey Key { get; }
            public AnimationCurve Curve { get; }
        }
    }
}
