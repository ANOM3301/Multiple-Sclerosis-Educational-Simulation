using System.Reflection;
using Convai.Modules.Gaze.Components;
using Convai.Modules.Gaze.Core;
using Convai.Modules.Gaze.Profiles;
using NUnit.Framework;
using UnityEngine;

namespace Convai.Tests.EditMode.Gaze
{
    public sealed class HeadLookMotionSolverTests
    {
        [Test]
        public void ConversationalPreset_EnablesConservativeIdleExplorationAndUpperBodyFollow()
        {
            ConvaiGazeHeadProfile profile = ConvaiGazeHeadProfile.CreateConversationalPreset();
            try
            {
                Assert.IsTrue(profile.EnableIdleExploration);
                Assert.AreEqual(6f, profile.IdleExplorationYawDegrees, 0.001f);
                Assert.AreEqual(1.5f, profile.IdleExplorationUpDegrees, 0.001f);
                Assert.AreEqual(2.5f, profile.IdleExplorationDownDegrees, 0.001f);
                Assert.AreEqual(2.2f, profile.IdleExplorationIntervalMin, 0.001f);
                Assert.AreEqual(5.5f, profile.IdleExplorationIntervalMax, 0.001f);
                Assert.IsTrue(profile.EnableUpperBodyFollow);
                Assert.AreEqual(28f, profile.UpperBodyActivationDegrees, 0.001f);
                Assert.LessOrEqual(profile.UpperBodyFollowShare, 0.25f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void OnValidate_ClampsMotionIdleAndUpperBodyValues()
        {
            ConvaiGazeHeadProfile profile = ScriptableObject.CreateInstance<ConvaiGazeHeadProfile>();
            try
            {
                Set(profile, "returnSharpness", -10f);
                Set(profile, "idleSharpness", -1f);
                Set(profile, "maxYawSpeedDegrees", -20f);
                Set(profile, "idleExplorationIntervalMin", 4f);
                Set(profile, "idleExplorationIntervalMax", 1f);
                Set(profile, "upperBodyFollowShare", 5f);
                Set(profile, "upperBodyActivationDegrees", -5f);
                InvokeOnValidate(profile);

                Assert.GreaterOrEqual(profile.ReturnSharpness, 0.5f);
                Assert.GreaterOrEqual(profile.IdleSharpness, 0.5f);
                Assert.GreaterOrEqual(profile.MaxYawSpeedDegrees, 0f);
                Assert.AreEqual(profile.IdleExplorationIntervalMin, profile.IdleExplorationIntervalMax);
                Assert.AreEqual(1f, profile.UpperBodyFollowShare, 0.001f);
                Assert.GreaterOrEqual(profile.UpperBodyActivationDegrees, 0f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SolveDistribution_UsesUpperBodyOnlyForLargeAngles()
        {
            ConvaiGazeHeadProfile profile = ConvaiGazeHeadProfile.CreateConversationalPreset();
            try
            {
                HeadLookDistribution small = HeadLookMotionSolver.SolveDistribution(profile, 10f, 0f, true, true);
                Assert.AreEqual(0f, small.ChestYaw, 0.001f);
                Assert.AreEqual(0f, small.UpperChestYaw, 0.001f);

                HeadLookDistribution large = HeadLookMotionSolver.SolveDistribution(profile, 50f, 0f, true, true);
                Assert.Greater(Mathf.Abs(large.ChestYaw), 0f);
                Assert.Greater(Mathf.Abs(large.UpperChestYaw), 0f);
                Assert.Less(Mathf.Abs(large.NeckYaw), 50f * profile.NeckShare);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SolveDistribution_MissingUpperBodyBonesKeepsRotationOnNeckAndHead()
        {
            ConvaiGazeHeadProfile profile = ConvaiGazeHeadProfile.CreateConversationalPreset();
            try
            {
                HeadLookDistribution distribution = HeadLookMotionSolver.SolveDistribution(profile, 40f, 8f, false, false);

                Assert.AreEqual(0f, distribution.ChestYaw, 0.001f);
                Assert.AreEqual(0f, distribution.UpperChestYaw, 0.001f);
                Assert.AreEqual(Mathf.Clamp(40f * profile.NeckShare, -profile.MaxNeckYaw, profile.MaxNeckYaw),
                    distribution.NeckYaw, 0.001f);
                Assert.AreEqual(Mathf.Clamp(40f * (1f - profile.NeckShare), -profile.MaxHeadYaw, profile.MaxHeadYaw),
                    distribution.HeadYaw, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SmoothAngle_RespectsAuthoredMaxSpeed()
        {
            float value = HeadLookMotionSolver.SmoothAngle(
                current: 0f,
                target: 100f,
                sharpness: 100f,
                maxSpeedDegrees: 10f,
                deltaTime: 0.5f);

            Assert.AreEqual(5f, value, 0.001f);
        }

        private static void Set(ConvaiGazeHeadProfile profile, string fieldName, float value)
        {
            typeof(ConvaiGazeHeadProfile)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(profile, value);
        }

        private static void InvokeOnValidate(ConvaiGazeHeadProfile profile)
        {
            typeof(ConvaiGazeHeadProfile)
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(profile, null);
        }
    }
}
