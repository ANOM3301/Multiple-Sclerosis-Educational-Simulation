using System;
using System.Net.Http;
using System.Threading.Tasks;
using Convai.Runtime.Core.Policies;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Policies
{
    /// <summary>
    ///     Unit tests for <see cref="IRetryPolicy"/> and <see cref="ExponentialBackoffPolicy"/>.
    ///     Tests cover delay calculations, transient error detection, and boundary conditions.
    /// </summary>
    [TestFixture]
    public class RetryPolicyTests
    {
        private ExponentialBackoffPolicy _policy;

        [SetUp]
        public void SetUp()
        {
            _policy = new ExponentialBackoffPolicy();
        }

        [TearDown]
        public void TearDown()
        {
            _policy = null;
        }

        #region MaxAttempts Tests

        [Test]
        public void MaxAttempts_ReturnsExpectedCount()
        {
            Assert.AreEqual(4, _policy.MaxAttempts);
        }

        [Test]
        public void MaxAttempts_IsPositive()
        {
            Assert.Greater(_policy.MaxAttempts, 0);
        }

        #endregion

        #region GetDelay Tests

        [Test]
        public void GetDelay_Attempt0_ReturnsZero()
        {
            TimeSpan delay = _policy.GetDelay(0);
            Assert.AreEqual(TimeSpan.Zero, delay);
        }

        [Test]
        public void GetDelay_Attempt1_ReturnsOneSecond()
        {
            TimeSpan delay = _policy.GetDelay(1);
            Assert.AreEqual(TimeSpan.FromSeconds(1), delay);
        }

        [Test]
        public void GetDelay_Attempt2_ReturnsTwoSeconds()
        {
            TimeSpan delay = _policy.GetDelay(2);
            Assert.AreEqual(TimeSpan.FromSeconds(2), delay);
        }

        [Test]
        public void GetDelay_Attempt3_ReturnsFourSeconds()
        {
            TimeSpan delay = _policy.GetDelay(3);
            Assert.AreEqual(TimeSpan.FromSeconds(4), delay);
        }

        [Test]
        public void GetDelay_BeyondMaxAttempts_ReturnsLastDelay()
        {
            TimeSpan delay = _policy.GetDelay(10);
            Assert.AreEqual(TimeSpan.FromSeconds(4), delay);
        }

        [Test]
        public void GetDelay_NegativeAttempt_ReturnsZero()
        {
            TimeSpan delay = _policy.GetDelay(-1);
            Assert.AreEqual(TimeSpan.Zero, delay);
        }

        [Test]
        public void GetDelay_DelaysIncreaseExponentially()
        {
            TimeSpan delay0 = _policy.GetDelay(0);
            TimeSpan delay1 = _policy.GetDelay(1);
            TimeSpan delay2 = _policy.GetDelay(2);
            TimeSpan delay3 = _policy.GetDelay(3);

            Assert.AreEqual(TimeSpan.Zero, delay0);
            Assert.Less(delay1, delay2);
            Assert.Less(delay2, delay3);
        }

        #endregion

        #region ShouldRetry - Transient Error Tests

        [Test]
        public void ShouldRetry_TimeoutException_ReturnsTrue()
        {
            var exception = new TimeoutException("Connection timed out");
            Assert.IsTrue(_policy.ShouldRetry(exception, 0));
        }

        [Test]
        public void ShouldRetry_HttpRequestException_ReturnsTrue()
        {
            var exception = new HttpRequestException("Network error");
            Assert.IsTrue(_policy.ShouldRetry(exception, 0));
        }

        [Test]
        public void ShouldRetry_OperationCanceledException_ReturnsTrue()
        {
            var exception = new OperationCanceledException("Operation was cancelled");
            Assert.IsTrue(_policy.ShouldRetry(exception, 0));
        }

        [Test]
        public void ShouldRetry_TaskCanceledException_ReturnsTrue()
        {
            // TaskCanceledException inherits from OperationCanceledException
            var exception = new TaskCanceledException("Task was cancelled");
            Assert.IsTrue(_policy.ShouldRetry(exception, 0));
        }

        #endregion

        #region ShouldRetry - Non-Transient Error Tests

        [Test]
        public void ShouldRetry_ArgumentException_ReturnsFalse()
        {
            var exception = new ArgumentException("Invalid argument");
            Assert.IsFalse(_policy.ShouldRetry(exception, 0));
        }

        [Test]
        public void ShouldRetry_InvalidOperationException_ReturnsFalse()
        {
            var exception = new InvalidOperationException("Invalid state");
            Assert.IsFalse(_policy.ShouldRetry(exception, 0));
        }

        [Test]
        public void ShouldRetry_NullReferenceException_ReturnsFalse()
        {
            var exception = new NullReferenceException("Null reference");
            Assert.IsFalse(_policy.ShouldRetry(exception, 0));
        }

        [Test]
        public void ShouldRetry_NullException_ReturnsFalse()
        {
            Assert.IsFalse(_policy.ShouldRetry(null, 0));
        }

        #endregion

        #region ShouldRetry - Boundary Condition Tests

        [Test]
        public void ShouldRetry_AtLastAttempt_ReturnsFalse()
        {
            // Attempt 3 is the last valid attempt (0, 1, 2, 3)
            // After attempt 3 fails, we cannot retry
            var exception = new TimeoutException();
            Assert.IsFalse(_policy.ShouldRetry(exception, 3));
        }

        [Test]
        public void ShouldRetry_BeyondMaxAttempts_ReturnsFalse()
        {
            var exception = new TimeoutException();
            Assert.IsFalse(_policy.ShouldRetry(exception, 10));
        }

        [Test]
        public void ShouldRetry_NegativeAttempt_ReturnsFalse()
        {
            var exception = new TimeoutException();
            Assert.IsFalse(_policy.ShouldRetry(exception, -1));
        }

        [Test]
        public void ShouldRetry_SecondToLastAttempt_ReturnsTrue()
        {
            // Attempt 2 is second to last, so retry is allowed
            var exception = new TimeoutException();
            Assert.IsTrue(_policy.ShouldRetry(exception, 2));
        }

        [Test]
        public void ShouldRetry_AllAttemptsExceptLast_ReturnsTrue()
        {
            var exception = new TimeoutException();

            // Attempts 0, 1, 2 should allow retry
            for (int i = 0; i < _policy.MaxAttempts - 1; i++)
            {
                Assert.IsTrue(_policy.ShouldRetry(exception, i),
                    $"Attempt {i} should allow retry");
            }

            // Last attempt should not allow retry
            Assert.IsFalse(_policy.ShouldRetry(exception, _policy.MaxAttempts - 1),
                "Last attempt should not allow retry");
        }

        #endregion

        #region ShouldRetry - Wrapped Exception Tests

        [Test]
        public void ShouldRetry_WrappedTimeoutException_ReturnsTrue()
        {
            var innerException = new TimeoutException();
            var wrapperException = new Exception("Wrapper", innerException);
            Assert.IsTrue(_policy.ShouldRetry(wrapperException, 0));
        }

        [Test]
        public void ShouldRetry_AggregateExceptionWithTransient_ReturnsTrue()
        {
            var timeoutException = new TimeoutException();
            var aggregate = new AggregateException(timeoutException);
            Assert.IsTrue(_policy.ShouldRetry(aggregate, 0));
        }

        [Test]
        public void ShouldRetry_AggregateExceptionWithMixedErrors_ReturnsTrue()
        {
            // If any inner exception is transient, return true
            var argumentException = new ArgumentException();
            var timeoutException = new TimeoutException();
            var aggregate = new AggregateException(argumentException, timeoutException);
            Assert.IsTrue(_policy.ShouldRetry(aggregate, 0));
        }

        [Test]
        public void ShouldRetry_AggregateExceptionWithNoTransient_ReturnsFalse()
        {
            var argumentException = new ArgumentException();
            var invalidOpException = new InvalidOperationException();
            var aggregate = new AggregateException(argumentException, invalidOpException);
            Assert.IsFalse(_policy.ShouldRetry(aggregate, 0));
        }

        [Test]
        public void ShouldRetry_DeeplyNestedTransientException_ReturnsTrue()
        {
            var timeoutException = new TimeoutException();
            var level1 = new Exception("Level 1", timeoutException);
            var level2 = new Exception("Level 2", level1);
            Assert.IsTrue(_policy.ShouldRetry(level2, 0));
        }

        #endregion

        #region Integration Tests

        [Test]
        public void Policy_FullRetrySequence_BehavesCorrectly()
        {
            var transientException = new TimeoutException();
            int attemptCount = 0;

            // Simulate retry loop
            while (_policy.ShouldRetry(transientException, attemptCount))
            {
                attemptCount++;
            }

            // Should have tried MaxAttempts - 1 retries
            // (initial attempt + MaxAttempts-1 retries = MaxAttempts total)
            Assert.AreEqual(_policy.MaxAttempts - 1, attemptCount);
        }

        [Test]
        public void Policy_ImplementsInterface()
        {
            IRetryPolicy policy = _policy;
            Assert.IsNotNull(policy);
            Assert.AreEqual(4, policy.MaxAttempts);
            Assert.AreEqual(TimeSpan.Zero, policy.GetDelay(0));
        }

        #endregion
    }
}

