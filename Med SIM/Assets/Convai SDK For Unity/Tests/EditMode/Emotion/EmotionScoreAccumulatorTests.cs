using System.Collections.Generic;
using Convai.Modules.Emotion.Core;
using Convai.Modules.Emotion.Taxonomy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Emotion
{
    [TestFixture]
    public sealed class EmotionScoreAccumulatorTests
    {
        private EmotionTaxonomyAsset _taxonomy;

        [SetUp]
        public void SetUp() => _taxonomy = EmotionTaxonomyAsset.CreateDefault();

        [TearDown]
        public void TearDown()
        {
            if (_taxonomy != null) Object.DestroyImmediate(_taxonomy);
            LogAssert.NoUnexpectedReceived();
        }

        // ── existing cases (preserved) ───────────────────────────────────────────────

        [Test]
        public void SetImmediateEmotion_SnapsOutputScoresInstantly()
        {
            // Arrange
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 5f, decaySpeed: 2f);

            // Act
            accumulator.SetImmediateEmotion("joy", 0.9f);

            // Assert
            Assert.That(accumulator.OutputScores["joy"], Is.EqualTo(0.9f).Within(1e-3f));
            accumulator.GetDominant(out string label, out float score);
            Assert.That(label, Is.EqualTo("joy"));
            Assert.That(score, Is.EqualTo(0.9f).Within(1e-3f));
        }

        [Test]
        public void Tick_SmoothsToTargetEmotionOverTime()
        {
            // Arrange
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 10f, decaySpeed: 5f);
            accumulator.SetTargetEmotion("anger", 1f);

            // Act
            for (int i = 0; i < 30; i++)
                accumulator.Tick(0.05f);

            // Assert
            Assert.That(accumulator.OutputScores["anger"], Is.GreaterThan(0.85f),
                "Smoothing should approach the target after ~1.5s.");
        }

        [Test]
        public void Tick_DecaysWhenTargetClearedToNeutral()
        {
            // Arrange
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 20f, decaySpeed: 10f);
            accumulator.SetImmediateEmotion("sadness", 1f);

            // Act
            accumulator.SetTargetEmotion(_taxonomy.Neutral.Label, 0f);
            for (int i = 0; i < 30; i++)
                accumulator.Tick(0.05f);

            // Assert
            Assert.That(accumulator.OutputScores["sadness"], Is.LessThan(0.05f),
                "Sadness should decay back toward zero.");
        }

        // ── new cases ────────────────────────────────────────────────────────────────

        [Test]
        public void Constructor_NullTaxonomy_Throws()
        {
            // Arrange / Act / Assert
            Assert.Throws<System.ArgumentNullException>(() =>
                _ = new EmotionScoreAccumulator(null));
        }

        [Test]
        public void GetDominant_WhenAllZero_ReturnsNeutralLabel()
        {
            // Arrange
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 5f, decaySpeed: 2f);

            // Act
            accumulator.GetDominant(out string label, out float score);

            // Assert
            Assert.That(label, Is.EqualTo(_taxonomy.Neutral.Label));
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void SetImmediateEmotion_ClearsAllOtherEmotionsInstantly()
        {
            // Arrange
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 5f, decaySpeed: 2f);
            accumulator.SetImmediateEmotion("joy", 1f);

            // Act
            accumulator.SetImmediateEmotion("anger", 0.5f);

            // Assert — joy must be cleared to zero
            Assert.That(accumulator.OutputScores["joy"], Is.EqualTo(0f).Within(1e-5f),
                "SetImmediateEmotion must zero all other emotions.");
            Assert.That(accumulator.OutputScores["anger"], Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void Reset_ClearsAllScoresAndBurst()
        {
            // Arrange
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 20f, decaySpeed: 20f);
            accumulator.SetImmediateEmotion("joy", 1f);

            // Act
            accumulator.Reset();

            // Assert — every emotion in the taxonomy should be zero
            foreach (KeyValuePair<string, float> kvp in accumulator.OutputScores)
                Assert.That(kvp.Value, Is.EqualTo(0f).Within(1e-5f),
                    $"Score for '{kvp.Key}' must be zero after Reset.");
        }

        [Test]
        public void OutputScores_AreAlwaysInUnitRange()
        {
            // Arrange
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 5f, decaySpeed: 2f);
            accumulator.SetImmediateEmotion("joy", 1f);
            accumulator.ConfigureMicroBurst(true, 0.25f, 1.8f, 0.05f);

            // Act — tick 60 frames
            for (int i = 0; i < 60; i++)
                accumulator.Tick(1f / 60f);

            // Assert
            foreach (KeyValuePair<string, float> kvp in accumulator.OutputScores)
            {
                Assert.That(kvp.Value, Is.GreaterThanOrEqualTo(0f),
                    $"Score '{kvp.Key}' below 0.");
                Assert.That(kvp.Value, Is.LessThanOrEqualTo(1f + 1e-4f),
                    $"Score '{kvp.Key}' above 1.");
            }
        }

        [Test]
        public void SetTargetEmotions_MultipleScores_DominantIsHighest()
        {
            // Arrange
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 50f, decaySpeed: 5f);
            var scores = new Dictionary<string, float>
            {
                { "joy",    0.3f },
                { "anger",  0.9f },
                { "sadness", 0.5f }
            };

            // Act
            accumulator.SetTargetEmotions(scores);
            for (int i = 0; i < 60; i++)
                accumulator.Tick(0.05f);

            accumulator.GetDominant(out string dominant, out _);

            // Assert
            Assert.That(dominant, Is.EqualTo("anger"),
                "Dominant emotion must be the one with the highest score.");
        }

        [Test]
        public void Tick_ZeroDelta_DoesNotChangeScores()
        {
            // Arrange
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 5f, decaySpeed: 2f);
            accumulator.SetImmediateEmotion("joy", 0.7f);
            float scoreBefore = accumulator.OutputScores["joy"];

            // Act
            accumulator.Tick(0f);

            // Assert
            Assert.That(accumulator.OutputScores["joy"], Is.EqualTo(scoreBefore).Within(1e-5f),
                "Zero deltaTime tick must be a no-op.");
        }

        [Test]
        public void MicroBurst_Enabled_OvershootsOnSignificantChange()
        {
            // Arrange — use a high overshoot to make the burst clearly observable
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 30f, decaySpeed: 5f);
            accumulator.ConfigureMicroBurst(true, duration: 0.3f, overshoot: 2.0f, threshold: 0.0f);

            // Snap joy to 0 first, then trigger a large positive delta
            accumulator.SetImmediateEmotion("joy", 0f);

            // Act — set target 1.0 to trigger burst (delta 1.0 > threshold 0.0)
            accumulator.SetTargetEmotion("joy", 1f);
            accumulator.Tick(0.05f); // first tick into burst window

            // Assert — with overshoot=2, output must exceed the underlying current score
            float raw = accumulator.OutputScores["joy"];
            Assert.That(raw, Is.GreaterThan(0f),
                "Burst overshoot must produce a positive output on the first tick.");
        }

        [Test]
        public void SetLerpSpeed_UpdatesSmoothing_AtRuntime()
        {
            // Arrange
            var accumulator = new EmotionScoreAccumulator(_taxonomy, lerpSpeed: 1f, decaySpeed: 1f);
            accumulator.SetTargetEmotion("joy", 1f);

            // Act — one tick at very slow speed
            accumulator.Tick(0.016f);
            float slowValue = accumulator.OutputScores["joy"];

            // Now switch to fast speed and tick again from same state
            accumulator.Reset();
            accumulator.SetLerpSpeed(100f);
            accumulator.SetTargetEmotion("joy", 1f);
            accumulator.Tick(0.016f);
            float fastValue = accumulator.OutputScores["joy"];

            // Assert
            Assert.That(fastValue, Is.GreaterThan(slowValue),
                "Higher lerpSpeed must produce a larger per-tick increment.");
        }
    }
}
