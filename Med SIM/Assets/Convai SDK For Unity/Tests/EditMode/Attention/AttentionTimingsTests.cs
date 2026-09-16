using Convai.Modules.Attention.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Attention
{
    /// <summary>
    ///     Contract tests for <see cref="AttentionTimings" />: clamping, defaults, value identity.
    /// </summary>
    [TestFixture]
    public sealed class AttentionTimingsTests
    {
        [TearDown]
        public void TearDown() => LogAssert.NoUnexpectedReceived();

        [Test]
        public void Constructor_NegativeCommitmentAcquire_ClampsToMinimum()
        {
            // Arrange / Act
            var timings = new AttentionTimings(
                commitmentAcquireSeconds: -5f,
                commitmentReleaseSeconds: 0.1f,
                focusLossHoldSeconds: 0f,
                maxContinuousHoldSeconds: 4f,
                interestBreakThreshold: 0.2f,
                interestDecayPerSecond: 1f,
                interestRecoveryPerSecond: 1f,
                focusPositionLerpSpeed: 10f,
                focusOffset: Vector3.zero);

            // Assert — clamped to Mathf.Max(0.01f, …)
            Assert.That(timings.CommitmentAcquireSeconds, Is.GreaterThanOrEqualTo(0.01f));
        }

        [Test]
        public void Constructor_NegativeDecayAndRecovery_ClampToZero()
        {
            // Arrange / Act
            var timings = new AttentionTimings(
                commitmentAcquireSeconds: 0.1f,
                commitmentReleaseSeconds: 0.1f,
                focusLossHoldSeconds: -1f,
                maxContinuousHoldSeconds: 4f,
                interestBreakThreshold: 0.2f,
                interestDecayPerSecond: -99f,
                interestRecoveryPerSecond: -99f,
                focusPositionLerpSpeed: 0f,
                focusOffset: Vector3.zero);

            // Assert
            Assert.That(timings.FocusLossHoldSeconds, Is.EqualTo(0f));
            Assert.That(timings.InterestDecayPerSecond, Is.EqualTo(0f));
            Assert.That(timings.InterestRecoveryPerSecond, Is.EqualTo(0f));
        }

        [Test]
        public void Constructor_InterestBreakThreshold_ClampedToUnitRange()
        {
            // Arrange / Act — out-of-range values
            var above = new AttentionTimings(0.1f, 0.1f, 0f, 4f, 5f, 1f, 1f, 10f, Vector3.zero);
            var below = new AttentionTimings(0.1f, 0.1f, 0f, 4f, -1f, 1f, 1f, 10f, Vector3.zero);

            // Assert
            Assert.That(above.InterestBreakThreshold, Is.LessThanOrEqualTo(1f));
            Assert.That(below.InterestBreakThreshold, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void Constructor_FocusOffset_IsPreservedExact()
        {
            // Arrange
            Vector3 expected = new Vector3(0.1f, 0.2f, 0.3f);

            // Act
            var timings = new AttentionTimings(0.1f, 0.1f, 0f, 4f, 0.2f, 1f, 1f, 10f, expected);

            // Assert
            Assert.That(timings.FocusOffset, Is.EqualTo(expected));
        }
    }
}
