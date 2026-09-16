using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Convai.Domain.Models;
using Convai.Infrastructure.Networking;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Infrastructure
{
    public class PlayerConversationInputTests
    {
        [Test]
        public void Interim_Stop_Without_Final_Does_Not_Emit_Completed()
        {
            var playerEvents = new CapturingPlayerEvents();
            var coordinator = new PlayerConversationInput(playerEvents, new ImmediateDispatcher());

            coordinator.HandleStart();
            coordinator.HandleInterim("Hello");
            coordinator.HandleStop();

            CollectionAssert.DoesNotContain(playerEvents.Phases, TranscriptionPhase.Completed);
            Assert.AreEqual(1, playerEvents.StoppedSessions.Count);
            Assert.IsFalse(playerEvents.StoppedSessions[0].DidProduceFinalTranscript);
        }

        [Test]
        public void AsrFinal_Stop_Completes_After_Grace_Window()
        {
            var playerEvents = new CapturingPlayerEvents();
            var coordinator = new PlayerConversationInput(playerEvents, new ImmediateDispatcher());

            coordinator.HandleStart();
            coordinator.HandleInterim("Tell me");
            coordinator.HandleAsrFinal("Tell me if");
            coordinator.HandleStop();

            Assert.IsFalse(playerEvents.Phases.Contains(TranscriptionPhase.Completed));

            Thread.Sleep(300);

            CollectionAssert.AreEqual(
                new[]
                {
                    TranscriptionPhase.Listening,
                    TranscriptionPhase.Interim,
                    TranscriptionPhase.AsrFinal,
                    TranscriptionPhase.Completed
                },
                playerEvents.Phases);
            Assert.AreEqual(1, playerEvents.StoppedSessions.Count);
            Assert.IsTrue(playerEvents.StoppedSessions[0].DidProduceFinalTranscript);
        }

        [Test]
        public void ProcessedFinal_Within_Grace_Window_Wins_And_Completes_Once()
        {
            var playerEvents = new CapturingPlayerEvents();
            var coordinator = new PlayerConversationInput(playerEvents, new ImmediateDispatcher());
            var speakerInfo = new SpeakerInfo("speaker-1", "Rishav", "PA_1");

            coordinator.HandleStart();
            coordinator.HandleAsrFinal("tell me if");
            coordinator.HandleStop();

            Thread.Sleep(75);
            coordinator.HandleProcessedFinal("tell me if you can hear me", speakerInfo);
            Thread.Sleep(50);

            Assert.AreEqual(1, playerEvents.Phases.Count(phase => phase == TranscriptionPhase.Completed));
            Assert.AreEqual(1, playerEvents.StoppedSessions.Count);
            Assert.AreEqual("tell me if you can hear me",
                playerEvents.Transcripts.Last(entry => entry.Phase == TranscriptionPhase.ProcessedFinal).Text);
        }

        [Test]
        public void LateProcessedFinal_PublishesPhaseWithoutStartingDuplicateSession()
        {
            var playerEvents = new CapturingPlayerEvents();
            var coordinator = new PlayerConversationInput(playerEvents, new ImmediateDispatcher());
            var speakerInfo = new SpeakerInfo("speaker-1", "Player", "PA_1");

            coordinator.HandleStart();
            coordinator.HandleProcessedFinal("Hello", speakerInfo);
            coordinator.HandleStop();
            int eventCountAfterCompletion = playerEvents.Transcripts.Count;

            coordinator.HandleProcessedFinal("Hello", speakerInfo);

            Assert.AreEqual(eventCountAfterCompletion + 1, playerEvents.Transcripts.Count);
            Assert.AreEqual(2,
                playerEvents.Phases.Count(phase => phase == TranscriptionPhase.ProcessedFinal));
            Assert.AreEqual(1,
                playerEvents.Phases.Count(phase => phase == TranscriptionPhase.Completed));
            Assert.AreEqual(1, playerEvents.StoppedSessions.Count);
        }

        private sealed class CapturingPlayerEvents : IConvaiPlayerEvents
        {
            public readonly List<TranscriptEntry> Transcripts = new();
            public readonly List<StoppedSession> StoppedSessions = new();

            public IEnumerable<TranscriptionPhase> Phases => Transcripts.Select(entry => entry.Phase);

            public void OnPlayerTranscriptionReceived(string transcript, TranscriptionPhase transcriptionPhase) =>
                OnPlayerTranscriptionReceived(transcript, transcriptionPhase, SpeakerInfo.Empty);

            public void OnPlayerTranscriptionReceived(string transcript, TranscriptionPhase transcriptionPhase,
                SpeakerInfo speakerInfo)
            {
                Transcripts.Add(new TranscriptEntry(transcript, transcriptionPhase, speakerInfo));
            }

            public void OnPlayerStartedSpeaking(string sessionId)
            {
            }

            public void OnPlayerStoppedSpeaking(string sessionId, bool didProduceFinalTranscript)
            {
                StoppedSessions.Add(new StoppedSession(sessionId, didProduceFinalTranscript));
            }
        }

        private sealed class ImmediateDispatcher : IMainThreadDispatcher
        {
            public bool TryDispatch(System.Action action)
            {
                action?.Invoke();
                return true;
            }
        }

        private readonly struct TranscriptEntry
        {
            public TranscriptEntry(string text, TranscriptionPhase phase, SpeakerInfo speakerInfo)
            {
                Text = text;
                Phase = phase;
                SpeakerInfo = speakerInfo;
            }

            public string Text { get; }
            public TranscriptionPhase Phase { get; }
            public SpeakerInfo SpeakerInfo { get; }
        }

        private readonly struct StoppedSession
        {
            public StoppedSession(string sessionId, bool didProduceFinalTranscript)
            {
                SessionId = sessionId;
                DidProduceFinalTranscript = didProduceFinalTranscript;
            }

            public string SessionId { get; }
            public bool DidProduceFinalTranscript { get; }
        }
    }
}
