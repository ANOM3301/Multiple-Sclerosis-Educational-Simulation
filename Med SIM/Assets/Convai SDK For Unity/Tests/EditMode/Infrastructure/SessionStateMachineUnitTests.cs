using System;
using System.Collections.Generic;
using System.Threading;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.EventSystem;
using Convai.Infrastructure.Networking.Services;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Infrastructure
{
    /// <summary>
    ///     Focused unit tests for SessionStateMachine thread-safety and unit-only edge cases.
    /// </summary>
    [TestFixture]
    public class SessionStateMachineUnitTests
    {
        [SetUp]
        public void SetUp()
        {
            _eventHub = new EventHub(new ImmediateScheduler());
            _stateMachine = new SessionStateMachine(_eventHub);
            _stateChanges = new List<SessionStateChanged>();
            _eventHub.Subscribe<SessionStateChanged>(evt => _stateChanges.Add(evt));
        }

        [TearDown]
        public void TearDown()
        {
            _stateMachine = null;
            _stateChanges = null;
            _eventHub = null;
        }

        private EventHub _eventHub;
        private SessionStateMachine _stateMachine;
        private List<SessionStateChanged> _stateChanges;

        private sealed class ImmediateScheduler : IUnityScheduler
        {
            public void ScheduleOnMainThread(Action action) => action?.Invoke();
            public void ScheduleOnBackground(Action action) => action?.Invoke();
            public bool IsMainThread() => true;
        }

        [Test]
        public void Reset_IsIdempotent()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.Reset();
            _stateChanges.Clear();

            _stateMachine.Reset();
            _stateMachine.Reset();
            _stateMachine.Reset();

            Assert.AreEqual(SessionState.Disconnected, _stateMachine.CurrentState);
            Assert.AreEqual(0, _stateChanges.Count);
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
                    if (_stateMachine.TryTransition(SessionState.Connecting)) Interlocked.Increment(ref successCount);
                });
            }

            foreach (Thread thread in threads)
                thread.Start();

            foreach (Thread thread in threads)
                thread.Join();

            Assert.AreEqual(1, successCount, "Only one thread should succeed in transitioning");
            Assert.AreEqual(SessionState.Connecting, _stateMachine.CurrentState);
        }

        [Test]
        public void ConcurrentReads_AreThreadSafe()
        {
            _stateMachine.TryTransition(SessionState.Connecting);
            _stateMachine.TryTransition(SessionState.Connected, "concurrent-session");

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
                            _ = _stateMachine.SessionId;
                            _ = _stateMachine.CanTransitionTo(SessionState.Disconnecting);
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
        public void ConcurrentResetAndTransition_IsThreadSafe()
        {
            const int iterations = 50;
            var errors = new List<Exception>();

            for (int i = 0; i < iterations; i++)
            {
                var eventHub = new EventHub(new ImmediateScheduler());
                _stateMachine = new SessionStateMachine(eventHub);
                var barrier = new Barrier(2);

                var transitionThread = new Thread(() =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        _stateMachine.TryTransition(SessionState.Connecting);
                        _stateMachine.TryTransition(SessionState.Connected);
                    }
                    catch (Exception ex)
                    {
                        lock (errors) errors.Add(ex);
                    }
                });

                var resetThread = new Thread(() =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        _stateMachine.Reset();
                    }
                    catch (Exception ex)
                    {
                        lock (errors) errors.Add(ex);
                    }
                });

                transitionThread.Start();
                resetThread.Start();
                transitionThread.Join();
                resetThread.Join();
            }

            Assert.AreEqual(0, errors.Count, "No exceptions should occur during concurrent reset and transition");
        }
    }
}
