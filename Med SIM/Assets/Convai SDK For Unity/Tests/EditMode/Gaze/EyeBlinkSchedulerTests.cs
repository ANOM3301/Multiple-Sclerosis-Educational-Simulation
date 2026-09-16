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
    public sealed class EyeBlinkSchedulerTests
    {
        [TearDown]
        public void TearDown() => LogAssert.NoUnexpectedReceived();

        // ── tests ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Reset_WithNullProfile_SetsDefaultInterval()
        {
            // Arrange
            var scheduler = new EyeBlinkScheduler();

            // Act
            scheduler.Reset(null);

            // Assert — cycle should not be in progress and EvaluateWeight returns 0
            Assert.That(scheduler.IsBlinking, Is.False);
            Assert.That(scheduler.EvaluateWeight(null), Is.EqualTo(0f));
        }

        [Test]
        public void Reset_WithProfile_ClearsCycleState()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var scheduler = new EyeBlinkScheduler();
            // Drive a tick long enough to start a blink cycle (default mean = 3.2s).
            for (int i = 0; i < 70; i++)
                scheduler.Tick(profile, 0.05f, ref rng);

            try
            {
                // Act
                scheduler.Reset(profile);

                // Assert
                Assert.That(scheduler.IsBlinking, Is.False);
                Assert.That(scheduler.CycleRemaining, Is.EqualTo(0f).Within(1e-5f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Tick_WithNullProfile_NeverStartsCycle()
        {
            // Arrange
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var scheduler = new EyeBlinkScheduler();

            // Act — advance 10 seconds in 0.05 s steps
            for (int i = 0; i < 200; i++)
                scheduler.Tick(null, 0.05f, ref rng);

            // Assert
            Assert.That(scheduler.IsBlinking, Is.False,
                "Null profile must disable blinking; no cycle should ever start.");
        }

        [Test]
        public void Tick_AfterIntervalElapsed_StartsBlink()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var scheduler = new EyeBlinkScheduler();
            scheduler.Reset(profile);

            try
            {
                // Act — advance 4s (> default mean 3.2s + jitter) in 0.05s steps
                bool blinkSeen = false;
                for (int i = 0; i < 100; i++)
                {
                    scheduler.Tick(profile, 0.05f, ref rng);
                    if (scheduler.IsBlinking) { blinkSeen = true; break; }
                }

                // Assert
                Assert.That(blinkSeen, Is.True,
                    "A blink cycle must fire within 5 seconds given default profile timings.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void EvaluateWeight_DuringCycle_ReturnsSineShapedPositiveValue()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var scheduler = new EyeBlinkScheduler();
            scheduler.Reset(profile);

            try
            {
                // Advance until blink fires
                bool cycleActive = false;
                for (int i = 0; i < 100 && !cycleActive; i++)
                {
                    scheduler.Tick(profile, 0.05f, ref rng);
                    cycleActive = scheduler.IsBlinking;
                }

                // Act / Assert
                if (cycleActive)
                {
                    // At the instant the cycle fires, _cycleRemaining == BlinkCycleDuration
                    // (phase=0) and sin(0·π)=0, so weight is 0 at the very first frame.
                    // Advance one step (same dt used in the loop) to move into the rising
                    // portion of the sine envelope before evaluating.
                    scheduler.Tick(profile, 0.05f, ref rng);
                    float weight = scheduler.EvaluateWeight(profile);
                    Assert.That(weight, Is.GreaterThan(0f));
                    Assert.That(weight, Is.LessThanOrEqualTo(100f));
                }
                else
                {
                    Assert.Ignore("Blink cycle did not fire within the expected window — adjust test timing.");
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void EvaluateWeight_WhenNotBlinking_ReturnsZero()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            var scheduler = new EyeBlinkScheduler();
            scheduler.Reset(profile);

            try
            {
                // Assert — immediately after Reset, no cycle is active
                Assert.That(scheduler.EvaluateWeight(profile), Is.EqualTo(0f));
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
                float[] run1 = new float[100];
                float[] run2 = new float[100];

                // Act
                DeterministicEmbodimentRandom rng1 = DeterministicEmbodimentRandomFixture.Create();
                var s1 = new EyeBlinkScheduler();
                s1.Reset(profile);
                for (int i = 0; i < 100; i++)
                {
                    s1.Tick(profile, 0.05f, ref rng1);
                    run1[i] = s1.EvaluateWeight(profile);
                }

                DeterministicEmbodimentRandom rng2 = DeterministicEmbodimentRandomFixture.Create();
                var s2 = new EyeBlinkScheduler();
                s2.Reset(profile);
                for (int i = 0; i < 100; i++)
                {
                    s2.Tick(profile, 0.05f, ref rng2);
                    run2[i] = s2.EvaluateWeight(profile);
                }

                // Assert
                Assert.That(run1, Is.EqualTo(run2),
                    "Same seed must produce bit-identical weight outputs over 100 ticks.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Tick_CycleDecaysToZero_AfterDurationElapses()
        {
            // Arrange
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
            DeterministicEmbodimentRandom rng = DeterministicEmbodimentRandomFixture.Create();
            var scheduler = new EyeBlinkScheduler();
            scheduler.Reset(profile);

            try
            {
                // Wait for blink to start
                bool started = false;
                for (int i = 0; i < 100 && !started; i++)
                {
                    scheduler.Tick(profile, 0.05f, ref rng);
                    started = scheduler.IsBlinking;
                }

                if (!started) { Assert.Ignore("Blink did not start — adjust timing."); return; }

                // Act — advance well beyond the cycle duration (default 0.15s)
                for (int i = 0; i < 20; i++)
                    scheduler.Tick(profile, 0.05f, ref rng);

                // Assert
                Assert.That(scheduler.IsBlinking, Is.False,
                    "Blink cycle must complete within its authored duration.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
