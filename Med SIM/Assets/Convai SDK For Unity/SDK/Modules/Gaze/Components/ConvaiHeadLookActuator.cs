using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Modules;
using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.Gaze.Core;
using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Gaze.Components
{
    /// <summary>
    ///     Applies yaw/pitch rotation to the head chain so the character visually
    ///     commits to the current <see cref="GazeIntent" /> target. Eye-only glances are
    ///     handled by <see cref="EyeGazeActuator" /> ; this component contributes head/neck
    ///     commitment during active gaze and subtle idle exploration when attention is relaxed.
    /// </summary>
    [AddComponentMenu("Convai/Embodiment/Head Look")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(EmbodimentExecutionOrders.HeadLook)]
    public sealed class ConvaiHeadLookActuator : EmbodimentProfileReceiver<ConvaiGazeHeadProfile>
    {
        private const float HeadAuthorityEpsilon = 0.0001f;
        private const float BoneWriteEpsilon = 0.0001f;

        private Transform _chest;
        private Transform _upperChest;
        private Transform _neck;
        private Transform _head;
        private Quaternion _lastChestBaseLocal;
        private Quaternion _lastUpperChestBaseLocal;
        private Quaternion _lastNeckBaseLocal;
        private Quaternion _lastHeadBaseLocal;
        private Quaternion _lastChestWrittenLocal;
        private Quaternion _lastUpperChestWrittenLocal;
        private Quaternion _lastNeckWrittenLocal;
        private Quaternion _lastHeadWrittenLocal;
        private bool _hasLastChestWrite;
        private bool _hasLastUpperChestWrite;
        private bool _hasLastNeckWrite;
        private bool _hasLastHeadWrite;

        private float _smoothedYaw;
        private float _smoothedPitch;
        private Vector2 _idleExplorationTargetAngles;
        private float _idleExplorationElapsed;
        private float _idleExplorationInterval;
        private DeterministicEmbodimentRandom _random;
        private bool _rigBindingChangedHandlerRegistered;

        public float CurrentHeadAuthority { get; private set; }
        public bool IsIdleExploring { get; private set; }
        public Vector2 CurrentSolvedAngles => new(_smoothedYaw, _smoothedPitch);

        /// <inheritdoc />
        protected override string ProfileModuleId => ModuleIds.GazeHead;

        /// <inheritdoc />
        protected override System.Func<ConvaiGazeHeadProfile> DefaultProfileFactory => ConvaiGazeHeadProfile.CreateDefault;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!enabled) return;

            GazeRuntimeBootstrap.EnsureCoordinator(Context, this);
            ResolveBones();
            _random = DeterministicEmbodimentRandom.Create(this, 0x484C4F4Fu);
            ResetIdleExplorationState();
            Context.RigBindingChanged += HandleRigBindingChanged;
            _rigBindingChangedHandlerRegistered = true;
        }

        protected override void OnDisable()
        {
            if (_rigBindingChangedHandlerRegistered && Context != null)
            {
                Context.RigBindingChanged -= HandleRigBindingChanged;
                _rigBindingChangedHandlerRegistered = false;
            }

            ReleaseBoneWrites();
            _smoothedYaw = 0f;
            _smoothedPitch = 0f;
            CurrentHeadAuthority = 0f;
            IsIdleExploring = false;
            ResetIdleExplorationState();

            base.OnDisable();
        }

        /// <remarks>
        ///     Head bones must be written in <c>LateUpdate</c> so the animator's humanoid pose
        ///     is applied first. All rotations route through the character root's world-space
        ///     axes via <see cref="GazeMath" /> to remain agnostic of per-rig bind-pose roll.
        /// </remarks>
        private void LateUpdate()
        {
            if (Context == null) return;
            if (_head == null && _neck == null && _chest == null && _upperChest == null) return;

            GazeIntent intent = Context.GazeIntentProvider?.Current ?? GazeIntent.Relaxed;
            ConvaiGazeHeadProfile p = EffectiveProfile;
            float deltaTime = Time.deltaTime > 0f ? Time.deltaTime : 1f / 60f;

            // EyeShare should bias the solve toward the eyes, but not starve the neck/head
            // completely; otherwise attentive states feel frozen even while gaze is engaged.
            // Authors can override the linear default with a profile curve.
            float headBlend = p.EvaluateHeadBlend(1f - intent.EyeShare);
            float headAuthority = Mathf.Clamp01(intent.OverallWeight * headBlend);
            CurrentHeadAuthority = headAuthority;

            bool hasActiveTarget = TryResolveActiveTarget(intent, p, headAuthority, out float targetYaw, out float targetPitch);
            IsIdleExploring = !hasActiveTarget && p.EnableIdleExploration && p.IdleExplorationWeight > 0f;
            if (IsIdleExploring)
            {
                UpdateIdleExploration(p, deltaTime);
                targetYaw = _idleExplorationTargetAngles.x * p.IdleExplorationWeight;
                targetPitch = _idleExplorationTargetAngles.y * p.IdleExplorationWeight;
            }

            float sharpness = hasActiveTarget
                ? p.SmoothingSharpness
                : IsIdleExploring ? p.IdleSharpness : p.ReturnSharpness;
            _smoothedYaw = HeadLookMotionSolver.SmoothAngle(
                _smoothedYaw, targetYaw, sharpness, p.MaxYawSpeedDegrees, deltaTime);
            _smoothedPitch = HeadLookMotionSolver.SmoothAngle(
                _smoothedPitch, targetPitch, sharpness, p.MaxPitchSpeedDegrees, deltaTime);

            ApplyOrRelease(p, _smoothedYaw, _smoothedPitch);
        }

        private bool TryResolveActiveTarget(
            in GazeIntent intent,
            ConvaiGazeHeadProfile p,
            float headAuthority,
            out float yaw,
            out float pitch)
        {
            yaw = 0f;
            pitch = 0f;

            if (headAuthority <= HeadAuthorityEpsilon)
                return false;

            Transform pivot = _head != null ? _head : _neck != null ? _neck : _upperChest != null ? _upperChest : _chest;
            Vector3 pivotPosition = pivot != null ? pivot.position : transform.position;
            Vector3 toTargetWorld = intent.WorldTargetPoint - pivotPosition;
            Transform reference = GazeReferenceFrame.Resolve(Context, transform);

            if (!GazeMath.TryResolveAngles(reference, toTargetWorld, out yaw, out pitch))
                return false;

            // Character root's +Z is the "natural" forward ; the head's rest-forward
            // approximately aligns with it on properly authored humanoids, so the yaw
            // computed here IS the signed angle we want to apply relative to rest.

            float deadzone = p.DeadzoneDegrees;
            if (Mathf.Abs(yaw) < deadzone) yaw = 0f;
            if (Mathf.Abs(pitch) < deadzone) pitch = 0f;

            yaw *= headAuthority;
            pitch *= headAuthority;
            return true;
        }

        private void UpdateIdleExploration(ConvaiGazeHeadProfile p, float deltaTime)
        {
            _idleExplorationElapsed += deltaTime;
            if (_idleExplorationElapsed < _idleExplorationInterval) return;

            _idleExplorationElapsed = 0f;
            _idleExplorationInterval = SampleIdleExplorationInterval(p);
            _idleExplorationTargetAngles = SampleIdleExplorationTarget(p);
        }

        private void ApplyOrRelease(ConvaiGazeHeadProfile p, float yaw, float pitch)
        {
            float threshold = p.HeadReturnThresholdDegrees;
            if (Mathf.Abs(yaw) <= threshold &&
                Mathf.Abs(pitch) <= threshold)
            {
                _smoothedYaw = 0f;
                _smoothedPitch = 0f;
                ReleaseBoneWrites();
                return;
            }

            ApplyDistribution(p, yaw, pitch);
        }

        private void ApplyDistribution(ConvaiGazeHeadProfile p, float yaw, float pitch)
        {
            HeadLookDistribution distribution = HeadLookMotionSolver.SolveDistribution(
                p,
                yaw,
                pitch,
                _chest != null,
                _upperChest != null);

            // Rotations are applied around the CHARACTER ROOT's world-space up/right
            // axes ; NOT the bone's own local axes ; so they remain invariant under any
            // bind-pose roll authored into the neck/head bone (common on CC4/iClone
            // rigs where neck/head carry ~180 degrees rest rotations).
            Transform reference = GazeReferenceFrame.Resolve(Context, transform);
            ApplyAdditiveRotation(
                _chest,
                reference,
                distribution.ChestYaw,
                distribution.ChestPitch,
                ref _hasLastChestWrite,
                ref _lastChestBaseLocal,
                ref _lastChestWrittenLocal);
            ApplyAdditiveRotation(
                _upperChest,
                reference,
                distribution.UpperChestYaw,
                distribution.UpperChestPitch,
                ref _hasLastUpperChestWrite,
                ref _lastUpperChestBaseLocal,
                ref _lastUpperChestWrittenLocal);
            ApplyAdditiveRotation(
                _neck,
                reference,
                distribution.NeckYaw,
                distribution.NeckPitch,
                ref _hasLastNeckWrite,
                ref _lastNeckBaseLocal,
                ref _lastNeckWrittenLocal);
            ApplyAdditiveRotation(
                _head,
                reference,
                distribution.HeadYaw,
                distribution.HeadPitch,
                ref _hasLastHeadWrite,
                ref _lastHeadBaseLocal,
                ref _lastHeadWrittenLocal);
        }

        private void ResolveBones()
        {
            _chest = null;
            _upperChest = null;
            _neck = null;
            _head = null;
            IStandardRigBinding rigBinding = Context?.EnsureRigBinding();
            if (rigBinding == null) return;

            if (rigBinding.TryGetBone(StandardBone.Chest, out Transform chest))
            {
                _chest = chest;
                _hasLastChestWrite = false;
            }
            if (rigBinding.TryGetBone(StandardBone.UpperChest, out Transform upperChest))
            {
                _upperChest = upperChest;
                _hasLastUpperChestWrite = false;
            }
            if (rigBinding.TryGetBone(StandardBone.Neck, out Transform neck))
            {
                _neck = neck;
                _hasLastNeckWrite = false;
            }
            if (rigBinding.TryGetBone(StandardBone.Head, out Transform head))
            {
                _head = head;
                _hasLastHeadWrite = false;
            }
        }

        private void HandleRigBindingChanged(IStandardRigBinding rigBinding)
        {
            ReleaseBoneWrites();
            _smoothedYaw = 0f;
            _smoothedPitch = 0f;
            CurrentHeadAuthority = 0f;
            IsIdleExploring = false;
            ResetIdleExplorationState();
            ResolveBones();
        }

        private void ReleaseBoneWrites()
        {
            RestoreIfStillOwning(_chest, _lastChestBaseLocal, ref _hasLastChestWrite, _lastChestWrittenLocal);
            RestoreIfStillOwning(_upperChest, _lastUpperChestBaseLocal, ref _hasLastUpperChestWrite, _lastUpperChestWrittenLocal);
            RestoreIfStillOwning(_neck, _lastNeckBaseLocal, ref _hasLastNeckWrite, _lastNeckWrittenLocal);
            RestoreIfStillOwning(_head, _lastHeadBaseLocal, ref _hasLastHeadWrite, _lastHeadWrittenLocal);
        }

        private void ResetIdleExplorationState()
        {
            _idleExplorationTargetAngles = Vector2.zero;
            _idleExplorationElapsed = float.PositiveInfinity;
            _idleExplorationInterval = 0f;
        }

        private float SampleIdleExplorationInterval(ConvaiGazeHeadProfile p) =>
            _random.Range(p.IdleExplorationIntervalMin, p.IdleExplorationIntervalMax);

        private Vector2 SampleIdleExplorationTarget(ConvaiGazeHeadProfile p)
        {
            if (_random.Value < p.IdleRecenteringChance)
                return Vector2.zero;

            return new Vector2(
                SampleBiasedSigned(p.IdleExplorationYawDegrees, p.IdleExplorationYawDegrees, p.IdleExplorationCenterBias),
                SampleBiasedSigned(p.IdleExplorationUpDegrees, p.IdleExplorationDownDegrees, p.IdleExplorationCenterBias));
        }

        private float SampleBiasedSigned(float negativeMagnitude, float positiveMagnitude, float centerBias)
        {
            bool positive = _random.Value >= 0.5f;
            float max = positive ? positiveMagnitude : negativeMagnitude;
            float exponent = Mathf.Lerp(1f, 3f, Mathf.Clamp01(centerBias));
            float magnitude = Mathf.Pow(_random.Value, exponent) * max;
            return positive ? magnitude : -magnitude;
        }

        private static void ApplyAdditiveRotation(
            Transform bone,
            Transform reference,
            float yaw,
            float pitch,
            ref bool hasLastWrite,
            ref Quaternion lastBaseLocal,
            ref Quaternion lastWrittenLocal)
        {
            if (bone == null) return;
            if (Mathf.Abs(yaw) <= BoneWriteEpsilon && Mathf.Abs(pitch) <= BoneWriteEpsilon)
            {
                RestoreIfStillOwning(bone, lastBaseLocal, ref hasLastWrite, lastWrittenLocal);
                return;
            }

            // Layer the current look intent over the Animator pose once per frame. If the
            // Animator did not touch this bone after our previous write, the current transform
            // is still our old procedural result, so reuse the base pose captured last frame.
            // This avoids both bind-pose snapping and cumulative rotation drift.
            Quaternion baseLocal = hasLastWrite && OwnershipEpsilon.Approximately(bone.localRotation, lastWrittenLocal)
                ? lastBaseLocal
                : bone.localRotation;

            GazeMath.ApplyRotationAroundWorldAxes(bone, baseLocal, reference, yaw, pitch);
            lastBaseLocal = baseLocal;
            lastWrittenLocal = bone.localRotation;
            hasLastWrite = true;
        }

        private static void RestoreIfStillOwning(
            Transform bone,
            Quaternion restLocal,
            ref bool hasLastWrite,
            Quaternion lastWrittenLocal)
        {
            if (bone != null && hasLastWrite && OwnershipEpsilon.Approximately(bone.localRotation, lastWrittenLocal))
                bone.localRotation = restLocal;

            hasLastWrite = false;
        }
    }
}
