using UnityEngine;

namespace Convai.Modules.Gaze.Profiles
{
    /// <summary>
    ///     Authoring asset for <c>ConvaiHeadLookActuator</c>. Controls how aggressively the head
    ///     and neck rotate toward the gaze target.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Convai/Embodiment/Gaze Head Profile",
        fileName = "ConvaiGazeHeadProfile")]
    public sealed class ConvaiGazeHeadProfile : ScriptableObject
    {
        [Header("Range")]
        [SerializeField, Range(0f, 90f)]
        [Tooltip("Maximum yaw rotation applied to the neck (degrees).")]
        private float maxNeckYaw = 40f;

        [SerializeField, Range(0f, 90f)]
        [Tooltip("Maximum pitch rotation applied to the neck (degrees).")]
        private float maxNeckPitch = 30f;

        [SerializeField, Range(0f, 60f)]
        [Tooltip("Additional yaw rotation applied to the head on top of the neck.")]
        private float maxHeadYaw = 20f;

        [SerializeField, Range(0f, 60f)]
        [Tooltip("Additional pitch rotation applied to the head on top of the neck.")]
        private float maxHeadPitch = 15f;

        [Header("Smoothing")]
        [SerializeField, Range(0.5f, 30f)]
        [Tooltip("Exponential approach rate while tracking an active attention target (higher = snappier head turns).")]
        private float smoothingSharpness = 6f;

        [SerializeField, Range(0.5f, 30f)]
        [Tooltip("Exponential return rate when attention authority fades out.")]
        private float returnSharpness = 5f;

        [SerializeField, Range(0.5f, 30f)]
        [Tooltip("Exponential approach rate for subtle idle head exploration.")]
        private float idleSharpness = 2.2f;

        [SerializeField, Range(0f, 720f)]
        [Tooltip("Maximum yaw speed in degrees per second. 0 disables speed limiting.")]
        private float maxYawSpeedDegrees = 160f;

        [SerializeField, Range(0f, 720f)]
        [Tooltip("Maximum pitch speed in degrees per second. 0 disables speed limiting.")]
        private float maxPitchSpeedDegrees = 120f;

        [SerializeField, Range(0f, 10f)]
        [Tooltip("Minimum angular deviation before the head begins to turn (degrees).")]
        private float deadzoneDegrees = 1.5f;

        [Header("Idle Exploration")]
        [SerializeField]
        [Tooltip("Lets the head make subtle slow exploration in idle instead of freezing at rest.")]
        private bool enableIdleExploration = true;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Strength of idle head exploration after attention authority fades out.")]
        private float idleExplorationWeight = 0.7f;

        [SerializeField, Range(0f, 30f)]
        [Tooltip("Maximum left/right idle head exploration amplitude in degrees.")]
        private float idleExplorationYawDegrees = 6f;

        [SerializeField, Range(0f, 15f)]
        [Tooltip("Maximum upward idle head exploration amplitude in degrees.")]
        private float idleExplorationUpDegrees = 1.5f;

        [SerializeField, Range(0f, 15f)]
        [Tooltip("Maximum downward idle head exploration amplitude in degrees.")]
        private float idleExplorationDownDegrees = 2.5f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Minimum dwell time before sampling a new idle head target.")]
        private float idleExplorationIntervalMin = 2.2f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Maximum dwell time before sampling a new idle head target.")]
        private float idleExplorationIntervalMax = 5.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Bias toward the center when sampling idle head targets.")]
        private float idleExplorationCenterBias = 0.55f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance that the next idle head target recenters instead of sampling a new offset.")]
        private float idleRecenteringChance = 0.32f;

        [Header("Authority")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum share of overall gaze authority that still reaches the head when eyes lead the look.")]
        private float minimumHeadContribution = 0.45f;

        [SerializeField]
        [Tooltip("Optional curve mapping head share (1 - EyeShare, x in [0,1]) to head contribution. " +
                 "Leave empty for the default linear blend between MinimumHeadContribution and 1.")]
        private AnimationCurve headBlendCurve;

        [Header("Distribution")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Share of the rotation carried by the neck. 1 = neck-only, 0 = head-only.")]
        private float neckShare = 0.6f;

        [Header("Upper Body Follow")]
        [SerializeField]
        [Tooltip("Lets chest / upper chest subtly assist large head turns when those bones exist.")]
        private bool enableUpperBodyFollow = true;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Share of large gaze rotation allowed to move into chest / upper chest.")]
        private float upperBodyFollowShare = 0.22f;

        [SerializeField, Range(0f, 90f)]
        [Tooltip("Angle where upper-body follow starts helping the head turn.")]
        private float upperBodyActivationDegrees = 28f;

        [SerializeField, Range(0f, 45f)]
        [Tooltip("Maximum yaw rotation applied to the chest during large head turns.")]
        private float maxChestYaw = 5f;

        [SerializeField, Range(0f, 30f)]
        [Tooltip("Maximum pitch rotation applied to the chest during large head turns.")]
        private float maxChestPitch = 2.5f;

        [SerializeField, Range(0f, 45f)]
        [Tooltip("Maximum yaw rotation applied to the upper chest during large head turns.")]
        private float maxUpperChestYaw = 7f;

        [SerializeField, Range(0f, 30f)]
        [Tooltip("Maximum pitch rotation applied to the upper chest during large head turns.")]
        private float maxUpperChestPitch = 3.5f;

        [Header("Ownership")]
        [SerializeField, Range(0.001f, 0.5f)]
        [Tooltip("Yaw/pitch threshold at which the actuator releases head/neck bones back to the base pose. " +
                 "Lower values feel locked-in; raise on scaled rigs to avoid micro-jitter.")]
        private float headReturnThresholdDegrees = 0.02f;

        public float MaxNeckYaw => maxNeckYaw;
        public float MaxNeckPitch => maxNeckPitch;
        public float MaxHeadYaw => maxHeadYaw;
        public float MaxHeadPitch => maxHeadPitch;
        public float SmoothingSharpness => smoothingSharpness;
        public float ReturnSharpness => returnSharpness;
        public float IdleSharpness => idleSharpness;
        public float MaxYawSpeedDegrees => maxYawSpeedDegrees;
        public float MaxPitchSpeedDegrees => maxPitchSpeedDegrees;
        public float DeadzoneDegrees => deadzoneDegrees;
        public bool EnableIdleExploration => enableIdleExploration;
        public float IdleExplorationWeight => idleExplorationWeight;
        public float IdleExplorationYawDegrees => idleExplorationYawDegrees;
        public float IdleExplorationUpDegrees => idleExplorationUpDegrees;
        public float IdleExplorationDownDegrees => idleExplorationDownDegrees;
        public float IdleExplorationIntervalMin => idleExplorationIntervalMin;
        public float IdleExplorationIntervalMax => idleExplorationIntervalMax;
        public float IdleExplorationCenterBias => idleExplorationCenterBias;
        public float IdleRecenteringChance => idleRecenteringChance;
        public float MinimumHeadContribution => minimumHeadContribution;
        public AnimationCurve HeadBlendCurve => headBlendCurve;
        public float NeckShare => neckShare;
        public bool EnableUpperBodyFollow => enableUpperBodyFollow;
        public float UpperBodyFollowShare => upperBodyFollowShare;
        public float UpperBodyActivationDegrees => upperBodyActivationDegrees;
        public float MaxChestYaw => maxChestYaw;
        public float MaxChestPitch => maxChestPitch;
        public float MaxUpperChestYaw => maxUpperChestYaw;
        public float MaxUpperChestPitch => maxUpperChestPitch;
        public float HeadReturnThresholdDegrees => headReturnThresholdDegrees;

        private void OnValidate()
        {
            maxNeckYaw = Mathf.Max(0f, maxNeckYaw);
            maxNeckPitch = Mathf.Max(0f, maxNeckPitch);
            maxHeadYaw = Mathf.Max(0f, maxHeadYaw);
            maxHeadPitch = Mathf.Max(0f, maxHeadPitch);
            smoothingSharpness = Mathf.Max(0.5f, smoothingSharpness);
            returnSharpness = Mathf.Max(0.5f, returnSharpness);
            idleSharpness = Mathf.Max(0.5f, idleSharpness);
            maxYawSpeedDegrees = Mathf.Max(0f, maxYawSpeedDegrees);
            maxPitchSpeedDegrees = Mathf.Max(0f, maxPitchSpeedDegrees);
            deadzoneDegrees = Mathf.Max(0f, deadzoneDegrees);
            idleExplorationWeight = Mathf.Clamp01(idleExplorationWeight);
            idleExplorationYawDegrees = Mathf.Max(0f, idleExplorationYawDegrees);
            idleExplorationUpDegrees = Mathf.Max(0f, idleExplorationUpDegrees);
            idleExplorationDownDegrees = Mathf.Max(0f, idleExplorationDownDegrees);
            idleExplorationIntervalMin = Mathf.Max(0.05f, idleExplorationIntervalMin);
            idleExplorationIntervalMax = Mathf.Max(idleExplorationIntervalMin, idleExplorationIntervalMax);
            idleExplorationCenterBias = Mathf.Clamp01(idleExplorationCenterBias);
            idleRecenteringChance = Mathf.Clamp01(idleRecenteringChance);
            minimumHeadContribution = Mathf.Clamp01(minimumHeadContribution);
            neckShare = Mathf.Clamp01(neckShare);
            upperBodyFollowShare = Mathf.Clamp01(upperBodyFollowShare);
            upperBodyActivationDegrees = Mathf.Max(0f, upperBodyActivationDegrees);
            maxChestYaw = Mathf.Max(0f, maxChestYaw);
            maxChestPitch = Mathf.Max(0f, maxChestPitch);
            maxUpperChestYaw = Mathf.Max(0f, maxUpperChestYaw);
            maxUpperChestPitch = Mathf.Max(0f, maxUpperChestPitch);
            headReturnThresholdDegrees = Mathf.Clamp(headReturnThresholdDegrees, 0.001f, 0.5f);
        }

        /// <summary>
        ///     Resolves the head-blend value for the given head share (<c>1 - EyeShare</c>).
        ///     When <see cref="HeadBlendCurve"/> is unauthored or empty, falls back to a linear
        ///     interpolation between <see cref="MinimumHeadContribution"/> and 1.
        /// </summary>
        public float EvaluateHeadBlend(float headShare)
        {
            float clamped = Mathf.Clamp01(headShare);
            if (headBlendCurve != null && headBlendCurve.length > 0)
                return Mathf.Clamp01(headBlendCurve.Evaluate(clamped));
            return Mathf.Lerp(minimumHeadContribution, 1f, clamped);
        }

        public static ConvaiGazeHeadProfile CreateDefault()
        {
            ConvaiGazeHeadProfile instance = CreateInstance<ConvaiGazeHeadProfile>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }

        /// <summary>
        ///     Conversational tuning: the neck carries ~60&#37; of the rotation, head finishes the
        ///     turn, and a small dead-zone prevents jitter when the target is near center.
        /// </summary>
        public static ConvaiGazeHeadProfile CreateConversationalPreset()
        {
            ConvaiGazeHeadProfile instance = CreateInstance<ConvaiGazeHeadProfile>();
            instance.maxNeckYaw = 38f;
            instance.maxNeckPitch = 26f;
            instance.maxHeadYaw = 18f;
            instance.maxHeadPitch = 14f;
            instance.smoothingSharpness = 5.5f;
            instance.returnSharpness = 4.2f;
            instance.idleSharpness = 2.2f;
            instance.maxYawSpeedDegrees = 150f;
            instance.maxPitchSpeedDegrees = 110f;
            instance.deadzoneDegrees = 1.2f;
            instance.enableIdleExploration = true;
            instance.idleExplorationWeight = 0.7f;
            instance.idleExplorationYawDegrees = 6f;
            instance.idleExplorationUpDegrees = 1.5f;
            instance.idleExplorationDownDegrees = 2.5f;
            instance.idleExplorationIntervalMin = 2.2f;
            instance.idleExplorationIntervalMax = 5.5f;
            instance.idleExplorationCenterBias = 0.55f;
            instance.idleRecenteringChance = 0.32f;
            instance.minimumHeadContribution = 0.5f;
            instance.neckShare = 0.62f;
            instance.enableUpperBodyFollow = true;
            instance.upperBodyFollowShare = 0.22f;
            instance.upperBodyActivationDegrees = 28f;
            instance.maxChestYaw = 5f;
            instance.maxChestPitch = 2.5f;
            instance.maxUpperChestYaw = 7f;
            instance.maxUpperChestPitch = 3.5f;
            return instance;
        }
    }
}
