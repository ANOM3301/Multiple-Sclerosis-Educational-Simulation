using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Convai.Domain.Logging;
using Convai.Infrastructure.Networking.Connection;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Infrastructure
{
    /// <summary>
    ///     Unit tests for LiveKitRoomBackend covering state machine integration,
    ///     state transitions, and event forwarding.
    /// </summary>
    /// <remarks>
    ///     These tests use a mock state machine to verify proper state transition logic
    ///     without requiring actual LiveKit network connections.
    /// </remarks>
    [TestFixture]
    public class LiveKitRoomBackendTests
    {
        [SetUp]
        public void SetUp()
        {
            _mockLogger = new MockLogger();
            _mockStateMachine = new MockConnectionStateMachine();
            _stateChanges = new List<ConnectionState>();

            _connectionManager = new LiveKitRoomBackend(
                _mockLogger,
                _mockStateMachine);

            _connectionManager.StateChanged += state => _stateChanges.Add(state);
        }

        [TearDown]
        public void TearDown()
        {
            _connectionManager?.Dispose();
            _connectionManager = null;
            _mockStateMachine = null;
            _mockLogger = null;
            _stateChanges = null;
        }

        private LiveKitRoomBackend _connectionManager;
        private MockConnectionStateMachine _mockStateMachine;
        private MockLogger _mockLogger;
        private List<ConnectionState> _stateChanges;

        [Test]
        public void State_InitiallyDisconnected() =>
            Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.State);

        [Test]
        public void StateMachine_IsExposed()
        {
            Assert.IsNotNull(_connectionManager.StateMachine);
            Assert.AreSame(_mockStateMachine, _connectionManager.StateMachine);
        }

        [Test]
        public void Room_IsNotNull() => Assert.IsNotNull(_connectionManager.Room);

        [Test]
        public void State_ReflectsStateMachineState()
        {
            _mockStateMachine.SetState(ConnectionState.Connecting);
            Assert.AreEqual(ConnectionState.Connecting, _connectionManager.State);

            _mockStateMachine.SetState(ConnectionState.Connected);
            Assert.AreEqual(ConnectionState.Connected, _connectionManager.State);

            _mockStateMachine.SetState(ConnectionState.Disconnecting);
            Assert.AreEqual(ConnectionState.Disconnecting, _connectionManager.State);

            _mockStateMachine.SetState(ConnectionState.Disconnected);
            Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.State);
        }

        [Test]
        public void StateChanged_ForwardsFromStateMachine()
        {
            _mockStateMachine.RaiseStateChanged(ConnectionState.Disconnected, ConnectionState.Connecting, null);

            Assert.AreEqual(1, _stateChanges.Count);
            Assert.AreEqual(ConnectionState.Connecting, _stateChanges[0]);
        }

        [Test]
        public void StateChanged_ForwardsMultipleChanges()
        {
            _mockStateMachine.RaiseStateChanged(ConnectionState.Disconnected, ConnectionState.Connecting, null);
            _mockStateMachine.RaiseStateChanged(ConnectionState.Connecting, ConnectionState.Connected, null);
            _mockStateMachine.RaiseStateChanged(ConnectionState.Connected, ConnectionState.Disconnecting, null);
            _mockStateMachine.RaiseStateChanged(ConnectionState.Disconnecting, ConnectionState.Disconnected, null);

            Assert.AreEqual(4, _stateChanges.Count);
            Assert.AreEqual(ConnectionState.Connecting, _stateChanges[0]);
            Assert.AreEqual(ConnectionState.Connected, _stateChanges[1]);
            Assert.AreEqual(ConnectionState.Disconnecting, _stateChanges[2]);
            Assert.AreEqual(ConnectionState.Disconnected, _stateChanges[3]);
        }

        [Test]
        public void DisconnectAsync_WhenAlreadyDisconnected_ReturnsEarly()
        {
            Assert.AreEqual(ConnectionState.Disconnected, _connectionManager.State);

            Task.Run(async () =>
            {
                await _connectionManager.DisconnectAsync();
            }).GetAwaiter().GetResult();

            Assert.AreEqual(0, _mockStateMachine.TransitionAttempts.Count);
        }

        [Test]
        public void DisconnectAsync_FromConnected_TransitionsToDisconnecting()
        {
            _mockStateMachine.SetState(ConnectionState.Connected);

            _mockStateMachine.TransitionAttempts.Clear();

            Task.Run(async () =>
            {
                await _connectionManager.DisconnectAsync();
            }).GetAwaiter().GetResult();

            Assert.IsTrue(_mockStateMachine.TransitionAttempts.Count > 0);
            Assert.AreEqual(ConnectionState.Disconnecting, _mockStateMachine.TransitionAttempts[0]);
        }

        [Test]
        public void DisconnectAsync_FromConnected_EndsInDisconnected()
        {
            _mockStateMachine.SetState(ConnectionState.Connected);
            _mockStateMachine.AllowAllTransitions = true;

            Task.Run(async () =>
            {
                await _connectionManager.DisconnectAsync();
            }).GetAwaiter().GetResult();

            Assert.Contains(ConnectionState.Disconnecting, _mockStateMachine.TransitionAttempts);
            Assert.Contains(ConnectionState.Disconnected, _mockStateMachine.TransitionAttempts);
        }

        [Test]
        public void Dispose_UnsubscribesFromStateMachine()
        {
            _connectionManager.Dispose();

            _mockStateMachine.RaiseStateChanged(ConnectionState.Disconnected, ConnectionState.Connecting, null);

            Assert.AreEqual(0, _stateChanges.Count);
        }

        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            Assert.DoesNotThrow(() =>
            {
                _connectionManager.Dispose();
                _connectionManager.Dispose();
                _connectionManager.Dispose();
            });
        }

        [Test]
        public void Constructor_ThrowsOnNullLogger()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new LiveKitRoomBackend(null, _mockStateMachine);
            });
        }

        [Test]
        public void Constructor_ThrowsOnNullStateMachine()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new LiveKitRoomBackend(_mockLogger, (IConnectionStateMachine)null);
            });
        }

        [Test]
        public void Constructor_WithDefaultStateMachine_CreatesManager()
        {
            using var manager = new LiveKitRoomBackend(_mockLogger);
            Assert.IsNotNull(manager);
            Assert.IsNotNull(manager.StateMachine);
            Assert.AreEqual(ConnectionState.Disconnected, manager.State);
        }

        /// <summary>
        ///     Mock implementation of IConnectionStateMachine for testing.
        /// </summary>
        private class MockConnectionStateMachine : IConnectionStateMachine
        {
            public List<ConnectionState> TransitionAttempts { get; } = new();
            public List<ConnectionState> ForceTransitionAttempts { get; } = new();
            public bool AllowAllTransitions { get; set; }

            public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;

            public event Action<ConnectionState, ConnectionState, string> StateChanged;

            public bool TryTransition(ConnectionState newState, string errorMessage = null)
            {
                TransitionAttempts.Add(newState);

                if (AllowAllTransitions || IsValidTransition(CurrentState, newState))
                {
                    ConnectionState oldState = CurrentState;
                    CurrentState = newState;
                    StateChanged?.Invoke(oldState, newState, errorMessage);
                    return true;
                }

                return false;
            }

            public void ForceTransition(ConnectionState newState, string errorMessage = null)
            {
                ForceTransitionAttempts.Add(newState);
                ConnectionState oldState = CurrentState;
                CurrentState = newState;
                if (oldState != newState) StateChanged?.Invoke(oldState, newState, errorMessage);
            }

            public bool CanTransitionTo(ConnectionState targetState) =>
                AllowAllTransitions || IsValidTransition(CurrentState, targetState);

            public void Reset()
            {
                ConnectionState oldState = CurrentState;
                CurrentState = ConnectionState.Disconnected;
                if (oldState != ConnectionState.Disconnected)
                    StateChanged?.Invoke(oldState, ConnectionState.Disconnected, null);
            }

            public void SetState(ConnectionState state) => CurrentState = state;

            public void RaiseStateChanged(ConnectionState oldState, ConnectionState newState, string errorMessage) =>
                StateChanged?.Invoke(oldState, newState, errorMessage);

            private static bool IsValidTransition(ConnectionState from, ConnectionState to)
            {
                return (from, to) switch
                {
                    (ConnectionState.Disconnected, ConnectionState.Connecting) => true,
                    (ConnectionState.Connecting, ConnectionState.Connected) => true,
                    (ConnectionState.Connecting, ConnectionState.Disconnected) => true,
                    (ConnectionState.Connected, ConnectionState.Reconnecting) => true,
                    (ConnectionState.Connected, ConnectionState.Disconnecting) => true,
                    (ConnectionState.Reconnecting, ConnectionState.Connected) => true,
                    (ConnectionState.Reconnecting, ConnectionState.Disconnected) => true,
                    (ConnectionState.Disconnecting, ConnectionState.Disconnected) => true,
                    _ => false
                };
            }
        }

        /// <summary>
        ///     Simple mock logger for testing.
        /// </summary>
        private class MockLogger : ILogger
        {
            public List<string> DebugMessages { get; } = new();
            public List<string> InfoMessages { get; } = new();
            public List<string> WarningMessages { get; } = new();
            public List<string> ErrorMessages { get; } = new();

            public void Log(LogLevel level, string message, LogCategory category = LogCategory.SDK)
            {
                switch (level)
                {
                    case LogLevel.Debug: DebugMessages.Add(message); break;
                    case LogLevel.Info: InfoMessages.Add(message); break;
                    case LogLevel.Warning: WarningMessages.Add(message); break;
                    case LogLevel.Error: ErrorMessages.Add(message); break;
                }
            }

            public void Log(LogLevel level, string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK) => Log(level, message, category);

            public void Debug(string message, LogCategory category = LogCategory.SDK) => DebugMessages.Add(message);

            public void Debug(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK) => Debug(message, category);

            public void Info(string message, LogCategory category = LogCategory.SDK) => InfoMessages.Add(message);

            public void Info(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK) => Info(message, category);

            public void Warning(string message, LogCategory category = LogCategory.SDK) => WarningMessages.Add(message);

            public void Warning(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK) => Warning(message, category);

            public void Error(string message, LogCategory category = LogCategory.SDK) => ErrorMessages.Add(message);

            public void Error(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK) => Error(message, category);

            public void Error(Exception exception, string message = null, LogCategory category = LogCategory.SDK) =>
                ErrorMessages.Add(message ?? exception.Message);

            public void Error(Exception exception, string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK) => Error(exception, message, category);

            public bool IsEnabled(LogLevel level, LogCategory category) => true;
        }
    }
}
