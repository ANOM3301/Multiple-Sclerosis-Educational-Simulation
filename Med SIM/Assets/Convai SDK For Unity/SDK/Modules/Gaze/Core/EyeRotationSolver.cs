using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Gaze.Core
{
    /// <summary>
    ///     Per-eye ownership record carried by <see cref="ConvaiEyeGazeActuator" />: the bone
    ///     reference, its bind-pose rest rotation, the auto-detected look axis, and the most
    ///     recent base / written rotations used to detect ownership conflicts with the
    ///     animator pass.
    /// </summary>
    internal struct EyeBoneOwnership
    {
        public Transform Bone;
        public Quaternion RestLocal;
        public Vector3 LocalForward;
        public Quaternion LastBaseLocal;
        public Quaternion LastWrittenLocal;
        public bool HasLastWrite;

        /// <summary>
        ///     Captures bone + auto-detected look axis. Resets ownership tracking so the next
        ///     write samples a fresh base pose.
        /// </summary>
        public void Bind(Transform bone, Vector3 characterForward)
        {
            Bone = bone;
            RestLocal = bone != null ? bone.localRotation : Quaternion.identity;
            LocalForward = bone != null
                ? GazeMath.DetectLocalForwardAxis(bone, RestLocal, characterForward)
                : Vector3.forward;
            HasLastWrite = false;
        }

        /// <summary>Clears all state (used on rig rebind / disable).</summary>
        public void Clear()
        {
            Bone = null;
            RestLocal = Quaternion.identity;
            LocalForward = Vector3.forward;
            LastBaseLocal = Quaternion.identity;
            LastWrittenLocal = Quaternion.identity;
            HasLastWrite = false;
        }

        /// <summary>
        ///     Adds the current rest-pose forward (in world space) into <paramref name="sum" />
        ///     and increments <paramref name="count" /> when the bone is bound.
        /// </summary>
        public void AccumulateRestForward(ref Vector3 sum, ref int count)
        {
            if (Bone == null) return;

            Transform parent = Bone.parent;
            Quaternion baseLocal = HasLastWrite && OwnershipEpsilon.Approximately(Bone.localRotation, LastWrittenLocal)
                ? LastBaseLocal
                : Bone.localRotation;
            Quaternion restWorld = parent != null ? parent.rotation * baseLocal : baseLocal;
            Vector3 forward = restWorld * LocalForward;
            if (forward.sqrMagnitude <= 1e-6f) return;

            sum += forward.normalized;
            count++;
        }

        /// <summary>Restores the bone to its bind-pose rotation when this component still owns it.</summary>
        public void RestoreToRestIfOwning()
        {
            if (Bone != null && HasLastWrite && OwnershipEpsilon.Approximately(Bone.localRotation, LastWrittenLocal))
                Bone.localRotation = RestLocal;
            HasLastWrite = false;
        }

        internal void RestoreToBaseIfOwning(Quaternion baseLocal)
        {
            if (Bone != null && HasLastWrite && OwnershipEpsilon.Approximately(Bone.localRotation, LastWrittenLocal))
                Bone.localRotation = baseLocal;
            HasLastWrite = false;
        }
    }

    /// <summary>
    ///     Stateless solver that rotates a single eye bone toward
    ///     <paramref name="smoothedDirectionWorld" /> within the profile's yaw/pitch clamps,
    ///     adding saccade and tremor offsets. All math is routed through the supplied
    ///     <paramref name="reference" /> frame so the result is immune to bind-pose roll on
    ///     DCC-exported rigs (CC4 / iClone / MetaHuman).
    /// </summary>
    internal static class EyeRotationSolver
    {
        public static Vector2 Apply(
            ref EyeBoneOwnership ownership,
            Transform reference,
            Vector3 smoothedDirectionWorld,
            ConvaiGazeEyeProfile profile,
            Vector2 saccadeOffset,
            float tremorYaw,
            float tremorPitch)
        {
            Transform eye = ownership.Bone;
            if (eye == null || profile == null) return Vector2.zero;
            Transform parent = eye.parent;
            if (parent == null) return Vector2.zero;

            Quaternion baseLocal = ownership.HasLastWrite
                && OwnershipEpsilon.Approximately(eye.localRotation, ownership.LastWrittenLocal)
                ? ownership.LastBaseLocal
                : eye.localRotation;
            Quaternion restWorld = parent.rotation * baseLocal;
            Vector3 restForwardWorld = restWorld * ownership.LocalForward;

            if (!GazeMath.TryResolveAngles(reference, restForwardWorld, out float restYaw, out float restPitch) ||
                !GazeMath.TryResolveAngles(reference, smoothedDirectionWorld, out float targetYaw, out float targetPitch))
            {
                ownership.RestoreToBaseIfOwning(baseLocal);
                return Vector2.zero;
            }

            float deltaYaw = Mathf.DeltaAngle(restYaw, targetYaw) + saccadeOffset.x + tremorYaw;
            float deltaPitch = Mathf.DeltaAngle(restPitch, targetPitch) + saccadeOffset.y + tremorPitch;

            deltaYaw = Mathf.Clamp(deltaYaw, -profile.MaxYawDegrees, profile.MaxYawDegrees);
            deltaPitch = Mathf.Clamp(deltaPitch, -profile.MaxPitchDegrees, profile.MaxPitchDegrees);

            GazeMath.ApplyRotationAroundWorldAxes(eye, baseLocal, reference, deltaYaw, deltaPitch);
            ownership.LastBaseLocal = baseLocal;
            ownership.LastWrittenLocal = eye.localRotation;
            ownership.HasLastWrite = true;
            return new Vector2(deltaYaw, deltaPitch);
        }
    }
}
