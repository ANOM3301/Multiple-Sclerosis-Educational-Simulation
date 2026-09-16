using Convai.Runtime.Animation;
using Convai.Modules.FacialAnimation.Profiles;
using UnityEngine;

namespace Convai.Modules.FacialAnimation.Core
{
    /// <summary>
    ///     Runtime-resolved binding that pairs a <see cref="ConvaiFacialAnimationProfile.CurveBinding" />
    ///     (referenced indirectly by index) with a specific blendshape target on a specific
    ///     <see cref="SkinnedMeshRenderer" />. The actuator holds a flat array of these so each
    ///     per-frame evaluation is a simple indexed loop.
    /// </summary>
    internal readonly struct ResolvedFacialBinding
    {
        public ResolvedFacialBinding(
            BlendshapeTargetKey key,
            AnimationCurve curve,
            bool isMouth,
            ConvaiFacialAnimationProfile.BindingWeightSettings weightSettings)
        {
            Key = key;
            Curve = curve;
            IsMouth = isMouth;
            WeightSettings = weightSettings;
        }

        public BlendshapeTargetKey Key { get; }
        public AnimationCurve Curve { get; }
        public bool IsMouth { get; }
        public ConvaiFacialAnimationProfile.BindingWeightSettings WeightSettings { get; }
    }
}
