using System;
using System.Collections.Generic;
using Convai.Domain.Abstractions;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.EventSystem;
using Convai.Infrastructure.Networking.Services;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Integration
{
    /// <summary>
    ///     Integration tests for SessionStateMachine and SessionService.
    ///     Tests the complete session lifecycle and event flow.
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class SessionStateMachineIntegrationTests
    {
        [SetUp]
        public void SetUp()
        {
            _eventHub = new EventHub(new ImmediateScheduler());
            _stateMachine = new SessionStateMachine(_eventHub);
            _stateChanges = new List<SessionStateChanged>();

            _eventHub.Subscribe<SessionStateChanged>(evt => _stateChanges.Add(evt), EventDeliveryPolicy.Immediate);
        }

        [TearDown]
        public void TearDown()
        {
            _eventHub = null;
            _stateChanges = null;
        }

        private sealed class ImmediateScheduler : IUnityScheduler
        {
            public void ScheduleOnMainThread(Action action) => action?.Invoke();
            public void ScheduleOnBackground(Action action) => action?.Invoke();
            public bool IsMainThread() => true;
        }

        private EventHub _eventHub;
        private SessionStateMachine _stateMachine;
        private List<SessionStateChanged> _stateChanges;

        [Test]
        public void SessionStateMachine_InitialState_IsDisconnected() =>
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);

        [Test]
        public void SessionStateMachine_TransitionToConnecting_PublishesEvent()
        {
            bool result = _stateMachine.TryTransition(SessionState.Connecting);

            Assert.IsTrue(result, "Transition should succeed");
            Assert.AreEqual(SessionState.Connecting, _stateMachine.CurrentState);
            Assert.AreEqual(1, _stateChanges.Count);
            Assert.AreEqual(SessionState.Connecting, _stateChanges[0].NewState);
        }

        [Test]
        public void SessionStateMachine_FullConnectionCycle_PublishesAllEvents()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected);
            _stateMachine.TryTransition(SessionState.Disconnecting);
            _stateMachine.TryTransition(SessionState.Disconnected);

            Assert.AreEqual(4, _stateChanges.Count);
            Assert.AreEqual(SessionState.Connecting, _stateChanges[0].NewState);
            Assert.AreEqual(SessionState.Connected, _stateChanges[1].NewState);
            Assert.AreEqual(SessionState.Disconnecting, _stateChanges[2].NewState);
            Assert.AreEqual(SessionState.Disconnected, _stateChanges[3].NewState);
        }

        [Test]
        public void SessionStateMachine_InvalidTransition_ReturnsFalse()
        {
            bool result = _stateMachine.TryTransition(SessionState.Connected);

            Assert.IsFalse(result, "Invalid transition should fail");
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
            Assert.AreEqual(0, _stateChanges.Count, "No event should be published for invalid transition");
        }

        [Test]
        public void SessionStateMachine_ErrorState_CanTransitionToDisconnected()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Error);

            bool result = _stateMachine.TryTransition(SessionState.Disconnected);

            Assert.IsTrue(result, "Should be able to transition from Error to Disconnected");
            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void SessionStateMachine_ForceTransition_BypassesValidation()
        {
            _stateMachine.ForceTransition(SessionState.Connected);

            Assert.AreEqual(SessionState.Connected, _stateMachine.CurrentState);
            Assert.AreEqual(1, _stateChanges.Count);
        }

        [Test]
        public void SessionService_StoreAndRetrieveSession_Works()
        {
            var mockPersistence = new MockSessionPersistence();
            var sessionService = new SessionService(mockPersistence);

            sessionService.StoreSession("char-123", "session-456");
            string retrieved = sessionService.LoadStoredSession("char-123");

            Assert.AreEqual("session-456", retrieved);
        }

        [Test]
        public void SessionService_ClearSession_RemovesStoredSession()
        {
            var mockPersistence = new MockSessionPersistence();
            var sessionService = new SessionService(mockPersistence);

            sessionService.StoreSession("char-123", "session-456");
            sessionService.ClearStoredSession("char-123");
            string retrieved = sessionService.LoadStoredSession("char-123");

            Assert.IsNull(retrieved, "Session should be cleared");
        }

        [Test]
        public void SessionService_ActiveSession_TracksCurrentSession()
        {
            var mockPersistence = new MockSessionPersistence();
            var sessionService = new SessionService(mockPersistence);

            sessionService.StoreSession("char-123", "session-456");
            sessionService.SetActiveSession("char-123", "session-456");

            Assert.IsTrue(sessionService.HasStoredSession("char-123"));
            Assert.AreEqual("char-123", sessionService.ActiveCharacterId);
            Assert.AreEqual("session-456", sessionService.ActiveSessionId);
        }

        [Test]
        public void SessionService_ClearActiveSession_ClearsTracking()
        {
            var mockPersistence = new MockSessionPersistence();
            var sessionService = new SessionService(mockPersistence);

            sessionService.SetActiveSession("char-123", "session-456");
            sessionService.ClearActiveSession();

            Assert.IsFalse(sessionService.HasStoredSession("char-123"));
            Assert.IsNull(sessionService.ActiveCharacterId);
            Assert.IsNull(sessionService.ActiveSessionId);
        }

        private class MockSessionPersistence : ISessionPersistence
        {
            private readonly Dictionary<string, string> _sessions = new();

            public void SaveSession(string characterId, string sessionId) => _sessions[characterId] = sessionId;

            public string LoadSession(string characterId) =>
                _sessions.TryGetValue(characterId, out string sessionId) ? sessionId : null;

            public void ClearSession(string characterId) => _sessions.Remove(characterId);

            public void ClearAllSessions() => _sessions.Clear();

            public bool HasSession(string characterId) => _sessions.ContainsKey(characterId);
        }
    }
}
