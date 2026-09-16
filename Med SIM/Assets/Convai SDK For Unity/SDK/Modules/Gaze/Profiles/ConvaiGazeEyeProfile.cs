using UnityEngine;

namespace Convai.Modules.Gaze.Profiles
{
    /// <summary>
    ///     Authoring asset for <c>ConvaiEyeGazeActuator</c>: per-eye tracking speeds, micro-saccade
    ///     cadence, blink cadence, and vergence handling.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Convai/Embodiment/Gaze Eye Profile",
        fileName = "ConvaiGazeEyeProfile")]
    public sealed class ConvaiGazeEyeProfile : ScriptableObject
    {
        [Header("Tracking")]
        [SerializeField, Range(1f, 40f)]
        [Tooltip("Exponential approach rate toward the target gaze direction.")]
        private float trackingSharpness = 18f;

        [SerializeField, Range(0f, 90f)]
        [Tooltip("Maximum allowed yaw deviation from the head-forward axis (degrees).")]
        private float maxYawDegrees = 45f;

        [SerializeField, Range(0f, 90f)]
        [Tooltip("Maximum allowed pitch deviation from the head-forward axis (degrees).")]
        private float maxPitchDegrees = 30f;

        [Header("Saccades")]
        [SerializeField]
        [Tooltip("Periodic micro-movements that keep the eyes from looking glassy.")]
        private bool enableSaccades = true;

        [SerializeField, Min(0.05f)]
        [Tooltip("Average interval between saccades (seconds).")]
        private float saccadeIntervalMean = 1.8f;

        [SerializeField, Min(0f)]
        [Tooltip("Random jitter added to saccade interval (seconds).")]
        private float saccadeIntervalJitter = 0.65f;

        [SerializeField, Range(0f, 5f)]
        [Tooltip("Maximum saccade deviation in degrees.")]
        private float saccadeMaxDegrees = 2.5f;

        [SerializeField, Range(0.01f, 0.25f)]
        [Tooltip("Duration of the saccade envelope (seconds).")]
        private float saccadeDuration = 0.07f;

        [Header("Micro Tremor")]
        [SerializeField]
        private bool enableMicroTremor = true;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Amplitude (degrees) of the always-on tremor.")]
        private float microTremorAmplitude = 0.18f;

        [SerializeField, Range(1f, 30f)]
        [Tooltip("Tremor frequency (Hz).")]
        private float microTremorFrequency = 11f;

        [Header("Idle Exploration")]
        [SerializeField]
        [Tooltip("Lets idle eyes drift between nearby points instead of locking to the player camera.")]
        private bool enableIdleExploration = true;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How strongly idle exploration pulls away from straight-ahead rest gaze.")]
        private float idleExplorationWeight = 0.65f;

        [SerializeField, Range(0f, 45f)]
        [Tooltip("Maximum left/right idle glance amplitude in degrees.")]
        private float idleExplorationHorizontalDegrees = 14f;

        [SerializeField, Range(0f, 25f)]
        [Tooltip("Maximum upward idle glance amplitude in degrees.")]
        private float idleExplorationUpDegrees = 5f;

        [SerializeField, Range(0f, 25f)]
        [Tooltip("Maximum downward idle glance amplitude in degrees.")]
        private float idleExplorationDownDegrees = 8f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Minimum dwell time before sampling a new idle glance target.")]
        private float idleExplorationIntervalMin = 1.1f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Maximum dwell time before sampling a new idle glance target.")]
        private float idleExplorationIntervalMax = 3.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Bias toward the center when sampling idle glance targets.")]
        private float idleExplorationCenterBias = 0.38f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance that the next idle glance recenters instead of sampling a new offset.")]
        private float idleRecenteringChance = 0.22f;

        [Header("Blink")]
        [SerializeField]
        private bool enableBlink = true;

        [SerializeField, Min(0.5f)]
        [Tooltip("Average interval between blinks (seconds).")]
        private float blinkIntervalMean = 3.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Random jitter added to blink interval (seconds).")]
        private float blinkIntervalJitter = 1.25f;

        [SerializeField, Range(0.02f, 0.3f)]
        [Tooltip("Full close -> open cycle duration (seconds).")]
        private float blinkCycleDuration = 0.15f;

        [Header("Eyelid Follow")]
        [SerializeField]
        [Tooltip("Procedurally couples eyelids to eye pitch/yaw so downward and extreme glances do not expose unrealistic sclera.")]
        private bool enableEyelidFollow = true;

        [SerializeField, Range(1f, 40f)]
        [Tooltip("Exponential smoothing speed for gaze-driven eyelid blendshapes.")]
        private float eyelidFollowSharpness = 18f;

        [SerializeField, Range(0f, 25f)]
        [Tooltip("Downward eye pitch where upper lid follow starts.")]
        private float downwardLidStartDegrees = 2f;

        [SerializeField, Range(1f, 45f)]
        [Tooltip("Downward eye pitch where upper lid follow reaches full authored weight.")]
        private float downwardLidFullDegrees = 18f;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Maximum weight for dedicated upper-lid-down targets when the eyes look downward.")]
        private float downwardUpperLidMaxWeight = 42f;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Maximum blink support weight used with downward gaze. This remains active even when dedicated lid targets exist, because some rigs expose lash-only lid helpers.")]
        private float downwardBlinkFallbackMaxWeight = 26f;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Optional EyeLookDown blendshape support. Keep at 0 for generic rigs unless these shapes help eyelid occlusion instead of double-driving eyeball direction.")]
        private float downwardLookShapeMaxWeight = 0f;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Maximum lower-lid/squint support weight for downward gaze.")]
        private float downwardLowerLidMaxWeight = 12f;

        [SerializeField, Range(0f, 25f)]
        [Tooltip("Upward eye pitch where upper lid lift / eye wide response starts.")]
        private float upwardLidStartDegrees = 3f;

        [SerializeField, Range(1f, 45f)]
        [Tooltip("Upward eye pitch where eye wide response reaches full authored weight.")]
        private float upwardLidFullDegrees = 18f;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Maximum eye-wide or upper-lid-up weight when the eyes look upward.")]
        private float upwardEyeWideMaxWeight = 18f;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Optional EyeLookUp blendshape support. Keep at 0 for generic rigs unless these shapes help eyelid occlusion instead of double-driving eyeball direction.")]
        private float upwardLookShapeMaxWeight = 0f;

        [SerializeField, Range(0f, 60f)]
        [Tooltip("Combined eye yaw/pitch strain where subtle squint starts.")]
        private float extremeGazeSquintStartDegrees = 16f;

        [SerializeField, Range(1f, 90f)]
        [Tooltip("Combined eye yaw/pitch strain where subtle squint reaches full authored weight.")]
        private float extremeGazeSquintFullDegrees = 42f;

        [SerializeField, Range(0f, 100f)]
        [Tooltip("Maximum subtle squint applied at extreme gaze angles.")]
        private float extremeGazeSquintMaxWeight = 10f;

        public float TrackingSharpness => trackingSharpness;
        public float MaxYawDegrees => maxYawDegrees;
        public float MaxPitchDegrees => maxPitchDegrees;
        public bool EnableSaccades => enableSaccades;
        public float SaccadeIntervalMean => saccadeIntervalMean;
        public float SaccadeIntervalJitter => saccadeIntervalJitter;
        public float SaccadeMaxDegrees => saccadeMaxDegrees;
        public float SaccadeDuration => saccadeDuration;
        public bool EnableMicroTremor => enableMicroTremor;
        public float MicroTremorAmplitude => microTremorAmplitude;
        public float MicroTremorFrequency => microTremorFrequency;
        public bool EnableIdleExploration => enableIdleExploration;
        public float IdleExplorationWeight => idleExplorationWeight;
        public float IdleExplorationHorizontalDegrees => idleExplorationHorizontalDegrees;
        public float IdleExplorationUpDegrees => idleExplorationUpDegrees;
        public float IdleExplorationDownDegrees => idleExplorationDownDegrees;
        public float IdleExplorationIntervalMin => idleExplorationIntervalMin;
        public float IdleExplorationIntervalMax => Mathf.Max(idleExplorationIntervalMin, idleExplorationIntervalMax);
        public float IdleExplorationCenterBias => idleExplorationCenterBias;
        public float IdleRecenteringChance => idleRecenteringChance;
        public bool EnableBlink => enableBlink;
        public float BlinkIntervalMean => blinkIntervalMean;
        public float BlinkIntervalJitter => blinkIntervalJitter;
        public float BlinkCycleDuration => blinkCycleDuration;
        public bool EnableEyelidFollow => enableEyelidFollow;
        public float EyelidFollowSharpness => eyelidFollowSharpness;
        public float DownwardLidStartDegrees => downwardLidStartDegrees;
        public float DownwardLidFullDegrees => downwardLidFullDegrees;
        public float DownwardUpperLidMaxWeight => downwardUpperLidMaxWeight;
        public float DownwardBlinkFallbackMaxWeight => downwardBlinkFallbackMaxWeight;
        public float DownwardLookShapeMaxWeight => downwardLookShapeMaxWeight;
        public float DownwardLowerLidMaxWeight => downwardLowerLidMaxWeight;
        public float UpwardLidStartDegrees => upwardLidStartDegrees;
        public float UpwardLidFullDegrees => upwardLidFullDegrees;
        public float UpwardEyeWideMaxWeight => upwardEyeWideMaxWeight;
        public float UpwardLookShapeMaxWeight => upwardLookShapeMaxWeight;
        public float ExtremeGazeSquintStartDegrees => extremeGazeSquintStartDegrees;
        public float ExtremeGazeSquintFullDegrees => extremeGazeSquintFullDegrees;
        public float ExtremeGazeSquintMaxWeight => extremeGazeSquintMaxWeight;

        private void OnValidate()
        {
            saccadeIntervalMean = Mathf.Max(0.05f, saccadeIntervalMean);
            saccadeIntervalJitter = Mathf.Clamp(saccadeIntervalJitter, 0f, saccadeIntervalMean);
            blinkIntervalMean = Mathf.Max(0.5f, blinkIntervalMean);
            blinkIntervalJitter = Mathf.Clamp(blinkIntervalJitter, 0f, blinkIntervalMean);
            eyelidFollowSharpness = Mathf.Max(1f, eyelidFollowSharpness);
            downwardLidFullDegrees = Mathf.Max(downwardLidStartDegrees + 0.01f, downwardLidFullDegrees);
            upwardLidFullDegrees = Mathf.Max(upwardLidStartDegrees + 0.01f, upwardLidFullDegrees);
            extremeGazeSquintFullDegrees = Mathf.Max(extremeGazeSquintStartDegrees + 0.01f, extremeGazeSquintFullDegrees);
            idleExplorationIntervalMin = Mathf.Max(0.05f, idleExplorationIntervalMin);
            idleExplorationIntervalMax = Mathf.Max(idleExplorationIntervalMin, idleExplorationIntervalMax);
        }

        public static ConvaiGazeEyeProfile CreateDefault()
        {
            ConvaiGazeEyeProfile instance = CreateInstance<ConvaiGazeEyeProfile>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }

        /// <summary>
        ///     Conversational tuning informed by published ocular studies: ~1.8 s mean saccade
        ///     interval, ~11 Hz micro-tremor, ~3.2 s mean blink interval, conservative yaw
        ///     and pitch limits that keep the eyes inside plausible oculomotor range.
        /// </summary>
        public static ConvaiGazeEyeProfile CreateConversationalPreset()
        {
            ConvaiGazeEyeProfile instance = CreateInstance<ConvaiGazeEyeProfile>();
            instance.trackingSharpness = 16f;
            instance.maxYawDegrees = 35f;
            instance.maxPitchDegrees = 22f;
            instance.enableSaccades = true;
            instance.saccadeIntervalMean = 1.9f;
            instance.saccadeIntervalJitter = 0.7f;
            instance.saccadeMaxDegrees = 2.2f;
            instance.saccadeDuration = 0.06f;
            instance.enableMicroTremor = true;
            instance.microTremorAmplitude = 0.15f;
            instance.microTremorFrequency = 11f;
            instance.enableIdleExploration = true;
            instance.idleExplorationWeight = 0.7f;
            instance.idleExplorationHorizontalDegrees = 14f;
            instance.idleExplorationUpDegrees = 5f;
            instance.idleExplorationDownDegrees = 8f;
            instance.idleExplorationIntervalMin = 1.1f;
            instance.idleExplorationIntervalMax = 3.2f;
            instance.idleExplorationCenterBias = 0.38f;
            instance.idleRecenteringChance = 0.22f;
            instance.enableBlink = true;
            instance.blinkIntervalMean = 3.4f;
            instance.blinkIntervalJitter = 1.3f;
            instance.blinkCycleDuration = 0.13f;
            instance.enableEyelidFollow = true;
            instance.eyelidFollowSharpness = 18f;
            instance.downwardLidStartDegrees = 2f;
            instance.downwardLidFullDegrees = 18f;
            instance.downwardUpperLidMaxWeight = 42f;
            instance.downwardBlinkFallbackMaxWeight = 26f;
            instance.downwardLookShapeMaxWeight = 0f;
            instance.downwardLowerLidMaxWeight = 12f;
            instance.upwardLidStartDegrees = 3f;
            instance.upwardLidFullDegrees = 18f;
            instance.upwardEyeWideMaxWeight = 18f;
            instance.upwardLookShapeMaxWeight = 0f;
            instance.extremeGazeSquintStartDegrees = 16f;
            instance.extremeGazeSquintFullDegrees = 42f;
            instance.extremeGazeSquintMaxWeight = 10f;
            return instance;
        }
    }
}
