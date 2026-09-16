using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Modules;
using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.Gaze.Core;
using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using UnityEngine;

namespace Convai.Modules.Gaze.Components
{
    /// <summary>
    ///     Rotates the character's eye bones toward the <see cref="GazeIntent" /> target,
    ///     layered with saccade micro-movements, a resting tremor, and optional blinks
    ///     through the facial compositor.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         All work is performed in <see cref="LateUpdate" /> so the humanoid animator's
    ///         head pose is already composed; writing earlier would be silently overwritten
    ///         by the animator pass which runs between <c>Update</c> and <c>LateUpdate</c>.
    ///     </para>
    ///     <para>
    ///         The actuator uses <see cref="DefaultExecutionOrder" /> <c>19500</c> so its
    ///         blink submission runs before <see cref="FacialBlendshapeCompositorHost" />
    ///         (order <c>20000</c>) flushes the composed blendshape values to the meshes.
    ///         Per-frame work is delegated to dedicated POCOs in
    ///         <see cref="Convai.Modules.Gaze.Core" /> so this component stays a thin
    ///         composition root: bone resolution, lifecycle, and frame glue only.
    ///     </para>
    /// </remarks>
    [AddComponentMenu("Convai/Embodiment/Eye Gaze")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(EmbodimentExecutionOrders.EyeGaze)]
    public sealed class ConvaiEyeGazeActuator :
        EmbodimentProfileReceiver<ConvaiGazeEyeProfile>,
        IFacialBlendshapeSource
    {
        private readonly EyeSaccadeGenerator _saccade = new();
        private readonly EyeBlinkScheduler _blink = new();
        private readonly EyeIdleExplorationDirector _idleExploration = new();
        private readonly EyeBlendshapeWriter _blendshapeWriter = new();

        private EyeBoneOwnership _leftEye;
        private EyeBoneOwnership _rightEye;

        private Vector3 _smoothedEyeDirection = Vector3.forward;
        private DeterministicEmbodimentRandom _random;
        private bool _rigBindingChangedHandlerRegistered;

        /// <inheritdoc />
        public Component SourceComponent => this;

        /// <inheritdoc />
        public string SourceName => "ConvaiEyeGazeActuator";

        /// <inheritdoc />
        protected override string ProfileModuleId => ModuleIds.GazeEye;

        /// <inheritdoc />
        protected override System.Func<ConvaiGazeEyeProfile> DefaultProfileFactory =>
            ConvaiGazeEyeProfile.CreateDefault;

        /// <inheritdoc />
        protected override void OnProfileApplied(ConvaiGazeEyeProfile newProfile)
        {
            _saccade.Reset(newProfile);
            _blink.Reset(newProfile);
            _idleExploration.Reset(newProfile, Time.time, ref _random);
            _blendshapeWriter.ResetEyelidFollow();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!enabled) return;

            GazeRuntimeBootstrap.EnsureCoordinator(Context, this);
            _random = DeterministicEmbodimentRandom.Create(this);

            ResolveBones();
            if (_leftEye.Bone == null && _rightEye.Bone == null && UnityEngine.Application.isPlaying)
                Debug.LogWarning(
                    $"[ConvaiEyeGazeActuator] No eye bones resolved on '{name}'. " +
                    "Check that the rig convention is detected correctly (StandardRigBinding) " +
                    "and that the character has LeftEye / RightEye bones in its hierarchy.",
                    this);

            _blendshapeWriter.Bind(Context?.EnsureRigBinding());

            ConvaiGazeEyeProfile profile = EffectiveProfile;
            _saccade.Reset(profile);
            _blink.Reset(profile);
            _idleExploration.Reset(profile, Time.time, ref _random);
            _blendshapeWriter.ResetEyelidFollow();
            _smoothedEyeDirection = GazeReferenceFrame.ResolveForward(Context, transform);

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

            ResetEyesToRest();
            base.OnDisable();
        }

