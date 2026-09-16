// Copyright (c) Convai. Licensed under the Convai SDK license. See LICENSE in the package root.

using System;
using UnityEngine;

namespace Convai.Sample.Camera
{
    /// <summary>
    ///     Configuration parameters for adaptive depth-of-field in <see cref="ConvaiOrbitCamera" />.
    ///     <para>
    ///         Defines distance thresholds and aperture ranges that control how DOF adapts to
    ///         camera zoom. Authored as a plain serializable struct for direct Inspector editing.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     Call <see cref="Validate" /> after authoring or before use to clamp values to safe ranges.
    /// </remarks>
    [Serializable]
    public struct OrbitDofSettings
    {
        [Header("Adaptive Depth of Field")]
        [Tooltip("Distance threshold (meters) for close-up DOF behavior. Below this, uses closeAperture.")]
        public float closeDistance;

        [Tooltip("Distance threshold (meters) for wide-angle DOF behavior. Above this, uses farAperture.")]
        public float farDistance;

        [Tooltip("Aperture (f-stop) at close distance. Lower values (e.g., f/1.4) create shallow DOF with background blur.")]
        public float closeAperture;

        [Tooltip("Aperture (f-stop) at far distance. Higher values (e.g., f/8.0) create deep DOF with sharp backgrounds.")]
        public float farAperture;

        [Header("Smoothing")]
        [Tooltip("Approximate time (seconds) for aperture to settle after zoom changes. Lower = snappier, higher = smoother.")]
        public float apertureSmoothTime;

        [Tooltip("Approximate time (seconds) for focus distance to settle. Typically faster than aperture.")]
        public float focusSmoothTime;

        [Header("Advanced")]
        [Tooltip("Offset added to calculated focus distance. Positive = focus behind pivot, negative = in front.")]
        public float focusBias;

        [Tooltip("Minimum allowed aperture (f-stop). Prevents extreme shallow DOF artifacts.")]
        public float minAperture;

        [Tooltip("Maximum allowed aperture (f-stop). Prevents complete loss of depth separation.")]
        public float maxAperture;

        /// <summary>
        ///     Production-safe defaults for cinematic character focus.
        ///     <para>
        ///         Tuned for face sharpness at all distances. Close-up uses narrow aperture
        ///         (f/8) to keep entire face sharp. Far uses wider aperture (f/2.8) for
        ///         background separation while maintaining subject clarity.
        ///     </para>
        ///     <para>
        ///         Matches Showcase HDRP team spec: aperture 1.2-8.0 range, focus distance
        ///         always equals camera distance for pivot sharpness.
        ///     </para>
        /// </summary>
        public static OrbitDofSettings Default => new OrbitDofSettings
        {
            closeDistance = 0.5f,  // Very close to face
            farDistance = 2.5f,    // Max conversation distance
            closeAperture = 2.8f,  // Used at FAR (background bokeh)
            farAperture = 8.0f,    // Used at CLOSE (face sharp)
            apertureSmoothTime = 0.3f,
            focusSmoothTime = 0.2f,
            focusBias = 0f,
            minAperture = 1.2f,    // Showcase team minimum
            maxAperture = 8.0f     // Showcase team maximum
        };

        /// <summary>
        ///     Normalizes and clamps all fields to safe ranges.
        ///     Call after authoring in Inspector or before supplying to controllers.
        /// </summary>
        public void Validate()
        {
            // Ensure close distance is positive
            closeDistance = Mathf.Max(0.1f, closeDistance);

            // Ensure far distance is greater than close distance
            farDistance = Mathf.Max(closeDistance + 0.01f, farDistance);

            // Clamp apertures to physically plausible range
            closeAperture = Mathf.Clamp(closeAperture, 0.7f, 22f);
            farAperture = Mathf.Clamp(farAperture, 0.7f, 22f);

            // Ensure min/max aperture bounds are valid
            minAperture = Mathf.Max(0.7f, minAperture);
            maxAperture = Mathf.Max(minAperture, maxAperture);
            maxAperture = Mathf.Min(22f, maxAperture);

            // Clamp calculated apertures within min/max bounds
            closeAperture = Mathf.Clamp(closeAperture, minAperture, maxAperture);
            farAperture = Mathf.Clamp(farAperture, minAperture, maxAperture);

            // Ensure smooth times are non-negative
            apertureSmoothTime = Mathf.Max(0f, apertureSmoothTime);
            focusSmoothTime = Mathf.Max(0f, focusSmoothTime);

            // Focus bias is unrestricted (can be positive or negative)
        }
    }
}
