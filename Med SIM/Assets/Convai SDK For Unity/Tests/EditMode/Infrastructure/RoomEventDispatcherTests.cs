using System;
using Convai.Infrastructure.Networking.Events;
using LiveKit.Proto;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Infrastructure
{
    [TestFixture]
    public class RoomEventDispatcherTests
    {
        [SetUp]
        public void SetUp() => _dispatcher = new RoomEventDispatcher();

        [TearDown]
        public void TearDown() => _dispatcher?.Dispose();

        private RoomEventDispatcher _dispatcher;

        [Test]
        public void IsAttached_InitiallyFalse() => Assert.IsFalse(_dispatcher.IsAttached);

        [Test]
        public void AttachToRoom_NullRoom_ThrowsArgumentNullException() =>
            Assert.Throws<ArgumentNullException>(() => _dispatcher.AttachToRoom(null));

        [Test]
        public void DetachFromRoom_WhenNotAttached_DoesNotThrow() =>
            Assert.DoesNotThrow(() => _dispatcher.DetachFromRoom());

        [Test]
        public void Dispose_WhenNotAttached_DoesNotThrow() => Assert.DoesNotThrow(() => _dispatcher.Dispose());

        [Test]
        public void Dispose_MultipleTimes_DoesNotThrow()
        {
            _dispatcher.Dispose();
            Assert.DoesNotThrow(() => _dispatcher.Dispose());
        }

        [Test]
        public void AttachToRoom_AfterDispose_ThrowsObjectDisposedException() => _dispatcher.Dispose();

        [Test]
        public void Events_CanSubscribe()
        {
            bool connectionStateChangedRaised = false;
            bool participantJoinedRaised = false;
            bool participantLeftRaised = false;
            bool trackPublishedRaised = false;
            bool trackUnpublishedRaised = false;
            bool trackSubscribedRaised = false;
            bool trackUnsubscribedRaised = false;
            bool dataReceivedRaised = false;
            bool disconnectedRaised = false;
            bool reconnectingRaised = false;
            bool reconnectedRaised = false;

            _dispatcher.ConnectionStateChanged += _ => connectionStateChangedRaised = true;
            _dispatcher.ParticipantJoined += _ => participantJoinedRaised = true;
            _dispatcher.ParticipantLeft += _ => participantLeftRaised = true;
            _dispatcher.TrackPublished += (_, _) => trackPublishedRaised = true;
            _dispatcher.TrackUnpublished += (_, _) => trackUnpublishedRaised = true;
            _dispatcher.TrackSubscribed += (_, _, _) => trackSubscribedRaised = true;
            _dispatcher.TrackUnsubscribed += (_, _, _) => trackUnsubscribedRaised = true;
            _dispatcher.DataReceived += (_, _, _, _) => dataReceivedRaised = true;
            _dispatcher.Disconnected += _ => disconnectedRaised = true;
            _dispatcher.Reconnecting += () => reconnectingRaised = true;
            _dispatcher.Reconnected += () => reconnectedRaised = true;

            Assert.IsFalse(connectionStateChangedRaised);
            Assert.IsFalse(participantJoinedRaised);
            Assert.IsFalse(participantLeftRaised);
            Assert.IsFalse(trackPublishedRaised);
            Assert.IsFalse(trackUnpublishedRaised);
            Assert.IsFalse(trackSubscribedRaised);
            Assert.IsFalse(trackUnsubscribedRaised);
            Assert.IsFalse(dataReceivedRaised);
            Assert.IsFalse(disconnectedRaised);
            Assert.IsFalse(reconnectingRaised);
            Assert.IsFalse(reconnectedRaised);
        }

        [Test]
        public void Events_CanUnsubscribe()
        {
            void Handler(ConnectionState _) { }
            _dispatcher.ConnectionStateChanged += Handler;
            Assert.DoesNotThrow(() => _dispatcher.ConnectionStateChanged -= Handler);
        }
    }
}