        /// <remarks>
        ///     Runs in <c>LateUpdate</c> at execution order <c>19500</c> so the humanoid
        ///     animator has already applied its head pose this frame. Bone writes issued here
        ///     survive into rendering; writing earlier would be overwritten by the animator.
        /// </remarks>
        private void LateUpdate()
        {
            // Actuator logic is runtime-only: bone writes have no meaning in EditMode and
            // EffectiveProfile may be null during a domain-reload race with [ExecuteAlways].
            if (!UnityEngine.Application.isPlaying) return;
            if (Context == null) return;

            float deltaTime = Time.deltaTime;
            ConvaiGazeEyeProfile profile = EffectiveProfile;
            GazeIntent intent = Context.GazeIntentProvider?.Current ?? GazeIntent.Relaxed;
            DialogueState dialogueState = Context.ConversationFlowSource?.Current.Primary ?? DialogueState.Idle;
            float now = Time.time;

            _saccade.Tick(profile, deltaTime, ref _random);
            _blink.Tick(profile, deltaTime, ref _random);

            FacialBlendshapeCompositorHost compositor = Context?.EnsureCompositor();
            _blendshapeWriter.SubmitBlink(compositor, this, _blink.EvaluateWeight(profile));

            if (_leftEye.Bone == null || _rightEye.Bone == null) return;

            Vector3 eyeCenter = (_leftEye.Bone.position + _rightEye.Bone.position) * 0.5f;
            Transform reference = GazeReferenceFrame.Resolve(Context, transform);
            Vector3 referenceForward = reference != null ? reference.forward : transform.forward;
            Vector3 restForward = ResolveCurrentEyeRestForwardWorld(referenceForward);

            bool useIdleExploration = ShouldUseIdleExploration(dialogueState, intent, profile);
            _idleExploration.Tick(profile, now, useIdleExploration, ref _random);

            Vector3 targetDirWorld = useIdleExploration
                ? _idleExploration.ResolveDirection(reference, restForward, profile)
                : ResolveTrackingDirection(intent, eyeCenter, restForward);

            float alpha = 1f - Mathf.Exp(-Mathf.Max(0.1f, profile.TrackingSharpness) * deltaTime);
            _smoothedEyeDirection = Vector3.Slerp(_smoothedEyeDirection, targetDirWorld, alpha).normalized;

            float tremorYaw = EyeTremorOscillator.Sample(profile, 0, now);
            float tremorPitch = EyeTremorOscillator.Sample(profile, 1, now);

            Vector2 leftAngles = EyeRotationSolver.Apply(
                ref _leftEye, reference, _smoothedEyeDirection, profile,
                _saccade.CurrentOffset, tremorYaw, tremorPitch);
            Vector2 rightAngles = EyeRotationSolver.Apply(
                ref _rightEye, reference, _smoothedEyeDirection, profile,
                _saccade.CurrentOffset, tremorYaw, tremorPitch);

            _blendshapeWriter.SubmitEyelidFollow(
                compositor, this, profile, leftAngles, rightAngles, deltaTime);
        }

        private void ResolveBones()
        {
            _leftEye.Clear();
            _rightEye.Clear();

            IStandardRigBinding rigBinding = Context?.EnsureRigBinding();
            if (rigBinding == null) return;

            Vector3 characterForward = GazeReferenceFrame.ResolveForward(Context, transform);

            if (rigBinding.TryGetBone(StandardBone.LeftEye, out Transform left))
                _leftEye.Bind(left, characterForward);
            if (rigBinding.TryGetBone(StandardBone.RightEye, out Transform right))
                _rightEye.Bind(right, characterForward);
        }

        private void ResetEyesToRest()
        {
            _leftEye.RestoreToRestIfOwning();
            _rightEye.RestoreToRestIfOwning();
            _smoothedEyeDirection = GazeReferenceFrame.ResolveForward(Context, transform);
            // EffectiveProfile is null-safe (returns null when _ownedProfile not yet initialized).
            ConvaiGazeEyeProfile p = EffectiveProfile;
            if (p != null) _saccade.Reset(p);
            _blendshapeWriter.ResetEyelidFollow();
        }

        private void HandleRigBindingChanged(IStandardRigBinding rigBinding)
        {
            ResetEyesToRest();
            ResolveBones();
            _blendshapeWriter.Bind(Context?.EnsureRigBinding());
            _smoothedEyeDirection = GazeReferenceFrame.ResolveForward(Context, transform);
        }

        private static Vector3 ResolveTrackingDirection(GazeIntent intent, Vector3 eyeCenter, Vector3 restForward)
        {
            float weight = Mathf.Clamp01(intent.OverallWeight);
            Vector3 desired = intent.WorldTargetPoint - eyeCenter;
            if (desired.sqrMagnitude < 1e-6f)
                desired = restForward;
            return Vector3.Slerp(restForward, desired.normalized, weight);
        }

        private static bool ShouldUseIdleExploration(
            DialogueState state,
            GazeIntent intent,
            ConvaiGazeEyeProfile profile) =>
            profile != null &&
            profile.EnableIdleExploration &&
            state == DialogueState.Idle &&
            intent.OverallWeight <= 0.05f;

        private Vector3 ResolveCurrentEyeRestForwardWorld(Vector3 fallbackForward)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            _leftEye.AccumulateRestForward(ref sum, ref count);
            _rightEye.AccumulateRestForward(ref sum, ref count);

            if (count > 0 && sum.sqrMagnitude > 1e-6f)
                return sum.normalized;

            return fallbackForward.sqrMagnitude > 1e-6f ? fallbackForward.normalized : Vector3.forward;
        }
    }
}
