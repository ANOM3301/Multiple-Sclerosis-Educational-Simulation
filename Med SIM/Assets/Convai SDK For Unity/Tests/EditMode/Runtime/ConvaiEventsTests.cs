using System;
using Convai.Domain.DomainEvents.LipSync;
using Convai.Domain.DomainEvents.Runtime;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.DomainEvents.Transcript;
using Convai.Domain.EventSystem;
using Convai.Domain.Models;
using Convai.Runtime.Facades;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode
{
    public class ConvaiEventsTests
    {
        [Test]
        public void Player_Transcript_Handler_Exception_Does_Not_Block_Other_Handlers()
        {
            EventHub eventHub = CreateEventHub();
            using var eventsFacade = new ConvaiEvents(eventHub);
            bool healthyHandlerCalled = false;

            LogAssert.ignoreFailingMessages = true;
            try
            {
                eventsFacade.OnPlayerTranscriptReceived += _ => throw new NullReferenceException("boom");
                eventsFacade.OnPlayerTranscriptReceived += _ => healthyHandlerCalled = true;

                eventHub.Publish(PlayerTranscriptReceived.Create(
                    "player-1",
                    "You",
                    "Hello",
                    false,
                    turnId: "turn-1",
                    messageId: "turn-1"));

                Assert.IsTrue(healthyHandlerCalled);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void Character_TurnCompleted_Handler_Exception_Does_Not_Block_Other_Handlers()
        {
            EventHub eventHub = CreateEventHub();
            using var eventsFacade = new ConvaiEvents(eventHub);
            bool healthyHandlerCalled = false;

            LogAssert.ignoreFailingMessages = true;
            try
            {
                eventsFacade.OnCharacterTurnCompleted += _ => throw new NullReferenceException("boom");
                eventsFacade.OnCharacterTurnCompleted += _ => healthyHandlerCalled = true;

                eventHub.Publish(CharacterTurnCompleted.Create("char-1", "participant-1", false));

                Assert.IsTrue(healthyHandlerCalled);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void LlmNoResponse_Provides_Character_Context()
        {
            EventHub eventHub = CreateEventHub();
            using var eventsFacade = new ConvaiEvents(eventHub);
            (string CharacterId, string ParticipantId, string Reason) captured = default;

            eventsFacade.OnLlmNoResponseReceived += e =>
                captured = (e.CharacterId, e.ParticipantId, e.Reason);

            eventHub.Publish(LlmNoResponseReceived.Create("char-1", "participant-1", "abstain"));

            Assert.AreEqual("char-1", captured.CharacterId);
            Assert.AreEqual("participant-1", captured.ParticipantId);
            Assert.AreEqual("abstain", captured.Reason);
        }

        [Test]
        public void InteractionCreated_Event_Is_Exposed()
        {
            EventHub eventHub = CreateEventHub();
            using var eventsFacade = new ConvaiEvents(eventHub);
            (string InteractionId, string CharacterSessionId) captured = default;

            eventsFacade.OnInteractionCreated += e =>
                captured = (e.InteractionId, e.CharacterSessionId);

            eventHub.Publish(InteractionCreated.Create(
                "char-1",
                "participant-1",
                "a4fce023-d850-4210-9d10-8c98228d1b4b",
                "0ded6a03-aeec-4c8b-a64b-0ee910695203"));

            Assert.AreEqual("a4fce023-d850-4210-9d10-8c98228d1b4b", captured.InteractionId);
            Assert.AreEqual("0ded6a03-aeec-4c8b-a64b-0ee910695203", captured.CharacterSessionId);
        }

        [Test]
        public void FinalUserTranscription_Event_Is_Exposed()
        {
            EventHub eventHub = CreateEventHub();
            using var eventsFacade = new ConvaiEvents(eventHub);
            FinalUserTranscriptionReceived captured = default;

            eventsFacade.OnFinalUserTranscriptionReceived += e => captured = e;

            eventHub.Publish(FinalUserTranscriptionReceived.Create(
                "Hello there",
                new SpeakerInfo("speaker-1", "Rishav", "PA_1")));

            Assert.AreEqual("Hello there", captured.Text);
            Assert.AreEqual("speaker-1", captured.SpeakerId);
            Assert.AreEqual("Rishav", captured.SpeakerName);
            Assert.AreEqual("PA_1", captured.ParticipantId);
        }

        [Test]
        public void UserIdleWarning_Handler_Exception_Does_Not_Block_Other_Handlers()
        {
            EventHub eventHub = CreateEventHub();
            using var eventsFacade = new ConvaiEvents(eventHub);
            bool healthyHandlerCalled = false;

            LogAssert.ignoreFailingMessages = true;
            try
            {
                eventsFacade.OnUserIdleWarningReceived += _ => throw new NullReferenceException("boom");
                eventsFacade.OnUserIdleWarningReceived += _ => healthyHandlerCalled = true;

                eventHub.Publish(UserIdleWarningReceived.Create(120, "Idle"));

                Assert.IsTrue(healthyHandlerCalled);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void BlendshapeTurnStats_Event_Is_Exposed()
        {
            EventHub eventHub = CreateEventHub();
            using var eventsFacade = new ConvaiEvents(eventHub);
            BlendshapeTurnStatsReceived captured = default;

            eventsFacade.OnBlendshapeTurnStatsReceived += e => captured = e;

            eventHub.Publish(BlendshapeTurnStatsReceived.Create(
                "char-1",
                "participant-1",
                150,
                150,
                48000,
                3000d,
                2800d,
                50d));

            Assert.AreEqual("char-1", captured.CharacterId);
            Assert.AreEqual("participant-1", captured.ParticipantId);
            Assert.AreEqual(2800d, captured.TotalAudioDurationMs);
            Assert.IsTrue(captured.FrameCountMatches);
        }

        private static EventHub CreateEventHub() => new(new ImmediateScheduler());

        private sealed class ImmediateScheduler : IUnityScheduler
        {
            public void ScheduleOnMainThread(Action action) => action?.Invoke();

            public void ScheduleOnBackground(Action action) => action?.Invoke();

            public bool IsMainThread() => true;
        }
    }
}
