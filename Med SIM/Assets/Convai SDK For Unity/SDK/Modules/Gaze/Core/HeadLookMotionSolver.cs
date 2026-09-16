using Convai.Modules.Gaze.Profiles;
using UnityEngine;

namespace Convai.Modules.Gaze.Core
{
    internal static class HeadLookMotionSolver
    {
        private const float UpperBodyBlendRangeDegrees = 35f;

        public static float SmoothAngle(
            float current,
            float target,
            float sharpness,
            float maxSpeedDegrees,
            float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            float alpha = 1f - Mathf.Exp(-Mathf.Max(0.1f, sharpness) * safeDeltaTime);
            float smoothed = current + (target - current) * alpha;
            if (maxSpeedDegrees <= 0f) return smoothed;
            return Mathf.MoveTowards(current, smoothed, maxSpeedDegrees * safeDeltaTime);
        }

        public static HeadLookDistribution SolveDistribution(
            ConvaiGazeHeadProfile profile,
            float yaw,
            float pitch,
            bool hasChest,
            bool hasUpperChest)
        {
            if (profile == null) return default;

            float chestYaw = 0f;
            float chestPitch = 0f;
            float upperChestYaw = 0f;
            float upperChestPitch = 0f;

            if (profile.EnableUpperBodyFollow && (hasChest || hasUpperChest))
            {
                float angle = Mathf.Max(Mathf.Abs(yaw), Mathf.Abs(pitch));
                float influence = EvaluateRange(
                    angle,
                    profile.UpperBodyActivationDegrees,
                    profile.UpperBodyActivationDegrees + UpperBodyBlendRangeDegrees) *
                    profile.UpperBodyFollowShare;

                SplitUpperBody(
                    yaw * influence,
                    profile.MaxChestYaw,
                    profile.MaxUpperChestYaw,
                    hasChest,
                    hasUpperChest,
                    out chestYaw,
                    out upperChestYaw);

                SplitUpperBody(
                    pitch * influence,
                    profile.MaxChestPitch,
                    profile.MaxUpperChestPitch,
                    hasChest,
                    hasUpperChest,
                    out chestPitch,
                    out upperChestPitch);
            }

            float remainingYaw = yaw - chestYaw - upperChestYaw;
            float remainingPitch = pitch - chestPitch - upperChestPitch;
            float neckShare = Mathf.Clamp01(profile.NeckShare);

            return new HeadLookDistribution(
                chestYaw,
                chestPitch,
                upperChestYaw,
                upperChestPitch,
                Mathf.Clamp(remainingYaw * neckShare, -profile.MaxNeckYaw, profile.MaxNeckYaw),
                Mathf.Clamp(remainingPitch * neckShare, -profile.MaxNeckPitch, profile.MaxNeckPitch),
                Mathf.Clamp(remainingYaw * (1f - neckShare), -profile.MaxHeadYaw, profile.MaxHeadYaw),
                Mathf.Clamp(remainingPitch * (1f - neckShare), -profile.MaxHeadPitch, profile.MaxHeadPitch));
        }

        private static void SplitUpperBody(
            float value,
            float chestLimit,
            float upperChestLimit,
            bool hasChest,
            bool hasUpperChest,
            out float chest,
            out float upperChest)
        {
            chest = 0f;
            upperChest = 0f;

            if (hasChest && hasUpperChest)
            {
                chest = Mathf.Clamp(value * 0.4f, -chestLimit, chestLimit);
                upperChest = Mathf.Clamp(value * 0.6f, -upperChestLimit, upperChestLimit);
                return;
            }

            if (hasUpperChest)
            {
                upperChest = Mathf.Clamp(value, -upperChestLimit, upperChestLimit);
                return;
            }

            if (hasChest)
                chest = Mathf.Clamp(value, -chestLimit, chestLimit);
        }

        private static float EvaluateRange(float value, float start, float full)
        {
            if (value <= start) return 0f;
            if (value >= full) return 1f;
            return Mathf.InverseLerp(start, full, value);
        }
    }

    internal readonly struct HeadLookDistribution
    {
        public readonly float ChestYaw;
        public readonly float ChestPitch;
        public readonly float UpperChestYaw;
        public readonly float UpperChestPitch;
        public readonly float NeckYaw;
        public readonly float NeckPitch;
        public readonly float HeadYaw;
        public readonly float HeadPitch;

        public HeadLookDistribution(
            float chestYaw,
            float chestPitch,
            float upperChestYaw,
            float upperChestPitch,
            float neckYaw,
            float neckPitch,
            float headYaw,
            float headPitch)
        {
            ChestYaw = chestYaw;
            ChestPitch = chestPitch;
            UpperChestYaw = upperChestYaw;
            UpperChestPitch = upperChestPitch;
            NeckYaw = neckYaw;
            NeckPitch = neckPitch;
            HeadYaw = headYaw;
            HeadPitch = headPitch;
        }
    }
}
