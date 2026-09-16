// Copyright (c) Convai. Licensed under the Convai SDK license. See LICENSE in the package root.

using UnityEngine;

namespace Convai.Sample.Camera
{
    /// <summary>
    ///     Pure calculation utilities for adaptive depth-of-field.
    ///     <para>
    ///         Computes aperture (f-stop) and focus distance from camera orbit distance using
    ///         smooth interpolation curves. Inspired by film-style distance-based exposure control.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     All methods are static and allocation-free. Designed to be called each frame without
    ///     garbage collection overhead.
    /// </remarks>
    public static class OrbitDofMath
    {
        /// <summary>
        ///     Result of adaptive DOF calculation containing aperture and focus distance.
        /// </summary>
        public struct AdaptiveDofResult
        {
            /// <summary>Calculated aperture (f-stop). Lower values = shallower depth of field.</summary>
            public float Aperture;

            /// <summary>Calculated focus distance in meters from camera to in-focus plane.</summary>
            public float FocusDistance;
        }

        /// <summary>
        ///     Calculates adaptive aperture and focus distance from camera orbit distance.
        /// </summary>
        /// <param name="cameraDistance">Current camera orbit distance in meters.</param>
        /// <param name="closeDistance">Distance threshold for close-up behavior (e.g., 0.78m).</param>
        /// <param name="farDistance">Distance threshold for wide-angle behavior (e.g., 2.5m).</param>
        /// <param name="closeAperture">Aperture at close distance (e.g., f/1.4 for shallow DOF).</param>
        /// <param name="farAperture">Aperture at far distance (e.g., f/8.0 for deep DOF).</param>
        /// <returns>Calculated aperture and focus distance.</returns>
        /// <example>
        ///     <code>
        ///     AdaptiveDofResult dof = OrbitDofMath.Evaluate(
        ///         cameraDistance: 1.2f,
        ///         closeDistance: 0.78f,
        ///         farDistance: 2.5f,
        ///         closeAperture: 1.4f,
        ///         farAperture: 8.0f);
        ///     // dof.Aperture ≈ 2.8f (mid-range)
        ///     // dof.FocusDistance = 1.2f (matches camera distance)
        ///     </code>
        /// </example>
        public static AdaptiveDofResult Evaluate(
            float cameraDistance,
            float closeDistance,
            float farDistance,
            float closeAperture,
            float farAperture)
        {
            // Sanitize inputs to prevent division by zero and ensure valid ranges
            float sanitizedCloseDistance = Mathf.Max(0.1f, closeDistance);
            float sanitizedFarDistance = Mathf.Max(sanitizedCloseDistance + 0.01f, farDistance);
            float sanitizedCameraDistance = Mathf.Max(0.1f, cameraDistance);

            // Normalize camera distance to [0, 1] range between close and far thresholds
            float t = Mathf.InverseLerp(sanitizedCloseDistance, sanitizedFarDistance, sanitizedCameraDistance);

            // Apply smooth easing for cinematic feel (S-curve)
            float smoothT = SmoothStep01(t);

            // Interpolate aperture: INVERTED for face sharpness
            // Close distance = NARROW aperture (f/8) = entire face sharp
            // Far distance = WIDER aperture (f/2.8) = background bokeh
            float aperture = Mathf.Lerp(
                Mathf.Max(0.7f, farAperture),  // SWAPPED: use far at close
                Mathf.Max(0.7f, closeAperture), // SWAPPED: use close at far
                smoothT);

            // Focus distance matches camera distance to keep pivot sharp
            float focusDistance = sanitizedCameraDistance;

            return new AdaptiveDofResult
            {
                Aperture = Mathf.Max(0.7f, aperture),
                FocusDistance = focusDistance
            };
        }

        /// <summary>
        ///     Smooth step interpolation (cubic Hermite) for value in [0, 1].
        ///     Produces S-curve easing: slow at start/end, fast in middle.
        /// </summary>
        /// <param name="value">Input value, will be clamped to [0, 1].</param>
        /// <returns>Smoothed value in [0, 1] using formula: t² * (3 - 2t).</returns>
        /// <remarks>
        ///     Common in graphics for smooth transitions. Equivalent to GLSL smoothstep().
        /// </remarks>
        private static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
