using System;
using System.Collections.Generic;
using System.Threading;
using Convai.Infrastructure.Networking.Connection;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Infrastructure
{
    /// <summary>
    ///     Unit tests for ConnectionStateMachine covering state transitions, concurrency, and edge cases.
    /// </summary>
    [TestFixture]
    public class ConnectionStateMachineTests
    {
        [SetUp]
        public void SetUp()
        {
            _stateMachine = new ConnectionStateMachine();
            _stateChanges = new List<(ConnectionState, ConnectionState, string)>();
            _stateMachine.StateChanged += (oldState, newState, errorMessage) =>
                _stateChanges.Add((oldState, newState, errorMessage));
        }

        [TearDown]
        public void TearDown()
        {
            _stateMachine = null;
            _stateChanges = null;
        }

        private ConnectionStateMachine _stateMachine;
        private List<(ConnectionState oldState, ConnectionState newState, string errorMessage)> _stateChanges;

        [Test]
        public void InitialState_IsDisconnected() =>
            Assert.AreEqual(ConnectionState.Disconnected, _stateMachine.CurrentState);

        [Test]
        public void TryTransition_DisconnectedToConnecting_ReturnsTrue()
        {
            bool result = _stateMachine.TryTransition(ConnectionState.Connecting);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Connecting, _stateMachine.CurrentState);
            Assert.AreEqual(1, _stateChanges.Count);
            Assert.AreEqual(ConnectionState.Disconnected, _stateChanges[0].oldState);
            Assert.AreEqual(ConnectionState.Connecting, _stateChanges[0].newState);
        }

        [Test]
        public void TryTransition_ConnectingToConnected_ReturnsTrue()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);

            bool result = _stateMachine.TryTransition(ConnectionState.Connected);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Connected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_ConnectingToDisconnected_ReturnsTrue()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);

            bool result = _stateMachine.TryTransition(ConnectionState.Disconnected, "Connection failed");

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Disconnected, _stateMachine.CurrentState);
            Assert.AreEqual("Connection failed", _stateChanges[1].errorMessage);
        }

        [Test]
        public void TryTransition_ConnectedToReconnecting_ReturnsTrue()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);

            bool result = _stateMachine.TryTransition(ConnectionState.Reconnecting);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Reconnecting, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_ConnectedToDisconnecting_ReturnsTrue()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);

            bool result = _stateMachine.TryTransition(ConnectionState.Disconnecting);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Disconnecting, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_ReconnectingToConnected_ReturnsTrue()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);
            _stateMachine.TryTransition(ConnectionState.Reconnecting);

            bool result = _stateMachine.TryTransition(ConnectionState.Connected);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Connected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_ReconnectingToDisconnected_ReturnsTrue()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);
            _stateMachine.TryTransition(ConnectionState.Reconnecting);

            bool result = _stateMachine.TryTransition(ConnectionState.Disconnected);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_DisconnectingToDisconnected_ReturnsTrue()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);
            _stateMachine.TryTransition(ConnectionState.Disconnecting);

            bool result = _stateMachine.TryTransition(ConnectionState.Disconnected);

            Assert.IsTrue(result);
            Assert.AreEqual(ConnectionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_DisconnectedToConnected_ReturnsFalse()
        {
            bool result = _stateMachine.TryTransition(ConnectionState.Connected);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Disconnected, _stateMachine.CurrentState);
            Assert.AreEqual(0, _stateChanges.Count);
        }

        [Test]
        public void TryTransition_DisconnectedToReconnecting_ReturnsFalse()
        {
            bool result = _stateMachine.TryTransition(ConnectionState.Reconnecting);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_DisconnectedToDisconnecting_ReturnsFalse()
        {
            bool result = _stateMachine.TryTransition(ConnectionState.Disconnecting);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Disconnected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_ConnectingToReconnecting_ReturnsFalse()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);

            bool result = _stateMachine.TryTransition(ConnectionState.Reconnecting);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Connecting, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_ConnectingToDisconnecting_ReturnsFalse()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);

            bool result = _stateMachine.TryTransition(ConnectionState.Disconnecting);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Connecting, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_ConnectedToConnecting_ReturnsFalse()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);

            bool result = _stateMachine.TryTransition(ConnectionState.Connecting);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Connected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_ConnectedToDisconnected_ReturnsFalse()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);

            bool result = _stateMachine.TryTransition(ConnectionState.Disconnected);

            Assert.IsFalse(result);
            Assert.AreEqual(ConnectionState.Connected, _stateMachine.CurrentState);
        }

        [Test]
        public void TryTransition_SameState_ReturnsFalse()
        {
            bool result = _stateMachine.TryTransition(ConnectionState.Disconnected);

            Assert.IsFalse(result);
            Assert.AreEqual(0, _stateChanges.Count);
        }

        [Test]
        public void TryTransition_ConnectingToConnecting_ReturnsFalse()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateChanges.Clear();

            bool result = _stateMachine.TryTransition(ConnectionState.Connecting);

            Assert.IsFalse(result);
            Assert.AreEqual(0, _stateChanges.Count);
        }

        [Test]
        public void CanTransitionTo_ValidTransition_ReturnsTrue() =>
            Assert.IsTrue(_stateMachine.CanTransitionTo(ConnectionState.Connecting));

        [Test]
        public void CanTransitionTo_InvalidTransition_ReturnsFalse()
        {
            Assert.IsFalse(_stateMachine.CanTransitionTo(ConnectionState.Connected));
            Assert.IsFalse(_stateMachine.CanTransitionTo(ConnectionState.Reconnecting));
        }

        [Test]
        public void CanTransitionTo_ReflectsCurrentState()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);

            Assert.IsTrue(_stateMachine.CanTransitionTo(ConnectionState.Reconnecting));
            Assert.IsTrue(_stateMachine.CanTransitionTo(ConnectionState.Disconnecting));
            Assert.IsFalse(_stateMachine.CanTransitionTo(ConnectionState.Connecting));
        }

        [Test]
        public void ForceTransition_BypassesValidation()
        {
            _stateMachine.ForceTransition(ConnectionState.Connected);

            Assert.AreEqual(ConnectionState.Connected, _stateMachine.CurrentState);
            Assert.AreEqual(1, _stateChanges.Count);
        }

        [Test]
        public void ForceTransition_SameState_NoEvent()
        {
            _stateMachine.ForceTransition(ConnectionState.Disconnected);

            Assert.AreEqual(0, _stateChanges.Count);
        }

        [Test]
        public void ForceTransition_IncludesErrorMessage()
        {
            _stateMachine.ForceTransition(ConnectionState.Disconnected, "Forced disconnect");

            Assert.AreEqual(0, _stateChanges.Count);

            _stateMachine.ForceTransition(ConnectionState.Connected, "Forced connect");
            Assert.AreEqual("Forced connect", _stateChanges[0].errorMessage);
        }

        [Test]
        public void Reset_FromDisconnected_NoEvent()
        {
            _stateMachine.Reset();

            Assert.AreEqual(ConnectionState.Disconnected, _stateMachine.CurrentState);
            Assert.AreEqual(0, _stateChanges.Count);
        }

        [Test]
        public void Reset_FromConnected_TransitionsToDisconnected()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);
            _stateChanges.Clear();

            _stateMachine.Reset();

            Assert.AreEqual(ConnectionState.Disconnected, _stateMachine.CurrentState);
            Assert.AreEqual(1, _stateChanges.Count);
            Assert.AreEqual(ConnectionState.Disconnected, _stateChanges[0].newState);
        }

        [Test]
        public void Reset_FromReconnecting_TransitionsToDisconnected()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);
            _stateMachine.TryTransition(ConnectionState.Reconnecting);
            _stateChanges.Clear();

            _stateMachine.Reset();

            Assert.AreEqual(ConnectionState.Disconnected, _stateMachine.CurrentState);
            Assert.AreEqual(1, _stateChanges.Count);
        }

        [Test]
        public void ConcurrentTransitions_OnlyOneSucceeds()
        {
            const int threadCount = 10;
            int successCount = 0;
            var barrier = new Barrier(threadCount);
            var threads = new Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                threads[i] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    if (_stateMachine.TryTransition(ConnectionState.Connecting))
                        Interlocked.Increment(ref successCount);
                });
            }

            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            Assert.AreEqual(1, successCount, "Only one thread should succeed in transitioning");
            Assert.AreEqual(ConnectionState.Connecting, _stateMachine.CurrentState);
        }

        [Test]
        public void ConcurrentReads_AreThreadSafe()
        {
            _stateMachine.TryTransition(ConnectionState.Connecting);
            _stateMachine.TryTransition(ConnectionState.Connected);

            const int threadCount = 20;
            var barrier = new Barrier(threadCount);
            var errors = new List<Exception>();
            var threads = new Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                threads[i] = new Thread(() =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        for (int j = 0; j < 100; j++)
                        {
                            _ = _stateMachine.CurrentState;
                            _ = _stateMachine.CanTransitionTo(ConnectionState.Disconnecting);
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (errors) errors.Add(ex);
                    }
                });
            }

            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            Assert.AreEqual(0, errors.Count, "No exceptions should occur during concurrent reads");
        }

        [Test]
        public void FullPath_ConnectDisconnect()
        {
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Connecting));
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Connected));
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Disconnecting));
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Disconnected));

            Assert.AreEqual(4, _stateChanges.Count);
        }

        [Test]
        public void FullPath_ConnectReconnectDisconnect()
        {
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Connecting));
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Connected));
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Reconnecting));
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Connected));
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Disconnecting));
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Disconnected));

            Assert.AreEqual(6, _stateChanges.Count);
        }

        [Test]
        public void FullPath_ConnectFailure()
        {
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Connecting));
            Assert.IsTrue(_stateMachine.TryTransition(ConnectionState.Disconnected, "Connection timeout"));

            Assert.AreEqual(2, _stateChanges.Count);
            Assert.AreEqual("Connection timeout", _stateChanges[1].errorMessage);
        }
    }
}
