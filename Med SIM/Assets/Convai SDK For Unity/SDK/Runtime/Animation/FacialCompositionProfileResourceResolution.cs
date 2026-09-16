using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convai.Runtime.Animation
{
    /// <summary>
    ///     Resolves the packaged fallback <see cref="ConvaiFacialCompositionProfile" /> from
    ///     Unity <c>Resources</c> using an ordered candidate list, with an optional integrator hook.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Candidate paths are relative to any <c>Resources</c> folder in the project (Unity
    ///         merges them). Order is significant: the first successful load wins.
    ///     </para>
    ///     <para>
    ///         Prefer assigning a profile explicitly on <see cref="Convai.Runtime.Embodiment.EmbodimentContext" />
    ///         or <see cref="FacialBlendshapeCompositorHost.SetCompositionProfile" />; this type
    ///         exists only for the automatic fallback when nothing is wired.
    ///     </para>
    /// </remarks>
    public static class FacialCompositionProfileResourceResolution
    {
        /// <summary>
        ///     Optional integrator hook evaluated before built-in <c>Resources</c> candidates.
        ///     Return <c>null</c> to fall through. Must be cheap and thread-safe with Unity's main-thread
        ///     rules (Unity API usage only on the main thread).
        /// </summary>
        public static Func<ConvaiFacialCompositionProfile> TryResolveCustomDefaultProfile;

        private static readonly string[] BuiltInResourceRelativePaths =
        {
            // Shipped with the package under SamplesShared/Resources/Embodiment/
            "Embodiment/ConvaiFacialCompositionProfile_Default",
            // Compatibility locations (older checkouts or integrator copies)
            "Convai/ConvaiFacialCompositionProfile_Default",
            "ConvaiFacialCompositionProfile_Default"
        };

        /// <summary>Built-in <c>Resources</c> paths tried in order after the optional custom hook.</summary>
        public static IReadOnlyList<string> BuiltInDefaultProfileResourceCandidates { get; } =
            Array.AsReadOnly(BuiltInResourceRelativePaths);

        /// <summary>First built-in candidate (stable primary path for docs and diagnostics).</summary>
        public static string PrimaryBuiltInResourceRelativePath => BuiltInResourceRelativePaths[0];

        /// <summary>
        ///     Loads the first available default profile: custom hook (if any), then each built-in
        ///     candidate path via <c>Resources.Load&lt;ConvaiFacialCompositionProfile&gt;(...)</c>.
        /// </summary>
        public static ConvaiFacialCompositionProfile TryLoadDefaultProfileFromResources()
        {
            if (TryResolveCustomDefaultProfile != null)
            {
                try
                {
                    ConvaiFacialCompositionProfile custom = TryResolveCustomDefaultProfile.Invoke();
                    if (custom != null)
                        return custom;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            IReadOnlyList<string> candidates = BuiltInDefaultProfileResourceCandidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                ConvaiFacialCompositionProfile loaded =
                    Resources.Load<ConvaiFacialCompositionProfile>(candidates[i]);
                if (loaded != null)
                    return loaded;
            }

            return null;
        }
    }
}
