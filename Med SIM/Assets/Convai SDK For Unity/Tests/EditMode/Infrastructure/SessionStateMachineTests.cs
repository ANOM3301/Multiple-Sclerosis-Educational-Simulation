using System;
using System.Collections.Generic;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.EventSystem;
using Convai.Infrastructure.Networking.Services;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Infrastructure
{
    /// <summary>
    ///     Unit tests for SessionStateMachine covering all state transitions and edge cases.
    /// </summary>
    [TestFixture]
    public class SessionStateMachineTests
    {
        private static SessionError TestError(string code) => SessionError.Create(code, code);

        [SetUp]
        public void SetUp()
        {
            _eventHub = new EventHub(new ImmediateScheduler());
            _stateMachine = new SessionStateMachine(_eventHub);
            _stateChangedEvents = new List<SessionStateChanged>();
            _eventHub.Subscribe<SessionStateChanged>(evt => _stateChangedEvents.Add(evt));
        }

        [TearDown]
        public void TearDown()
        {
            _stateMachine = null;
            _stateChangedEvents = null;
            _eventHub = null;
        }

        private EventHub _eventHub;
        private SessionStateMachine _stateMachine;
        private List<SessionStateChanged> _stateChangedEvents;

        private sealed class ImmediateScheduler : IUnityScheduler
        {
            public void ScheduleOnMainThread(Action action) => action?.Invoke();
            public void ScheduleOnBackground(Action action) => action?.Invoke();
            public bool IsMainThread() => true;
        }

        [Test]
        public void InitialState_IsDisconnected() =>
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);

        [Test]
        public void InitialSessionId_IsNull() => Assert.IsNull(_stateMachine.SessionId);

        [Test]
        public void TryTransition_Disconnected_To_Connecting_Succeeds()
        {
            bool result = _stateMachine.TryTransition(SessionState.Connecting);

            Assert.IsTrue(result);
            Assert.AreEqual(SessionState.Connecting, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Connecting_To_Connected_Succeeds()
        {
            _stateMachine.TryTransition(SessionState.Connecting);

            bool result = _stateMachine.TryTransition(SessionState.Connected, "test-session-123");

            Assert.IsTrue(result);
            Assert.AreEqual(SessionState.Connected, _stateMachine.CurrentState);
            Assert.AreEqual("test-session-123", _stateMachine.SessionId);
        }

        [Test]
        public void TryTransition_Connecting_To_Error_Succeeds()
        {
            _stateMachine.TryTransition(SessionState.Connecting);

            bool result = _stateMachine.TryTransition(SessionState.Error, error: TestError("CONNECTION_TIMEOUT"));

            Assert.IsTrue(result);
            Assert.AreEqual(SessionState.Error, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Connected_To_Disconnecting_Succeeds()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);

            bool result = _stateMachine.TryTransition(SessionState.Disconnecting);

            Assert.IsTrue(result);
            Assert.AreEqual(SessionState.Disconnecting, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Connected_To_Reconnecting_Succeeds()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);

            bool result = _stateMachine.TryTransition(SessionState.Reconnecting);

            Assert.IsTrue(result);
            Assert.AreEqual(SessionState.Reconnecting, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Reconnecting_To_Connected_Succeeds()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);
            _stateMachine.TryTransition(SessionState.Reconnecting);

            bool result = _stateMachine.TryTransition(SessionState.Connected);

            Assert.IsTrue(result);
            Assert.AreEqual(SessionState.Connected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Reconnecting_To_Error_Succeeds()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);
            _stateMachine.TryTransition(SessionState.Reconnecting);

            bool result = _stateMachine.TryTransition(SessionState.Error, error: TestError("RECONNECT_FAILED"));

            Assert.IsTrue(result);
            Assert.AreEqual(SessionState.Error, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Disconnecting_To_Disconnected_Succeeds()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);
            _stateMachine.TryTransition(SessionState.Disconnecting);

            bool result = _stateMachine.TryTransition(SessionState.Disconnected);

            Assert.IsTrue(result);
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Error_To_Disconnected_Succeeds()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Error);

            bool result = _stateMachine.TryTransition(SessionState.Disconnected);

            Assert.IsTrue(result);
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Disconnected_To_Connected_Fails()
        {
            bool result = _stateMachine.TryTransition(SessionState.Connected);

            Assert.IsFalse(result);
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Disconnected_To_Reconnecting_Fails()
        {
            bool result = _stateMachine.TryTransition(SessionState.Reconnecting);

            Assert.IsFalse(result);
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Disconnected_To_Disconnecting_Fails()
        {
            bool result = _stateMachine.TryTransition(SessionState.Disconnecting);

            Assert.IsFalse(result);
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Disconnected_To_Error_Fails()
        {
            bool result = _stateMachine.TryTransition(SessionState.Error);

            Assert.IsFalse(result);
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Connecting_To_Disconnecting_Fails()
        {
            _stateMachine.TryTransition(SessionState.Connecting);

            bool result = _stateMachine.TryTransition(SessionState.Disconnecting);

            Assert.IsFalse(result);
            Assert.AreEqual(SessionState.Connecting, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Connecting_To_Reconnecting_Fails()
        {
            _stateMachine.TryTransition(SessionState.Connecting);

            bool result = _stateMachine.TryTransition(SessionState.Reconnecting);

            Assert.IsFalse(result);
            Assert.AreEqual(SessionState.Connecting, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Connected_To_Connecting_Fails()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);

            bool result = _stateMachine.TryTransition(SessionState.Connecting);

            Assert.IsFalse(result);
            Assert.AreEqual(SessionState.Connected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Connected_To_Error_Fails()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);

            bool result = _stateMachine.TryTransition(SessionState.Error);

            Assert.IsFalse(result);
            Assert.AreEqual(SessionState.Connected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_Error_To_Connecting_Fails()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Error);

            bool result = _stateMachine.TryTransition(SessionState.Connecting);

            Assert.IsFalse(result);
            Assert.AreEqual(SessionState.Error, _stateMachine.CurrentState);
        }

        [Test]
        public void ForceTransition_BypassesValidation()
        {
            _stateMachine.ForceTransition(SessionState.Connected, "forced-session");

            Assert.AreEqual(SessionState.Connected, _stateMachine.CurrentState);
            Assert.AreEqual("forced-session", _stateMachine.SessionId);
        }

        [Test]
        public void ForceTransition_CanTransitionToAnyState()
        {
            _stateMachine.ForceTransition(SessionState.Error, error: TestError("FORCED_ERROR"));

            Assert.AreEqual(SessionState.Error, _stateMachine.CurrentState);
        }

        [Test]
        public void ForceTransition_RaisesStateChangedEvent()
        {
            _stateMachine.ForceTransition(SessionState.Connected);

            Assert.AreEqual(1, _stateChangedEvents.Count);
            Assert.AreEqual(SessionState.Disconnected, _stateChangedEvents[0].OldState);
            Assert.AreEqual(SessionState.Connected, _stateChangedEvents[0].NewState);
        }

        [Test]
        public void TryTransition_RaisesStateChangedEvent_WithCorrectArguments()
        {
            _stateMachine.TryTransition(SessionState.Connecting);

            Assert.AreEqual(1, _stateChangedEvents.Count);
            SessionStateChanged evt = _stateChangedEvents[0];
            Assert.AreEqual(SessionState.Disconnected, evt.OldState);
            Assert.AreEqual(SessionState.Connecting, evt.NewState);
        }

        [Test]
        public void TryTransition_Connected_IncludesSessionIdInEvent()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "session-abc");

            Assert.AreEqual(2, _stateChangedEvents.Count);
            SessionStateChanged connectedEvent = _stateChangedEvents[1];
            Assert.AreEqual(SessionState.Connected, connectedEvent.NewState);
            Assert.AreEqual("session-abc", connectedEvent.SessionId);
        }

        [Test]
        public void TryTransition_Error_IncludesErrorCodeInEvent()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Error, error: TestError("NETWORK_ERROR"));

            Assert.AreEqual(2, _stateChangedEvents.Count);
            SessionStateChanged errorEvent = _stateChangedEvents[1];
            Assert.AreEqual(SessionState.Error, errorEvent.NewState);
            Assert.AreEqual("NETWORK_ERROR", errorEvent.ErrorCode);
        }

        [Test]
        public void InvalidTransition_DoesNotRaiseStateChangedEvent()
        {
            _stateMachine.TryTransition(SessionState.Connected);

            Assert.AreEqual(0, _stateChangedEvents.Count);
        }

        [Test]
        public void FullConnectionCycle_RaisesCorrectEvents()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "session-123");
            _stateMachine.TryTransition(SessionState.Disconnecting);
            _stateMachine.TryTransition(SessionState.Disconnected);

            Assert.AreEqual(4, _stateChangedEvents.Count);
            Assert.AreEqual(SessionState.Connecting, _stateChangedEvents[0].NewState);
            Assert.AreEqual(SessionState.Connected, _stateChangedEvents[1].NewState);
            Assert.AreEqual(SessionState.Disconnecting, _stateChangedEvents[2].NewState);
            Assert.AreEqual(SessionState.Disconnected, _stateChangedEvents[3].NewState);
        }

        [Test]
        public void Reset_SetsStateToDisconnected()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);

            _stateMachine.Reset();

            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void Reset_ClearsSessionId()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "session-to-clear");

            _stateMachine.Reset();

            Assert.IsNull(_stateMachine.SessionId);
        }

        [Test]
        public void Reset_RaisesStateChangedEvent_WhenNotAlreadyDisconnected()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateChangedEvents.Clear();

            _stateMachine.Reset();

            Assert.AreEqual(1, _stateChangedEvents.Count);
            Assert.AreEqual(SessionState.Connecting, _stateChangedEvents[0].OldState);
            Assert.AreEqual(SessionState.Disconnected, _stateChangedEvents[0].NewState);
        }

        [Test]
        public void Reset_DoesNotRaiseEvent_WhenAlreadyDisconnected()
        {
            _stateMachine.Reset();

            Assert.AreEqual(0, _stateChangedEvents.Count);
        }

        [Test]
        public void Reset_FromErrorState_Succeeds()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Error);

            _stateMachine.Reset();

            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void CanTransitionTo_ValidTransition_ReturnsTrue() =>
            Assert.IsTrue(_stateMachine.CanTransitionTo(SessionState.Connecting));

        [Test]
        public void CanTransitionTo_InvalidTransition_ReturnsFalse() =>
            Assert.IsFalse(_stateMachine.CanTransitionTo(SessionState.Connected));

        [Test]
        public void CanTransitionTo_ReflectsCurrentState()
        {
            Assert.IsFalse(_stateMachine.CanTransitionTo(SessionState.Disconnecting));

            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);

            Assert.IsTrue(_stateMachine.CanTransitionTo(SessionState.Disconnecting));
        }

        [Test]
        public void CanTransitionTo_FromReconnecting_AllowsOnlyConnectedAndError()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);
            _stateMachine.TryTransition(SessionState.Reconnecting);

            Assert.IsTrue(_stateMachine.CanTransitionTo(SessionState.Connected));
            Assert.IsTrue(_stateMachine.CanTransitionTo(SessionState.Error));
            Assert.IsFalse(_stateMachine.CanTransitionTo(SessionState.Disconnected));
        }

        [Test]
        public void SessionId_IsSetOnConnected()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "new-session-id");

            Assert.AreEqual("new-session-id", _stateMachine.SessionId);
        }

        [Test]
        public void SessionId_IsPreservedDuringReconnecting()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "original-session");
            _stateMachine.TryTransition(SessionState.Reconnecting);

            Assert.AreEqual("original-session", _stateMachine.SessionId);
        }

        [Test]
        public void SessionId_IsClearedOnDisconnected()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "session-to-clear");
            _stateMachine.TryTransition(SessionState.Disconnecting);
            _stateMachine.TryTransition(SessionState.Disconnected);

            Assert.IsNull(_stateMachine.SessionId);
        }

        [Test]
        public void SessionId_CanBeUpdatedOnReconnection()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "old-session");
            _stateMachine.TryTransition(SessionState.Reconnecting);
            _stateMachine.TryTransition(SessionState.Connected, "new-session");

            Assert.AreEqual("new-session", _stateMachine.SessionId);
        }

        [Test]
        public void SessionId_NotUpdatedIfNullOnConnected()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "original");
            _stateMachine.TryTransition(SessionState.Reconnecting);
            _stateMachine.TryTransition(SessionState.Connected);

            Assert.AreEqual("original", _stateMachine.SessionId);
        }

        [Test]
        public void SessionId_RemainsNull_WhenConnectedWithoutSessionId()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);

            Assert.AreEqual(SessionState.Connected, _stateMachine.CurrentState);
            Assert.IsNull(_stateMachine.SessionId);
        }

        [Test]
        public void SessionId_TreatsEmptyStringAsNull_WhenConnected()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, string.Empty);

            Assert.AreEqual(SessionState.Connected, _stateMachine.CurrentState);
            Assert.IsNull(_stateMachine.SessionId);
        }

        [Test]
        public void SessionId_IsPreservedInErrorState_AfterReconnectFailure()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "session-in-error");
            _stateMachine.TryTransition(SessionState.Reconnecting);
            _stateMachine.TryTransition(SessionState.Error, error: TestError("RECONNECT_FAILED"));

            Assert.AreEqual(SessionState.Error, _stateMachine.CurrentState);
            Assert.AreEqual("session-in-error", _stateMachine.SessionId);
        }

        [Test]
        public void FullReconnectionCycle_WorksCorrectly()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "session-123");

            _stateMachine.TryTransition(SessionState.Reconnecting);
            Assert.AreEqual(SessionState.Reconnecting, _stateMachine.CurrentState);
            Assert.AreEqual("session-123", _stateMachine.SessionId);

            _stateMachine.TryTransition(SessionState.Connected);
            Assert.AreEqual(SessionState.Connected, _stateMachine.CurrentState);
        }

        [Test]
        public void ErrorRecoveryCycle_WorksCorrectly()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Error, error: TestError("AUTH_FAILED"));

            _stateMachine.TryTransition(SessionState.Disconnected);
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);

            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "new-session");
            Assert.AreEqual(SessionState.Connected, _stateMachine.CurrentState);
        }
    }
}
