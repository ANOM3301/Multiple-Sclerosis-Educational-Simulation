using System;
using Convai.Domain.Errors;
using Convai.Infrastructure.Networking;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Infrastructure
{
    [TestFixture]
    public class RoomInitializationFailureSupportTests
    {
        [Test]
        public void FromRequestFailure_WithHttp400InvalidStoredSession_PublishesBadRequestWithoutRetry()
        {
            RoomInitializationFailureOutcome outcome = RoomInitializationFailureSupport.FromRequestFailure(
                "stored-session",
                enableSessionResume: true,
                failureMessage: "HTTP request failed: Bad Request (Code: 400). Response: Invalid character_session_id",
                InvalidStoredSessionRecoveryPolicy.RetryWithoutStoredSessionDisallowed,
                responseCode: 400,
                sessionErrorMessage: "HTTP request failed: Bad Request (Code: 400)");

            Assert.That(outcome.Category, Is.EqualTo(RoomInitializationFailureCategory.RoomDetailsRequestFailed));
            Assert.That(outcome.RecoveryOutcome.Status,
                Is.EqualTo(RoomInitializationOutcomeStatus.InvalidStoredSession));
            Assert.That(outcome.RecoveryOutcome.ShouldRetryWithoutStoredSession, Is.False);
            Assert.That(outcome.RecoveryOutcome.ShouldClearStoredSession, Is.False);
            Assert.That(outcome.ShouldPublishSessionError, Is.True);
            Assert.That(outcome.SessionErrorCode, Is.EqualTo(SessionErrorCodes.ConnectionBadRequest));
            Assert.That(outcome.SessionErrorMessage, Is.EqualTo("HTTP request failed: Bad Request (Code: 400)"));
            Assert.That(outcome.SessionErrorIsRecoverable, Is.False);
            Assert.That(outcome.DiagnosticsMetadata, Does.Contain("responseCode=400"));
        }

        [Test]
        public void FromRequestException_WithFetchException_PreservesDetailedFailureAndRecoverableHttpCode()
        {
            var exception = new RoomInitializationFetchException(
                "HTTP request failed: Service Unavailable (Code: 503). Response: temporary outage",
                "HTTP request failed: Service Unavailable (Code: 503)",
                responseCode: 503);

            RoomInitializationFailureOutcome outcome = RoomInitializationFailureSupport.FromRequestException(
                attemptedCharacterSessionId: null,
                enableSessionResume: false,
                exception,
                InvalidStoredSessionRecoveryPolicy.RetryWithoutStoredSessionAllowed);

            Assert.That(outcome.FailureMessage,
                Is.EqualTo("HTTP request failed: Service Unavailable (Code: 503). Response: temporary outage"));
            Assert.That(outcome.SessionErrorMessage,
                Is.EqualTo("HTTP request failed: Service Unavailable (Code: 503)"));
            Assert.That(outcome.SessionErrorCode, Is.EqualTo(SessionErrorCodes.ConnectionServiceUnavailable));
            Assert.That(outcome.SessionErrorIsRecoverable, Is.True);
        }

        [Test]
        public void FromRequestException_WithGenericException_MapsConnectionFailed()
        {
            RoomInitializationFailureOutcome outcome = RoomInitializationFailureSupport.FromRequestException(
                attemptedCharacterSessionId: null,
                enableSessionResume: false,
                new Exception("boom"),
                InvalidStoredSessionRecoveryPolicy.RetryWithoutStoredSessionAllowed);

            Assert.That(outcome.Category, Is.EqualTo(RoomInitializationFailureCategory.RoomDetailsRequestFailed));
            Assert.That(outcome.SessionErrorCode, Is.EqualTo(SessionErrorCodes.ConnectionFailed));
            Assert.That(outcome.SessionErrorMessage, Is.EqualTo("boom"));
            Assert.That(outcome.ShouldPublishSessionError, Is.True);
            Assert.That(outcome.RecoveryOutcome.Status, Is.EqualTo(RoomInitializationOutcomeStatus.Failed));
        }

        [Test]
        public void FromInvalidRoomDetails_DoesNotProducePublishableSessionError()
        {
            RoomInitializationFailureOutcome outcome = RoomInitializationFailureSupport.FromInvalidRoomDetails(
                "stored-session",
                enableSessionResume: true,
                failureMessage: "Failed to get room details",
                InvalidStoredSessionRecoveryPolicy.RetryWithoutStoredSessionDisallowed);

            Assert.That(outcome.Category, Is.EqualTo(RoomInitializationFailureCategory.InvalidRoomDetails));
            Assert.That(outcome.RecoveryOutcome.Status, Is.EqualTo(RoomInitializationOutcomeStatus.Failed));
            Assert.That(outcome.ShouldPublishSessionError, Is.True);
            Assert.That(outcome.SessionErrorCode, Is.EqualTo(SessionErrorCodes.ConnectionFailed));
            Assert.That(outcome.SessionErrorMessage, Is.EqualTo("Failed to get room details"));
        }
    }
}
