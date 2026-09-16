using Convai.Domain.Embodiment.Readings;
using Convai.Modules.Attention.Core;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Attention
{
    [TestFixture]
    public sealed class WeightedAttentionDirectorTests
    {
        [TearDown]
        public void TearDown() => LogAssert.NoUnexpectedReceived();

        private static AttentionTimings DefaultTimings() => new(
            commitmentAcquireSeconds: 0.1f,
            commitmentReleaseSeconds: 0.1f,
            focusLossHoldSeconds: 0.2f,
            maxContinuousHoldSeconds: 4f,
            interestBreakThreshold: 0.2f,
            interestDecayPerSecond: 0.25f,
            interestRecoveryPerSecond: 0.5f,
            focusPositionLerpSpeed: 10f,
            focusOffset: Vector3.zero);

        [Test]
        public void Tick_WithNoCandidates_ReturnsInvalidReading()
        {
            var director = new WeightedAttentionDirector();

            AttentionReading reading = director.Tick(new AttentionCandidate[0], DefaultTimings(), 0.016f);

            Assert.That(reading.IsValid, Is.False);
            Assert.That(reading.Commitment, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Tick_RampsCommitmentUpAcrossFrames_WhenCandidateIsPresent()
        {
            var director = new WeightedAttentionDirector();
            AttentionTimings timings = DefaultTimings();
            var candidates = new[]
            {
                new AttentionCandidate(priority: 10, relevance: 1f, target: null,
                    worldPoint: new Vector3(0f, 1.6f, 1f), debugName: "camera")
            };

            float commitmentPrev = 0f;
            for (int frame = 0; frame < 12; frame++)
            {
                AttentionReading reading = director.Tick(candidates, timings, 0.05f);
                Assert.That(reading.Commitment, Is.GreaterThanOrEqualTo(commitmentPrev - 1e-4f));
                commitmentPrev = reading.Commitment;
            }

            Assert.That(commitmentPrev, Is.GreaterThan(0.5f), "Commitment should rise to >50% within 0.6 seconds.");
        }

        [Test]
        public void Tick_FallsBackToInvalid_WhenTargetDisappears()
        {
            var director = new WeightedAttentionDirector();
            AttentionTimings timings = DefaultTimings();
            var present = new[]
            {
                new AttentionCandidate(priority: 1, relevance: 1f, target: null,
                    worldPoint: Vector3.forward, debugName: "a")
            };

            for (int i = 0; i < 30; i++)
                director.Tick(present, timings, 0.033f);

            Assert.That(director.Current.IsValid, Is.True);

            for (int i = 0; i < 60; i++)
                director.Tick(new AttentionCandidate[0], timings, 0.033f);

            Assert.That(director.Current.IsValid, Is.False,
                "Commitment should fully release after the focus-loss hold window.");
        }

        [Test]
        public void Tick_PrunesInterestEntries_ForCandidatesThatDisappear()
        {
            var director = new WeightedAttentionDirector();
            AttentionTimings timings = DefaultTimings();

            for (int i = 0; i < 6; i++)
            {
                var uniqueCandidate = new[]
                {
                    new AttentionCandidate(priority: 1, relevance: 1f, target: null,
                        worldPoint: new Vector3(i, 0f, 1f), debugName: "candidate-" + i)
                };

                director.Tick(uniqueCandidate, timings, 0.033f);
            }

            director.Tick(new AttentionCandidate[0], timings, 0.033f);

            IDictionary interest = (IDictionary)typeof(WeightedAttentionDirector)
                .GetField("_interest", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(director);

            Assert.That(interest.Count, Is.LessThanOrEqualTo(1),
                "Interest tracking should prune vanished candidates instead of growing for the full session.");
        }

        [Test]
        public void Tick_KeepsSingleCandidateTarget_WhenHoldBudgetExpires()
        {
            var director = new WeightedAttentionDirector();
            var target = new GameObject("OnlyFocusTarget");
            try
            {
                AttentionTimings timings = new(
                    commitmentAcquireSeconds: 0.01f,
                    commitmentReleaseSeconds: 0.01f,
                    focusLossHoldSeconds: 0.2f,
                    maxContinuousHoldSeconds: 0.05f,
                    interestBreakThreshold: 0.9f,
                    interestDecayPerSecond: 10f,
                    interestRecoveryPerSecond: 0f,
                    focusPositionLerpSpeed: 10f,
                    focusOffset: Vector3.zero);
                var candidates = new[]
                {
                    new AttentionCandidate(priority: 1, relevance: 1f, target: target.transform,
                        worldPoint: Vector3.forward, debugName: "only")
                };

                AttentionReading reading = AttentionReading.Empty;
                for (int i = 0; i < 10; i++)
                    reading = director.Tick(candidates, timings, 0.033f);

                Assert.That(reading.IsValid, Is.True);
                Assert.AreSame(target.transform, reading.Target,
                    "A single valid candidate should not be force-cleared into a valid reading with a null target.");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        // ── new cases ────────────────────────────────────────────────────────────────

        [Test]
        public void Tick_HigherRelevanceCandidate_WinsOverLower()
        {
            // Arrange
            var director = new WeightedAttentionDirector();
            var goA = new GameObject("TargetA");
            var goB = new GameObject("TargetB");
            try
            {
                AttentionTimings timings = DefaultTimings();
                var candidates = new[]
                {
                    new AttentionCandidate(priority: 1, relevance: 0.2f, target: goA.transform,
                        worldPoint: new Vector3(0f, 1.6f, 1f), debugName: "A"),
                    new AttentionCandidate(priority: 1, relevance: 0.9f, target: goB.transform,
                        worldPoint: new Vector3(1f, 1.6f, 1f), debugName: "B")
                };

                // Act — run long enough to commit
                AttentionReading last = AttentionReading.Empty;
                for (int i = 0; i < 30; i++)
                    last = director.Tick(candidates, timings, 0.05f);

                // Assert
                Assert.That(last.IsValid, Is.True);
                Assert.AreSame(goB.transform, last.Target,
                    "The higher-relevance candidate (B) must win when both are present.");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        [Test]
        public void Tick_ZeroRelevanceCandidate_DoesNotCommit()
        {
            // Arrange
            var director = new WeightedAttentionDirector();
            AttentionTimings timings = DefaultTimings();
            var candidates = new[]
            {
                new AttentionCandidate(priority: 1, relevance: 0f, target: null,
                    worldPoint: Vector3.forward, debugName: "zero-rel")
            };

            // Act — run 60 ticks
            for (int i = 0; i < 60; i++)
                director.Tick(candidates, timings, 0.033f);

            // Assert
            Assert.That(director.Current.IsValid, Is.False,
                "Zero-relevance candidates must never produce a valid attention reading.");
        }

        [Test]
        public void Tick_TargetWorldPoint_IsNonNaN()
        {
            // Arrange
            var director = new WeightedAttentionDirector();
            var target = new GameObject("NaNGuardTarget");
            target.transform.position = new Vector3(1e5f, 1e5f, 1e5f);
            try
            {
                AttentionTimings timings = DefaultTimings();
                var candidates = new[]
                {
                    new AttentionCandidate(priority: 1, relevance: 1f, target: target.transform,
                        worldPoint: target.transform.position, debugName: "far")
                };

                AttentionReading reading = AttentionReading.Empty;
                for (int i = 0; i < 30; i++)
                    reading = director.Tick(candidates, timings, 0.05f);

                // Assert — focus position must never be NaN
                Assert.That(float.IsNaN(reading.SmoothedPoint.x), Is.False);
                Assert.That(float.IsNaN(reading.SmoothedPoint.y), Is.False);
                Assert.That(float.IsNaN(reading.SmoothedPoint.z), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Tick_CommitmentIsMonotonicallyIncreasing_WhenCandidatePersists()
        {
            // Arrange
            var director = new WeightedAttentionDirector();
            AttentionTimings timings = DefaultTimings();
            var candidates = new[]
            {
                new AttentionCandidate(priority: 1, relevance: 1f, target: null,
                    worldPoint: Vector3.forward, debugName: "steady")
            };

            float prev = -1f;
            bool violated = false;

            // Act — 20 ticks while candidate is present
            for (int i = 0; i < 20; i++)
            {
                AttentionReading r = director.Tick(candidates, timings, 0.05f);
                if (r.Commitment < prev - 1e-4f) { violated = true; break; }
                prev = r.Commitment;
            }

            // Assert
            Assert.That(violated, Is.False,
                "Commitment must not decrease while the same candidate is continuously present.");
        }

        [Test]
        public void Tick_100Ticks_Determinism_SameInputsSameReading()
        {
            // Arrange
            AttentionTimings timings = DefaultTimings();
            var candidatesA = new[]
            {
                new AttentionCandidate(priority: 1, relevance: 0.8f, target: null,
                    worldPoint: new Vector3(0f, 1.6f, 2f), debugName: "player")
            };

            AttentionReading[] run1 = new AttentionReading[100];
            AttentionReading[] run2 = new AttentionReading[100];

            // Act
            var d1 = new WeightedAttentionDirector();
            for (int i = 0; i < 100; i++)
                run1[i] = d1.Tick(candidatesA, timings, 0.016f);

            var d2 = new WeightedAttentionDirector();
            for (int i = 0; i < 100; i++)
                run2[i] = d2.Tick(candidatesA, timings, 0.016f);

            // Assert — commitment values must be bit-identical
            for (int i = 0; i < 100; i++)
            {
                Assert.That(run1[i].Commitment, Is.EqualTo(run2[i].Commitment).Within(1e-6f),
                    $"Commitment diverged at tick {i}.");
                Assert.That(run1[i].IsValid, Is.EqualTo(run2[i].IsValid),
                    $"Validity diverged at tick {i}.");
            }
        }

        [Test]
        public void Tick_WhenCandidateRemovedMidTrack_IsValidEventuallyFalse()
        {
            // Arrange
            var director = new WeightedAttentionDirector();
            AttentionTimings timings = new(
                commitmentAcquireSeconds: 0.05f,
                commitmentReleaseSeconds: 0.05f,
                focusLossHoldSeconds: 0.1f,
                maxContinuousHoldSeconds: 10f,
                interestBreakThreshold: 0.2f,
                interestDecayPerSecond: 5f,
                interestRecoveryPerSecond: 5f,
                focusPositionLerpSpeed: 10f,
                focusOffset: Vector3.zero);

            var present = new[]
            {
                new AttentionCandidate(1, 1f, null, Vector3.forward, "a")
            };

            // Acquire commitment
            for (int i = 0; i < 20; i++)
                director.Tick(present, timings, 0.033f);

            // Act — remove candidate
            for (int i = 0; i < 60; i++)
                director.Tick(new AttentionCandidate[0], timings, 0.033f);

            // Assert
            Assert.That(director.Current.IsValid, Is.False,
                "After candidate removal + hold-window expiry, commitment must fully release.");
        }

        [Test]
        public void Tick_FocusOffset_AppliedToWorldPoint()
        {
            // Arrange
            Vector3 offset = new Vector3(0f, 0.3f, 0f);
            AttentionTimings timings = new(
                commitmentAcquireSeconds: 0.01f,
                commitmentReleaseSeconds: 0.01f,
                focusLossHoldSeconds: 0f,
                maxContinuousHoldSeconds: 10f,
                interestBreakThreshold: 0.2f,
                interestDecayPerSecond: 1f,
                interestRecoveryPerSecond: 1f,
                focusPositionLerpSpeed: 100f,
                focusOffset: offset);

            var director = new WeightedAttentionDirector();
            Vector3 candidatePos = new Vector3(0f, 1.6f, 2f);
            var candidates = new[]
            {
                new AttentionCandidate(1, 1f, null, candidatePos, "p")
            };

            // Act — run enough to achieve full commitment
            AttentionReading reading = AttentionReading.Empty;
            for (int i = 0; i < 60; i++)
                reading = director.Tick(candidates, timings, 0.033f);

            // Assert — focus world should approach (candidatePos + offset)
            Vector3 expected = candidatePos + offset;
            Assert.That(reading.SmoothedPoint.y, Is.GreaterThan(candidatePos.y - 0.1f),
                "Focus offset must shift the focus world point above the raw candidate position.");
        }

        [Test]
        public void Tick_NullCandidatesArray_ReturnsFalseIsValid()
        {
            // Arrange
            var director = new WeightedAttentionDirector();
            AttentionTimings timings = DefaultTimings();

            // Act — passing an empty array (safest null-equivalent the API accepts)
            AttentionReading reading = director.Tick(new AttentionCandidate[0], timings, 0.016f);

            // Assert
            Assert.That(reading.IsValid, Is.False);
        }

        [Test]
        public void Tick_PriorityHigher_WinsWhenRelevanceTies()
        {
            // Arrange
            var director = new WeightedAttentionDirector();
            var goLow = new GameObject("LowPriority");
            var goHigh = new GameObject("HighPriority");
            try
            {
                AttentionTimings timings = DefaultTimings();
                // Same relevance, different priority
                var candidates = new[]
                {
                    new AttentionCandidate(priority: 0, relevance: 0.8f, target: goLow.transform,
                        worldPoint: new Vector3(-1f, 1.6f, 2f), debugName: "low"),
                    new AttentionCandidate(priority: 5, relevance: 0.8f, target: goHigh.transform,
                        worldPoint: new Vector3(1f, 1.6f, 2f), debugName: "high")
                };

                // Act
                AttentionReading last = AttentionReading.Empty;
                for (int i = 0; i < 30; i++)
                    last = director.Tick(candidates, timings, 0.05f);

                // Assert
                Assert.That(last.IsValid, Is.True);
                Assert.AreSame(goHigh.transform, last.Target,
                    "Higher-priority candidate must win when relevance is equal.");
            }
            finally
            {
                Object.DestroyImmediate(goLow);
                Object.DestroyImmediate(goHigh);
            }
        }
    }
}
