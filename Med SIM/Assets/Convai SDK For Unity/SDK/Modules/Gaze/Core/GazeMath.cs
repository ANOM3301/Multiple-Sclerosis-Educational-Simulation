using UnityEngine;

namespace Convai.Modules.Gaze.Core
{
    /// <summary>
    ///     Axis-convention-agnostic helpers for computing and applying gaze rotations on
    ///     humanoid rigs. The helpers operate in a stable <em>gaze reference frame</em>
    ///     (typically the rig root / animator transform with Unity-standard +Z forward /
    ///     +Y up / +X right axes) instead of the target bone's
    ///     <c>localRotation</c> basis, which on rigs exported from DCC tools (e.g.
    ///     Reallusion / CC4, iClone, MetaHuman) frequently carries large bind-pose rolls.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The rationale for routing all rotations through the root's world axes ;
    ///         rather than the bone's own <c>localRotation</c> axes ; is that
    ///         <see cref="Quaternion.AngleAxis" /> with a world-space axis produces a
    ///         rotation that is indifferent to any bind-pose roll baked into the bone
    ///         chain above the target. This sidesteps the infamous "eyes rolled skyward"
    ///         artefact on CC4 rigs where the eye bone's rest <see cref="Transform.localRotation" />
    ///         is a ~180 degrees quaternion that makes naive <c>AngleAxis(yaw, Vector3.up) * restLocal</c>
    ///         composition produce nonsense.
    ///     </para>
    ///     <para>
    ///         All functions are static and allocation-free; they are safe to call from
    ///         <see cref="MonoBehaviour.LateUpdate" /> hot paths.
    ///     </para>
    /// </remarks>
    internal static class GazeMath
    {
        private const float DirectionEpsilon = 1e-6f;

        /// <summary>
        ///     Computes the yaw/pitch angles (in degrees) required to rotate a reference
        ///     frame's forward direction so that it points along <paramref name="worldDirection" />.
        /// </summary>
        /// <param name="reference">
        ///     Reference transform whose local basis defines the frame in which yaw is a
        ///     rotation around the local +Y axis and pitch around the local +X axis.
        ///     Typically the rig root / animator transform.
        /// </param>
        /// <param name="worldDirection">World-space direction to resolve.</param>
        /// <param name="yawDegrees">Signed yaw; positive rotates the forward axis toward +X (right).</param>
        /// <param name="pitchDegrees">Signed pitch; positive tips the forward axis toward -Y (nose-down).</param>
        /// <returns><c>true</c> if the direction had sufficient magnitude to resolve angles.</returns>
        public static bool TryResolveAngles(
            Transform reference,
            Vector3 worldDirection,
            out float yawDegrees,
            out float pitchDegrees)
        {
            if (worldDirection.sqrMagnitude < DirectionEpsilon || reference == null)
            {
                yawDegrees = 0f;
                pitchDegrees = 0f;
                return false;
            }

            Vector3 localDir = reference.InverseTransformDirection(worldDirection.normalized);
            float sqrMag = localDir.sqrMagnitude;
            if (sqrMag < DirectionEpsilon)
            {
                yawDegrees = 0f;
                pitchDegrees = 0f;
                return false;
            }

            localDir *= 1f / Mathf.Sqrt(sqrMag);
            yawDegrees = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            pitchDegrees = -Mathf.Asin(Mathf.Clamp(localDir.y, -1f, 1f)) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>
        ///     Applies a yaw/pitch delta to a bone's rest-local rotation by rotating around
        ///     the <paramref name="reference" />'s <em>world-space</em> up and right axes
        ///     and then converting the result back into the bone's parent-local frame.
        /// </summary>
        /// <remarks>
        ///     This formulation is independent of any bind-pose roll in the bone or its
        ///     ancestors: rotating a world-space rotation around a world-space axis is
        ///     well-defined regardless of how the bone's <c>localRotation</c> was authored.
        /// </remarks>
        /// <param name="bone">The bone to rotate (writes <see cref="Transform.localRotation" />).</param>
        /// <param name="restLocal">The bone's rest-local rotation (captured once at bind time).</param>
        /// <param name="reference">Reference transform supplying the world-space up/right axes.</param>
        /// <param name="yawDegrees">Yaw offset in degrees (rotation around <see cref="Transform.up" />).</param>
        /// <param name="pitchDegrees">Pitch offset in degrees (rotation around <see cref="Transform.right" />).</param>
        public static void ApplyRotationAroundWorldAxes(
            Transform bone,
            Quaternion restLocal,
            Transform reference,
            float yawDegrees,
            float pitchDegrees)
        {
            if (bone == null || reference == null) return;
            Transform parent = bone.parent;

            Quaternion delta = Quaternion.AngleAxis(yawDegrees, reference.up) *
                               Quaternion.AngleAxis(pitchDegrees, reference.right);

            Quaternion restWorld = parent != null ? parent.rotation * restLocal : restLocal;
            Quaternion targetWorld = delta * restWorld;

            bone.localRotation = parent != null
                ? Quaternion.Inverse(parent.rotation) * targetWorld
                : targetWorld;
        }

        /// <summary>
        ///     Auto-detects which cardinal axis of the <paramref name="bone" />'s local
        ///     frame corresponds to its "look" direction at rest, by finding the axis
        ///     whose world-space projection (given the current parent pose + rest rotation)
        ///     is most aligned with <paramref name="worldForwardReference" />.
        /// </summary>
        /// <remarks>
        ///     Used at bind time on eye bones: Reallusion/CC4 rigs author eye bones with
        ///     an ~180 degrees rest rotation, so the eye's "look" axis in its local frame is
        ///     typically <see cref="Vector3.down" /> (-Y) rather than <see cref="Vector3.forward" />.
        ///     Detecting this once removes the need for a per-rig configuration asset.
        /// </remarks>
        /// <param name="bone">Bone whose local forward axis is being detected.</param>
        /// <param name="restLocal">Rest-local rotation of the bone at bind time.</param>
        /// <param name="worldForwardReference">
        ///     World-space direction that represents "forward" for the character (typically
        ///     the gaze reference transform's <see cref="Transform.forward" />).
        /// </param>
        /// <returns>
        ///     One of the six cardinal unit vectors (+/-X, +/-Y, +/-Z) expressed in the bone's
        ///     local frame ; the axis that, when rotated by <paramref name="restLocal" />
        ///     and the parent's world rotation, points closest to the reference forward.
        /// </returns>
        public static Vector3 DetectLocalForwardAxis(
            Transform bone,
            Quaternion restLocal,
            Vector3 worldForwardReference)
        {
            if (bone == null || worldForwardReference.sqrMagnitude < DirectionEpsilon)
                return Vector3.forward;

            Transform parent = bone.parent;
            Quaternion restWorld = parent != null ? parent.rotation * restLocal : restLocal;
            Vector3 reference = worldForwardReference.normalized;

            Vector3[] candidates =
            {
                Vector3.forward, Vector3.back,
                Vector3.right, Vector3.left,
                Vector3.up, Vector3.down
            };

            Vector3 best = Vector3.forward;
            float bestDot = float.MinValue;
            for (int i = 0; i < candidates.Length; i++)
            {
                float dot = Vector3.Dot(restWorld * candidates[i], reference);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = candidates[i];
                }
            }
            return best;
        }
    }
}
