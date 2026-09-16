using System.Collections.Generic;
using Convai.Domain.DomainEvents.Transcript;
using Convai.Domain.Models;
using Convai.Runtime.Adapters.Networking;
using Convai.Runtime.Components;
using Convai.Runtime.Room;
using Convai.Tests.EditMode.Fixtures;
using Convai.Tests.EditMode.Mocks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.Runtime
{
    /// <summary>
    ///     Push-to-talk keeps capture open briefly after physical release so the STT provider can finalize.
    ///     If the first window expires, the stop is sent while capture remains open for one final bounded window.
    /// </summary>
    public class ConvaiPushToTalkControllerTests
    {
        private readonly List<Object> _createdObjects = new();
        private MockRoomAudioService _audio;
        private MockRoomConnectionService _connection;
        private ConvaiPushToTalkController _controller;
        private FakeEventHub _eventHub;
        private TurnTakingOptions _options;

        [SetUp]
        public void SetUp()
        {
            _options = TurnTakingOptions.CreatePushToTalkDefault();
            _connection = new MockRoomConnectionService();
            _audio = new MockRoomAudioService();
            _eventHub = new FakeEventHub();

            var managerObject = new GameObject("ConvaiManager");
            _createdObjects.Add(managerObject);
            ConvaiManager manager = managerObject.AddComponent<ConvaiManager>();

            var controllerObject = new GameObject("ConvaiPushToTalkController");
            _createdObjects.Add(controllerObject);
            _controller = controllerObject.AddComponent<ConvaiPushToTalkController>();
            _controller.InjectForTests(
                manager,
                _eventHub,
                _connection,
                _audio,
                () => TurnTakingOptionsResolver.ResolveFromSource(_options));
            _controller.SetTargetCharacterIdForTests("character-1");

            _connection.ConnectAsync();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdObjects.Count; i++)
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            _createdObjects.Clear();
        }

        [Test]
        public void Release_KeepsCaptureOpenUntilAsrFinalThenCommitsTheTurn()
        {
            Assert.IsTrue(_controller.Press());
            Assert.IsTrue(_controller.Release());

            Assert.That(_audio.IsMicMuted, Is.False);
            Assert.That(_connection.SttMutedStates, Is.EqualTo(new[] { false }));
            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.Zero);

            PublishPlayerTranscript(TranscriptionPhase.AsrFinal);

            Assert.That(_audio.IsMicMuted, Is.True);
            Assert.That(_connection.SttMutedStates, Is.EqualTo(new[] { false, true }));
            Assert.That(
                _connection.TurnControlCalls,
                Is.EqualTo(new[] { "stt:False", "force-user-stopped-speaking", "stt:True" }));
            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.EqualTo(1));
        }

        [Test]
        public void AsrFinalBeforeRelease_DoesNotSkipTailForALaterSegment()
        {
            Assert.IsTrue(_controller.Press());
            PublishPlayerTranscript(TranscriptionPhase.AsrFinal);

            Assert.IsTrue(_controller.Release());

            Assert.That(_audio.IsMicMuted, Is.False);
            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.Zero);

            PublishPlayerTranscript(TranscriptionPhase.AsrFinal);

            Assert.That(_audio.IsMicMuted, Is.True);
            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Release_WhenInitialTailExpires_SignalsStopButKeepsCaptureOpenForFinalization()
        {
            _options.PushToTalkPolicy.ReleaseTailMs = 500;

            Assert.IsTrue(_controller.Press());
            Assert.IsTrue(_controller.Release());

            _controller.ExpireReleaseTailForTests();

            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.EqualTo(1));
            Assert.That(_audio.IsMicMuted, Is.False);
            Assert.That(_connection.SttMutedStates, Is.EqualTo(new[] { false }));
            Assert.That(
                _connection.TurnControlCalls,
                Is.EqualTo(new[] { "stt:False", "force-user-stopped-speaking" }));

            PublishPlayerTranscript(TranscriptionPhase.AsrFinal);

            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.EqualTo(1));
            Assert.That(_audio.IsMicMuted, Is.True);
            Assert.That(_connection.SttMutedStates, Is.EqualTo(new[] { false, true }));
            Assert.That(
                _connection.TurnControlCalls,
                Is.EqualTo(new[] { "stt:False", "force-user-stopped-speaking", "stt:True" }));
        }

        [Test]
        public void Release_WhenPostStopTailExpires_ClosesCaptureWithoutSendingStopAgain()
        {
            _options.PushToTalkPolicy.ReleaseTailMs = 100;

            Assert.IsTrue(_controller.Press());
            Assert.IsTrue(_controller.Release());

            _controller.ExpireReleaseTailForTests();
            _controller.ExpireReleaseTailForTests();

            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.EqualTo(1));
            Assert.That(_audio.IsMicMuted, Is.True);
            Assert.That(_connection.SttMutedStates, Is.EqualTo(new[] { false, true }));
        }

        [Test]
        public void Release_WithTailDisabled_CommitsImmediately()
        {
            _options.PushToTalkPolicy.ReleaseTailMs = 0;

            Assert.IsTrue(_controller.Press());
            Assert.IsTrue(_controller.Release());

            Assert.That(_audio.IsMicMuted, Is.True);
            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Release_WithServerSttToggleDisabled_LeavesBackendTranscriptionAlone()
        {
            _options.PushToTalkPolicy.EnableServerSttToggle = false;
            _options.PushToTalkPolicy.ReleaseTailMs = 0;

            Assert.IsTrue(_controller.Press());
            Assert.IsTrue(_controller.Release());

            Assert.That(_audio.IsMicMuted, Is.True);
            Assert.That(_connection.SttMutedStates, Is.Empty);
            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Release_WithoutPress_DoesNotEndTheTurn()
        {
            Assert.IsFalse(_controller.Release());

            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.Zero);
        }

        [Test]
        public void ConversationInputModeTransition_WhilePressed_CommitsAndClosesCapture()
        {
            Assert.IsTrue(_controller.Press());

            _controller.PrepareForConversationInputModeTransition(
                TurnTakingOptionsResolver.ResolveFromSource(TurnTakingOptions.CreateHandsFreeDefault()),
                "test:handsfree-switch");

            Assert.That(_audio.IsMicMuted, Is.True);
            Assert.That(
                _connection.TurnControlCalls,
                Is.EqualTo(new[] { "stt:False", "force-user-stopped-speaking", "stt:True" }));
            Assert.That(_connection.ForceUserStoppedSpeakingCallCount, Is.EqualTo(1));
            Assert.That(_controller.IsPressed, Is.False);
        }

        private void PublishPlayerTranscript(TranscriptionPhase phase)
        {
            _eventHub.Publish(PlayerTranscriptReceived.Create(
                "player",
                "Player",
                "test",
                phase == TranscriptionPhase.AsrFinal,
                phase));
        }
    }
}
