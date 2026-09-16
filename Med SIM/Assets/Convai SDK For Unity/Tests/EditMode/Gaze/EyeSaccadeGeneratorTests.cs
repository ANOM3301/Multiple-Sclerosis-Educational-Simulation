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
    public sealed class EyeSaccadeGeneratorTests
    {
        [TearDown]
        public void TearDown() => LogAssert.NoUnexpectedReceived();

        // ── tests ─────────────────────────────────────────────────────────────────────

        [Test]
        public void CurrentOffset_AtConstruction_IsZero()
        {
            // Arrange / Act
            var generator = new EyeSaccadeGenerator();

            // Assert
            Assert.That(generator.CurrentOffset, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Reset_WithNullProfile_OffsetRemainsZero()
        {
            // Arrange
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var generator = new EyeSaccadeGenerator();

            // Act
            generator.Reset(null);
            generator.Tick(null, 1f, ref rng);

            // Assert
            Assert.That(generator.CurrentOffset, Is.EqualTo(Vector2.zero),
                "Null profile must keep the saccade generator silent.");
        }

        [Test]
        public void Tick_WithSaccadesDisabled_OffsetAlwaysZero()
        {
            // Arrange — null profile triggers disabled path
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var generator = new EyeSaccadeGenerator();

            // Act — advance well past the default interval (1.8s)
            for (int i = 0; i < 100; i++)
                generator.Tick(null, 0.05f, ref rng);

            // Assert
            Assert.That(generator.CurrentOffset, Is.EqualTo(Vector2.zero),
                "Saccades must be suppressed when profile is null (disabled path).");
        }

        [Test]
        public void Tick_AfterIntervalElapsed_OffsetBecomesNonZero()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var generator = new EyeSaccadeGenerator();
            generator.Reset(profile);

            try
            {
                // Act — advance past default mean interval (1.8s) in small steps
                bool saccadeObserved = false;
                for (int i = 0; i < 200; i++)
                {
                    generator.Tick(profile, 0.02f, ref rng);
                    if (generator.CurrentOffset.sqrMagnitude > 1e-6f)
                    {
                        saccadeObserved = true;
                        break;
                    }
                }

                // Assert
                Assert.That(saccadeObserved, Is.True,
                    "A saccade pulse must occur within 4 seconds at default interval settings.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Tick_OffsetAmplitude_WithinProfileMaxDegrees()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var generator = new EyeSaccadeGenerator();
            generator.Reset(profile);

            try
            {
                // Act — run 20 seconds, track max observed magnitude
                float maxMagnitude = 0f;
                for (int i = 0; i < 400; i++)
                {
                    generator.Tick(profile, 0.05f, ref rng);
                    float mag = generator.CurrentOffset.magnitude;
                    if (mag > maxMagnitude) maxMagnitude = mag;
                }

                // Assert — envelope cannot exceed the diagonal of (maxDegrees, 0.5*maxDegrees)
                float maxAllowed = profile.SaccadeMaxDegrees * Mathf.Sqrt(1f + 0.25f) + 0.001f;
                Assert.That(maxMagnitude, Is.LessThanOrEqualTo(maxAllowed),
                    "Saccade offset must stay within the profile's SaccadeMaxDegrees limit.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Tick_100Ticks_Determinism_SameSeedSameOutput()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            try
            {
                var offsets1 = new Vector2[100];
                var offsets2 = new Vector2[100];

                // Act
                DeterministicEmbodimentRandom rng1 = DeterministicEmbodimentRandomFixture.Create();
                var g1 = new EyeSaccadeGenerator();
                g1.Reset(profile);
                for (int i = 0; i < 100; i++)
                {
                    g1.Tick(profile, 0.05f, ref rng1);
                    offsets1[i] = g1.CurrentOffset;
                }

                DeterministicEmbodimentRandom rng2 = DeterministicEmbodimentRandomFixture.Create();
                var g2 = new EyeSaccadeGenerator();
                g2.Reset(profile);
                for (int i = 0; i < 100; i++)
                {
                    g2.Tick(profile, 0.05f, ref rng2);
                    offsets2[i] = g2.CurrentOffset;
                }

                // Assert
                Assert.That(offsets1, Is.EqualTo(offsets2),
                    "Same seed must produce bit-identical saccade offsets over 100 ticks.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Tick_AfterReset_OffsetRestarts_FromZero()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var generator = new EyeSaccadeGenerator();

            try
            {
                // Advance past interval
                for (int i = 0; i < 200; i++)
                    generator.Tick(profile, 0.02f, ref rng);

                // Act — reset clears active cycle
                generator.Reset(profile);

                // Assert
                Assert.That(generator.CurrentOffset, Is.EqualTo(Vector2.zero),
                    "Reset must clear any in-progress saccade cycle.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
