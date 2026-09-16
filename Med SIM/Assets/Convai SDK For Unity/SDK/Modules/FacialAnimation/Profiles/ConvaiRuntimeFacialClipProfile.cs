using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convai.Modules.FacialAnimation.Profiles
{
    /// <summary>
    ///     Authoring asset for the runtime facial clip player. Captures the source clip plus
    ///     the precomputed blendshape curve cache so player builds can play the clip without
    ///     calling editor-only animation APIs.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The cache is populated at author time (Editor) when the inspector calls
    ///         <see cref="RebuildCache" /> via the Editor pipeline. Runtime players read the
    ///         cache verbatim; mutating it in a player build has no effect.
    ///     </para>
    ///     <para>
    ///         Playback parameters (speed, loop, weight) live on the receiver component, not
    ///         the profile, so multiple characters can share a single profile asset while
    ///         keeping their per-character playback tuning local.
    ///     </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Convai/Embodiment/Runtime Facial Clip Profile", fileName = "ConvaiRuntimeFacialClipProfile")]
    public sealed class ConvaiRuntimeFacialClipProfile : ScriptableObject
    {
        [Serializable]
        public sealed class CachedCurveBinding
        {
            [SerializeField] private string blendshapeName;
            [SerializeField] private AnimationCurve curve;

            public CachedCurveBinding(string blendshapeName, AnimationCurve curve)
            {
                this.blendshapeName = blendshapeName ?? string.Empty;
                this.curve = curve != null
                    ? new AnimationCurve(curve.keys)
                    {
                        preWrapMode = curve.preWrapMode,
                        postWrapMode = curve.postWrapMode
                    }
                    : null;
            }

            public string BlendshapeName => blendshapeName;
            public AnimationCurve Curve => curve;
        }

        [SerializeField, Tooltip("Source clip baked into the curve cache.")]
        private AnimationClip clip;

        [SerializeField, HideInInspector]
        private List<CachedCurveBinding> cachedCurveBindings = new();

        public AnimationClip Clip => clip;
        public IReadOnlyList<CachedCurveBinding> CachedCurveBindings => cachedCurveBindings;

        /// <summary>Authoring-side replacement of the cached curve list (Editor entry point).</summary>
        public void SetCache(AnimationClip sourceClip, IEnumerable<CachedCurveBinding> bindings)
        {
            clip = sourceClip;
            cachedCurveBindings ??= new List<CachedCurveBinding>();
            cachedCurveBindings.Clear();
            if (bindings == null) return;
            foreach (CachedCurveBinding binding in bindings)
                if (binding != null) cachedCurveBindings.Add(binding);
        }

        /// <summary>Factory used by the receiver when no profile asset is assigned.</summary>
        public static ConvaiRuntimeFacialClipProfile CreateDefault()
        {
            ConvaiRuntimeFacialClipProfile profile = CreateInstance<ConvaiRuntimeFacialClipProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.name = "ConvaiRuntimeFacialClipProfile (Runtime Default)";
            return profile;
        }
    }
}
