// Copyright (c) Convai. Licensed under the Convai SDK license. See LICENSE in the package root.

using System;
using Convai.Sample.Camera.Input;
using UnityEngine;

namespace Convai.Sample.Camera
{
    /// <summary>
    ///     Production-grade orbit camera used by the Convai SDK samples.
    ///     <para>
    ///         Orbits a world-space pivot (supplied as a <see cref="Transform" /> or a custom
    ///         <see cref="IOrbitTarget" />) and responds to mouse-style input:
    ///         <list type="bullet">
    ///             <item><description>Right-mouse drag orbits yaw and pitch.</description></item>
    ///             <item><description>Middle-mouse drag pans laterally along the camera's right/up axes.</description></item>
    ///             <item><description>Scroll wheel dollies, with a distance-aware step curve.</description></item>
    ///         </list>
    ///         The component moves the <see cref="GameObject" /> it lives on and is intended
    ///         to be attached directly to the <see cref="UnityEngine.Camera" />.
    ///     </para>
    ///     <para>
    ///         Motion is produced by a <em>state-based</em> damping model: raw input feeds
    ///         target values (yaw, pitch, distance, pan), and the current state is critically
    ///         damped toward them each frame using <see cref="Mathf.SmoothDampAngle" /> for
    ///         angles and <see cref="Mathf.SmoothDamp" /> for scalars / pan components. Pose
    ///         is then composed from the smoothed state, which avoids gimbal-sensitive
    ///         quaternion damping and keeps rotation, dolly, and pan feeling independent.
    ///     </para>
    ///     <para>
    ///         Design goals:
    ///         <list type="bullet">
    ///             <item><description>Zero render-pipeline dependency and no external camera package dependency.</description></item>
    ///             <item><description>Zero Convai runtime dependency (works with any <see cref="Transform" />).</description></item>
    ///             <item><description>Input backend pluggable via <see cref="ICameraInputReader" />; works with
    ///             the legacy Input Manager, the Input System package, or a user-supplied source.</description></item>
    ///             <item><description>Deterministic: all state advances inside <see cref="LateUpdate" />.</description></item>
    ///             <item><description>Allocation-free per frame.</description></item>
    ///         </list>
    ///     </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Convai/Samples/Camera/Convai Orbit Camera")]
    public sealed class ConvaiOrbitCamera : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Transform the camera orbits. If left empty, the camera freezes at its current pose " +
                 "until SetTarget(Transform) is called or OrbitTarget is assigned.")]
        private Transform _target;

        [SerializeField]
        [Tooltip("How the built-in Transform target resolves the pivot. Stable Pivot ignores animation-driven " +
                 "motion on child/bone targets while still following the character root or explicit anchor.")]
        private OrbitTargetTrackingMode _targetTrackingMode = OrbitTargetTrackingMode.StablePivot;

        [SerializeField]
        [Tooltip("Optional stable reference frame for Stable Pivot tracking. Leave empty to use the target's " +
                 "parent Animator transform, falling back to the target root.")]
        private Transform _targetAnchor;

        [SerializeField]
        [Tooltip("Camera tuning. Use OrbitCameraSettings.Default (available via right-click > Reset) " +
                 "for sensible production-safe defaults.")]
        private OrbitCameraSettings _settings = OrbitCameraSettings.Default;

        [SerializeField]
        [Tooltip("If true, SnapToTarget is invoked on Start so the first rendered frame is already in pose.")]
        private bool _snapOnStart = true;

        private ICameraInputReader _inputReader;
        private IOrbitTarget _orbitTarget;
        private TransformOrbitTarget _builtInTransformTarget;
        private UnityEngine.Camera _attachedCamera;

        // Extracted components (composition-based architecture)
        private OrbitInputAccumulator _inputAccumulator;
        private OrbitSmoother _smoother;
        private OrbitCollisionResolver _collisionResolver;
        private OrbitLensController _lensController;
        private OrbitCursorManager _cursorManager;
        private OrbitPivotTracker _pivotTracker;
        private OrbitComposer _composer;
        private OrbitIdleDrift _idleDrift;
        private OrbitDepthOfFieldController _dofController;
        private IOrbitVolumeAdapter _volumeAdapter;

        // State consolidated into value types
        private OrbitState _currentState;
        private OrbitState _targetState;
        private float _lastInputTime;

        /// <summary>Raised whenever the orbit target is replaced (including to <c>null</c>).</summary>
        public event Action<Transform> TargetChanged;

        /// <summary>
        ///     The <see cref="Transform" /> the camera orbits. Setting this replaces any custom
        ///     <see cref="OrbitTarget" /> with a built-in <see cref="TransformOrbitTarget" />
        ///     using <see cref="TargetTrackingMode" /> and <see cref="TargetAnchor" />.
        /// </summary>
        public Transform Target
        {
            get => _target;
            set => SetTarget(value);
        }

        /// <summary>
        ///     Tuning parameters. Setting a new value calls <see cref="OrbitCameraSettings.Validate" />
        ///     and re-clamps the live orbit state without snapping the pose.
        /// </summary>
        public OrbitCameraSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                _settings.Validate();
                if (_inputAccumulator != null)
                {
                    _currentState = _inputAccumulator.ClampOrbitState(_currentState, _settings);
                    _targetState = _inputAccumulator.ClampTargetState(_targetState, _settings);
                }
            }
        }

        /// <summary>
        ///     Tracking mode used by the built-in transform target. The default
        ///     <see cref="OrbitTargetTrackingMode.StablePivot" /> captures a pivot relative to
        ///     the target's Animator/root so idle animation on bones does not move the camera.
        /// </summary>
        public OrbitTargetTrackingMode TargetTrackingMode
        {
            get => _targetTrackingMode;
            set
            {
                if (_targetTrackingMode == value) return;

                _targetTrackingMode = value;
                ConfigureBuiltInTarget(true);
            }
        }

        /// <summary>
        ///     Optional stable reference frame for <see cref="OrbitTargetTrackingMode.StablePivot" />.
        ///     Leave <c>null</c> to auto-resolve from the target's parent Animator or root.
        /// </summary>
        public Transform TargetAnchor
        {
            get => _targetAnchor;
            set
            {
                if (_targetAnchor == value) return;

                _targetAnchor = value;
                ConfigureBuiltInTarget(true);
            }
        }

        /// <summary>
        ///     The input source. Defaults to <see cref="DefaultCameraInputReader" /> which
        ///     auto-selects the best backend available at runtime. Assign a custom
        ///     <see cref="ICameraInputReader" /> to integrate bespoke bindings (for example,
        ///     driven by an <c>InputActionAsset</c>). Assigning <c>null</c> installs a safe
        ///     no-op reader.
        /// </summary>
        public ICameraInputReader InputReader
        {
            get => _inputReader ??= new DefaultCameraInputReader();
            set => _inputReader = value ?? NullCameraInputReader.Instance;
        }

        /// <summary>
        ///     Active <see cref="IOrbitTarget" />. When assigned, takes precedence over
        ///     the serialized <see cref="Target" /> reference so callers can supply composite
        ///     or procedural pivots without losing the inspector fallback.
        /// </summary>
        public IOrbitTarget OrbitTarget
        {
            get
            {
                if (_orbitTarget != null)
                    return _orbitTarget;

                EnsureBuiltInTarget();
                return _builtInTransformTarget;
            }
            set => _orbitTarget = value;
        }

        /// <summary>The current (smoothed) yaw angle in degrees.</summary>
        public float Yaw => _currentState.Yaw;

        /// <summary>The current (smoothed) pitch angle in degrees.</summary>
        public float Pitch => _currentState.Pitch;

        /// <summary>The current (smoothed) orbit distance in meters.</summary>
        public float Distance => _currentState.Distance;

        /// <summary>The current (smoothed) camera-relative pan offset in meters.</summary>
        public Vector2 PanOffset => _currentState.Pan;

        /// <summary>
        ///     Replaces the orbit target <see cref="Transform" /> and raises
        ///     <see cref="TargetChanged" />. Any custom <see cref="IOrbitTarget" /> assigned
        ///     via <see cref="OrbitTarget" /> is cleared.
        /// </summary>
        /// <param name="target">The new target transform, or <c>null</c> to clear.</param>
        public void SetTarget(Transform target)
        {
            _target = target;
            EnsureBuiltInTarget();
            ConfigureBuiltInTarget(true);
            _orbitTarget = null;
            TargetChanged?.Invoke(target);
        }

        /// <summary>
        ///     Captures the current transform target point as the camera pivot for
        ///     <see cref="OrbitTargetTrackingMode.StablePivot" /> and
        ///     <see cref="OrbitTargetTrackingMode.FixedWorldPoint" />.
        /// </summary>
        public void CaptureTargetPivot()
        {
            EnsureComponentsInitialized();
            EnsureBuiltInTarget();
            _builtInTransformTarget.CapturePivot();
            _pivotTracker.Reset();
        }

        /// <summary>
        ///     Resets the targets for yaw, pitch, distance, and pan to the initial pose
        ///     declared in <see cref="Settings" />. The change is smoothed by the normal
        ///     update step; call <see cref="SnapToTarget" /> immediately afterwards to
        ///     teleport instead.
        /// </summary>
        public void ResetView()
        {
            EnsureComponentsInitialized();

            _targetState = new OrbitState(
                _settings.initialYaw,
                _settings.initialPitch,
                _settings.initialDistance,
                Vector2.zero);
            _targetState = _inputAccumulator.ClampTargetState(_targetState, _settings);
        }

        /// <summary>
        ///     Forces the camera to the desired pose for the current target immediately,
        ///     bypassing smoothing. Useful on scene load, teleport, or after re-parenting.
        /// </summary>
        public void SnapToTarget()
        {
            EnsureComponentsInitialized();

            _currentState = _targetState;
            _smoother.Reset();
            _collisionResolver.Reset(_targetState.Distance);
            _lensController.Reset(_settings.focalLength);
            _dofController?.Reset(_settings.dofSettings, _targetState.Distance);

            if (!_pivotTracker.TryResolvePivot(_target, _orbitTarget, _builtInTransformTarget, out Vector3 pivot))
                return;

            _pivotTracker.SnapTo(pivot);
            _lensController.Apply(ResolveAttachedCamera(), 0f, _settings);

            _composer.Compose(
                pivot,
                _currentState,
                _settings,
                Vector3.zero,
                _lensController,
                ResolveAttachedCamera(),
                out Vector3 framedPivot,
                out Quaternion rotation);

            float collisionDistance = _collisionResolver.Resolve(
                framedPivot,
                rotation,
                _currentState.Distance,
                0f,
                _settings);

            Vector3 position = OrbitMath.ComposePosition(framedPivot, rotation, collisionDistance);
            transform.SetPositionAndRotation(position, rotation);
        }

        private void Reset()
        {
            _targetTrackingMode = OrbitTargetTrackingMode.StablePivot;
            _targetAnchor = null;
            _settings = OrbitCameraSettings.Default;
            _snapOnStart = true;
        }

        private void OnValidate()
        {
            _settings.Validate();
            ConfigureBuiltInTarget(false);
        }

        private void Awake()
        {
            EnsureComponentsInitialized();

            // Ensure built-in target exists if serialized target is set
            if (_target != null)
                EnsureBuiltInTarget();
        }

        private void EnsureComponentsInitialized()
        {
            if (_inputAccumulator != null)
                return;

            _settings.Validate();

            // Initialize state
            _currentState = new OrbitState(
                _settings.initialYaw,
                _settings.initialPitch,
                _settings.initialDistance,
                Vector2.zero);
            _targetState = _currentState;
            _lastInputTime = GetClockTime();

            // Initialize components
            _inputAccumulator = new OrbitInputAccumulator();
            _smoother = new OrbitSmoother();
            _collisionResolver = new OrbitCollisionResolver();
            _lensController = new OrbitLensController();
            _cursorManager = new OrbitCursorManager();
            _pivotTracker = new OrbitPivotTracker();
            _composer = new OrbitComposer();
            _idleDrift = new OrbitIdleDrift();
            _dofController = new OrbitDepthOfFieldController();
            _volumeAdapter = OrbitVolumeAdapterFactory.Create();

            _collisionResolver.Reset(_currentState.Distance);
            _lensController.Reset(_settings.focalLength);
            _dofController.Reset(_settings.dofSettings, _currentState.Distance);
        }

        private void Start()
        {
            if (_snapOnStart) SnapToTarget();
        }

        private void OnDisable()
        {
            _cursorManager?.Restore();
        }

        private void LateUpdate()
        {
            EnsureComponentsInitialized();

            float deltaTime = _settings.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime < 1e-4f) deltaTime = 1e-4f;
            float now = GetClockTime();

            // Sample input
            ICameraInputReader reader = InputReader;
            reader.Sample();

            bool inputBlocked = reader.IsPointerOverUI() || reader.IsTextInputFocused();
            bool orbitHeld = !inputBlocked && reader.IsOrbitActive();
            bool panHeld = !inputBlocked && reader.IsPanActive();

            Vector2 mouseDelta = (orbitHeld || panHeld) ? reader.ReadOrbitDelta() : Vector2.zero;
            float zoomDelta = inputBlocked ? 0f : reader.ReadZoomDelta();
            bool hasMotionInput = (mouseDelta.sqrMagnitude > 0f) || !Mathf.Approximately(zoomDelta, 0f);

            if (hasMotionInput)
                _lastInputTime = now;

            // Accumulate input into target state
            if (panHeld && mouseDelta.sqrMagnitude > 0f)
                _targetState = _inputAccumulator.AccumulatePan(_targetState, mouseDelta, _currentState.Distance, _settings);
            else if (orbitHeld && mouseDelta.sqrMagnitude > 0f)
                _targetState = _inputAccumulator.AccumulateOrbit(_targetState, mouseDelta, _settings);

            if (!Mathf.Approximately(zoomDelta, 0f))
                _targetState = _inputAccumulator.AccumulateZoom(_targetState, zoomDelta, _settings);

            _targetState = _inputAccumulator.ClampTargetState(_targetState, _settings);

            // Smooth current state towards target
            _currentState = _smoother.Advance(_currentState, _targetState, deltaTime, _settings);
            _currentState = _inputAccumulator.ClampOrbitState(_currentState, _settings);

            // Lens control
            _lensController.Apply(ResolveAttachedCamera(), deltaTime, _settings);

            // Depth of field control
            if (_settings.dofEnabled && _dofController != null && _volumeAdapter != null)
            {
                _dofController.Calculate(_currentState.Distance, _settings.dofSettings);
                OrbitDofState dofState = _dofController.Advance(deltaTime, _settings.dofSettings);
                _volumeAdapter.TryApplyDepthOfField(
                    ResolveAttachedCamera(),
                    dofState.Aperture,
                    dofState.FocusDistance);
            }

            // Resolve and smooth pivot
            if (!_pivotTracker.TryResolvePivot(_target, _orbitTarget, _builtInTransformTarget, out Vector3 pivot))
                return;

            Vector3 smoothedPivot = _pivotTracker.AdvancePivot(pivot, deltaTime, _settings);
            Vector3 driftOffsets = _idleDrift.Evaluate(_lastInputTime, now, _settings);

            // Compose final pose
            _composer.Compose(
                smoothedPivot,
                _currentState,
                _settings,
                driftOffsets,
                _lensController,
                ResolveAttachedCamera(),
                out Vector3 framedPivot,
                out Quaternion rotation);

            float desiredDistance = Mathf.Clamp(
                _currentState.Distance + driftOffsets.z,
                _settings.distanceMin,
                _settings.distanceMax);

            float collisionDistance = _collisionResolver.Resolve(framedPivot, rotation, desiredDistance, deltaTime, _settings);
            Vector3 position = OrbitMath.ComposePosition(framedPivot, rotation, collisionDistance);

            transform.SetPositionAndRotation(position, rotation);

            // Cursor management
            _cursorManager.Update(orbitHeld || panHeld, _settings);
        }









        private UnityEngine.Camera ResolveAttachedCamera()
        {
            if (_attachedCamera != null)
                return _attachedCamera;

            _attachedCamera = GetComponent<UnityEngine.Camera>();
            return _attachedCamera;
        }



        private float GetClockTime()
        {
            return _settings.useUnscaledTime ? Time.unscaledTime : Time.time;
        }



        private void EnsureBuiltInTarget()
        {
            if (_builtInTransformTarget == null)
                _builtInTransformTarget = new TransformOrbitTarget(_target, _targetTrackingMode, _targetAnchor);
            else
                ConfigureBuiltInTarget(false);
        }

        private void ConfigureBuiltInTarget(bool recapturePivot)
        {
            if (_builtInTransformTarget == null)
                return;

            _builtInTransformTarget.Anchor = _targetAnchor;
            _builtInTransformTarget.TrackingMode = _targetTrackingMode;
            _builtInTransformTarget.Transform = _target;

            if (recapturePivot)
                _builtInTransformTarget.CapturePivot();
        }





#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_settings.dofEnabled || _dofController == null || _pivotTracker == null)
                return;

            if (!_pivotTracker.TryResolvePivot(_target, _orbitTarget, _builtInTransformTarget, out Vector3 pivot))
                return;

            OrbitDofState dofState = _dofController.CurrentState;
            OrbitDofSettings dofSettings = _settings.dofSettings;

            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(pivot, dofState.FocusDistance);

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pivot, dofSettings.closeDistance);

            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(pivot, dofSettings.farDistance);

            UnityEditor.Handles.Label(
                pivot + Vector3.up * 0.5f,
                $"DOF: f/{dofState.Aperture:F1} @ {dofState.FocusDistance:F2}m",
                new UnityEngine.GUIStyle { normal = { textColor = Color.green } });
        }
#endif

    }
}
