using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Taxonomy;
using Convai.Runtime.Animation;
using UnityEngine;

namespace Convai.Modules.Emotion.Outputs
{
    /// <summary>
    ///     Strategy interface for projecting composed emotion scores onto a concrete output
    ///     target (blendshapes, animator parameters, shaders, etc.).
    /// </summary>
    /// <remarks>
    ///     Bindings are authored on the <c>ConvaiEmotionProfile</c> and consumed by
    ///     <c>ConvaiEmotionController</c>. Multiple bindings can coexist, so a character can
    ///     simultaneously drive blendshapes and animator parameters.
    /// </remarks>
    public interface IEmotionOutputBinding
    {
        /// <summary>
        ///     Called once when the owning controller initializes or whenever the profile
        ///     changes. Implementations should cache resolved targets here and register any
        ///     animator parameters via the supplied <paramref name="conductor" />.
        /// </summary>
        void Bind(
            Object owner,
            IEmotionTaxonomy taxonomy,
            IStandardRigBinding rig,
            AnimatorConductor conductor,
            FacialBlendshapeCompositorHost compositor);

        /// <summary>Writes the current frame's scores to the bound output.</summary>
        void Apply(
            IReadOnlyDictionary<string, float> scores,
            float neutralAlternationFactor);

        /// <summary>Releases any owned resources / animator registrations.</summary>
        void Unbind(Object owner);
    }
}
