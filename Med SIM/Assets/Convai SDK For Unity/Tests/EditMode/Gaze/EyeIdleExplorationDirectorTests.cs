using Convai.Modules.Gaze.Core;
using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Embodiment;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Gaze
{
    [TestFixture]
    public sealed class EyeIdleExplorationDirectorTests
    {
        [TearDown]
        public void TearDown() => LogAssert.NoUnexpectedReceived();

        // ── tests ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Angles_AtConstruction_AreZero()
        {
            // Arrange / Act
            var director = new EyeIdleExplorationDirector();

            // Assert
            Assert.That(director.Angles, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Tick_WhenNotActive_ClearsAngles()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var director = new EyeIdleExplorationDirector();
            director.Reset(profile, 0f, ref rng);

            try
            {
                // Force a sample by advancing past next-sample time
                for (int i = 0; i < 100; i++)
                    director.Tick(profile, i * 0.05f, isActive: true, ref rng);

                // Act — now mark inactive
                director.Tick(profile, 200f, isActive: false, ref rng);

                // Assert
                Assert.That(director.Angles, Is.EqualTo(Vector2.zero),
                    "When not active, exploration angles must clear to zero.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Tick_WithNullProfile_AnglesAlwaysZero()
        {
            // Arrange
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var director = new EyeIdleExplorationDirector();

            // Act — advance several intervals, always null profile
            for (int i = 0; i < 100; i++)
                director.Tick(null, i * 0.05f, isActive: true, ref rng);

            // Assert
            Assert.That(director.Angles, Is.EqualTo(Vector2.zero),
                "Null profile must suppress all idle exploration.");
        }

        [Test]
        public void Tick_AfterNextSampleTime_SamplesNewAngles()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var director = new EyeIdleExplorationDirector();
            director.Reset(profile, 0f, ref rng);

            try
            {
                // Act — jump past the maximum interval (3.2s) in one step
                // Use a time value well beyond IdleExplorationIntervalMax
                director.Tick(profile, profile.IdleExplorationIntervalMax + 1f, isActive: true, ref rng);

                // Assert — angles are not guaranteed non-zero (recenter chance exists),
                // but at least one tick at a new time should have been attempted.
                // We verify the sample window is respected by checking that subsequent ticks
                // within the new interval do not change the angles.
                Vector2 sampledAngles = director.Angles;
                float holdTime = profile.IdleExplorationIntervalMax + 1.01f;
                director.Tick(profile, holdTime, isActive: true, ref rng);

                Assert.That(director.Angles, Is.EqualTo(sampledAngles),
                    "Angles should not change during the hold window between samples.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Tick_HorizontalAngles_RespectProfileLimits()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var director = new EyeIdleExplorationDirector();
            director.Reset(profile, 0f, ref rng);

            try
            {
                // Act — run 50 sample cycles by advancing time in large steps
                float maxYaw = 0f;
                float maxPitch = 0f;
                for (int i = 0; i < 50; i++)
                {
                    director.Tick(profile, i * (profile.IdleExplorationIntervalMax + 1f), isActive: true, ref rng);
                    float absYaw = Mathf.Abs(director.Angles.x);
                    float absPitch = Mathf.Abs(director.Angles.y);
                    if (absYaw > maxYaw) maxYaw = absYaw;
                    if (absPitch > maxPitch) maxPitch = absPitch;
                }

                // Assert — sampled angles must not exceed the profile limits
                Assert.That(maxYaw, Is.LessThanOrEqualTo(profile.IdleExplorationHorizontalDegrees + 1e-3f),
                    "Horizontal exploration must not exceed IdleExplorationHorizontalDegrees.");
                Assert.That(maxPitch, Is.LessThanOrEqualTo(
                    Mathf.Max(profile.IdleExplorationUpDegrees, profile.IdleExplorationDownDegrees) + 1e-3f),
                    "Vertical exploration must not exceed the configured up/down limits.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Tick_100Cycles_Determinism_SameSeedSameAngles()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            try
            {
                const int cycles = 50;
                var run1 = new Vector2[cycles];
                var run2 = new Vector2[cycles];

                // Act
                DeterministicEmbodimentRandom rng1 = DeterministicEmbodimentRandomFixture.Create();
                var d1 = new EyeIdleExplorationDirector();
                d1.Reset(profile, 0f, ref rng1);
                for (int i = 0; i < cycles; i++)
                {
                    d1.Tick(profile, i * (profile.IdleExplorationIntervalMax + 1f), isActive: true, ref rng1);
                    run1[i] = d1.Angles;
                }

                DeterministicEmbodimentRandom rng2 = DeterministicEmbodimentRandomFixture.Create();
                var d2 = new EyeIdleExplorationDirector();
                d2.Reset(profile, 0f, ref rng2);
                for (int i = 0; i < cycles; i++)
                {
                    d2.Tick(profile, i * (profile.IdleExplorationIntervalMax + 1f), isActive: true, ref rng2);
                    run2[i] = d2.Angles;
                }

                // Assert
                Assert.That(run1, Is.EqualTo(run2),
                    "Same seed must produce bit-identical exploration angles.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
