using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Attention.Providers
{
    /// <summary>
    ///     Out-of-the-box focus provider that targets the main camera (or an explicit
    ///     transform) so the character always has a valid gaze anchor for the player.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This component is useful for first-person and third-person single-player
    ///         scenarios. Advanced integrators can
    ///         replace it with their own <see cref="IFocusTargetProvider" /> implementations -
    ///         the attention director does not care which provider supplies the candidate as
    ///         long as the <see cref="AttentionCandidate" /> contract is satisfied.
    ///     </para>
    /// </remarks>
    [AddComponentMenu("Convai/Embodiment/Focus Target Provider (Default)")]
    [DisallowMultipleComponent]
    [EmbodimentComponentBranding("Default Focus Target Provider", "Player Anchor")]
    public sealed class DefaultFocusTargetProvider : MonoBehaviour, IFocusTargetProvider
    {
        [SerializeField, Tooltip("Static priority compared across providers when relevance ties break.")]
        private int priority = 0;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Base relevance reported when the target is visible and in range.")]
        private float baseRelevance = 0.8f;

        [SerializeField]
        [Tooltip("Explicit target. When left empty, Camera.main is used.")]
        private Transform explicitTarget;

        [SerializeField, Min(0f)]
        [Tooltip("Target point is offset upwards by this much to aim at the player's face rather than feet.")]
        private float targetHeadHeight = 1.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Distance (meters) beyond which relevance fades to zero.")]
        private float maxDistance = 8f;

        [SerializeField, Min(0f)]
        [Tooltip("Distance (meters) below which relevance is at its maximum.")]
        private float fullRelevanceDistance = 3f;

        private Transform _cachedCameraTransform;

        private static Camera[] _cameraScratch = System.Array.Empty<Camera>();

        /// <inheritdoc />
        public int Priority => priority;

        public void Configure(
            float configuredBaseRelevance,
            float configuredTargetHeadHeight,
            float configuredMaxDistance,
            float configuredFullRelevanceDistance)
        {
            baseRelevance = Mathf.Clamp01(configuredBaseRelevance);
            targetHeadHeight = Mathf.Max(0f, configuredTargetHeadHeight);
            maxDistance = Mathf.Max(0f, configuredMaxDistance);
            fullRelevanceDistance = Mathf.Max(0f, configuredFullRelevanceDistance);
        }

        /// <inheritdoc />
        public bool TryGetCandidate(Transform characterRoot, out AttentionCandidate candidate)
        {
            candidate = default;

            Transform target = ResolveTarget();
            if (target == null) return false;

            // Camera transforms already sit at eye-level, but explicit targets usually point
            // at a player/root transform; lift such targets by the configured head height so
            // gaze lands on the implied eye-line rather than the feet.
            Vector3 worldPoint = target.position;
            if (explicitTarget != null && targetHeadHeight > 0f)
                worldPoint += Vector3.up * targetHeadHeight;

            float distance = characterRoot != null
                ? Vector3.Distance(characterRoot.position, worldPoint)
                : 0f;

            float relevance = ComputeRelevance(distance);
            if (relevance <= 0f) return false;

            candidate = new AttentionCandidate(
                priority: priority,
                relevance: relevance,
                target: target,
                worldPoint: worldPoint,
                debugName: explicitTarget != null ? explicitTarget.name : "MainCamera");
            return true;
        }

        private Transform ResolveTarget()
        {
            if (explicitTarget != null) return explicitTarget;

            Camera cam = Camera.main;
            if (cam != null && !ReferenceEquals(_cachedCameraTransform, cam.transform))
            {
                _cachedCameraTransform = cam.transform;
            }
            else if (cam == null && _cachedCameraTransform == null)
            {
                _cachedCameraTransform = ResolveFirstEnabledCamera();
            }
            else if (_cachedCameraTransform != null &&
                     !IsUsableCachedTarget(_cachedCameraTransform))
            {
                _cachedCameraTransform = null;
            }

            return _cachedCameraTransform;
        }

        private static bool IsUsableCachedTarget(Transform candidate)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                return false;

            Camera camera = candidate.GetComponent<Camera>();
            if (camera != null)
                return camera.isActiveAndEnabled;

            return candidate.CompareTag("MainCamera");
        }

        private static Transform ResolveFirstEnabledCamera()
        {
            int count = Camera.allCamerasCount;
            if (count <= 0) return null;

            if (_cameraScratch.Length < count)
                _cameraScratch = new Camera[Mathf.NextPowerOfTwo(count)];

            Camera.GetAllCameras(_cameraScratch);
            for (int i = 0; i < count; i++)
            {
                Camera candidate = _cameraScratch[i];
                if (candidate != null && candidate.isActiveAndEnabled)
                {
                    System.Array.Clear(_cameraScratch, 0, count);
                    return candidate.transform;
                }
            }

            System.Array.Clear(_cameraScratch, 0, count);
            return null;
        }

        private float ComputeRelevance(float distance)
        {
            if (maxDistance <= 0f) return baseRelevance;
            if (distance <= fullRelevanceDistance) return baseRelevance;
            if (distance >= maxDistance) return 0f;

            float t = 1f - Mathf.InverseLerp(fullRelevanceDistance, maxDistance, distance);
            return baseRelevance * t;
        }
    }
}
