using System.Collections.Generic;
using Convai.Domain.DomainEvents.Runtime;
using Convai.Domain.DomainEvents.Transcript;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Infrastructure
{
    public partial class RTVIHandlerTests
    {
        [Test]
        public void BotReady_PublishesResolvedCharacter()
        {
            RtviTestContext context = CreateContext();
            context.Registry.RegisterCharacter(new TestCharacterAgent("char-1", "Camila"));
            context.Registry.SetParticipantId("char-1", "participant-1");
            CharacterReady captured = default;
            context.EventHub.Subscribe<CharacterReady>(evt => captured = evt);

            context.Gateway.ProcessIncoming(CreateInboundPacket("bot-ready", "participant-1"));

            Assert.AreEqual("char-1", captured.CharacterId);
            Assert.AreEqual("participant-1", captured.ParticipantId);
        }

        [Test]
        public void BotLifecycle_LogOnlyRoutes_AreRegistered()
        {
            RtviTestContext context = CreateContext();

            context.Gateway.ProcessIncoming(CreateInboundPacket("bot-llm-stopped", "participant-1"));
            context.Gateway.ProcessIncoming(CreateInboundPacket("bot-tts-started", "participant-1"));
            context.Gateway.ProcessIncoming(CreateInboundPacket("bot-tts-stopped", "participant-1"));

            Assert.IsTrue(context.Logger.Contains("Character LLM stopped"));
            Assert.IsTrue(context.Logger.Contains("Character TTS started"));
            Assert.IsTrue(context.Logger.Contains("Character TTS stopped"));
        }

        [Test]
        public void BotTurnCompleted_ClearsGeneratedResponseIdentity()
        {
            RtviTestContext context = CreateContext();
            context.Registry.RegisterCharacter(new TestCharacterAgent("char-1", "Camila"));
            context.Registry.SetParticipantId("char-1", "participant-1");
            var transcripts = new List<CharacterTranscriptReceived>();
            context.EventHub.Subscribe<CharacterTranscriptReceived>(transcripts.Add);

            context.Gateway.ProcessIncoming(CreateInboundPacket("bot-llm-started", "participant-1"));
            context.Gateway.ProcessIncoming(CreateBotTranscriptionPacket(
                "bot-transcription", "First", "participant-1"));
            context.Gateway.ProcessIncoming(CreateInboundPacket("bot-turn-completed", "participant-1"));
            context.Gateway.ProcessIncoming(CreateInboundPacket("bot-llm-started", "participant-1"));
            context.Gateway.ProcessIncoming(CreateBotTranscriptionPacket(
                "bot-transcription", "Second", "participant-1"));

            Assert.AreEqual(2, transcripts.Count);
            Assert.AreNotEqual(transcripts[0].ResponseId, transcripts[1].ResponseId);
        }

        [Test]
        public void BotLlmStarted_BeginsNewResponseIdentityWithoutWaitingForPriorCompletion()
        {
            RtviTestContext context = CreateContext();
            context.Registry.RegisterCharacter(new TestCharacterAgent("char-1", "Camila"));
            context.Registry.SetParticipantId("char-1", "participant-1");
            var transcripts = new List<CharacterTranscriptReceived>();
            context.EventHub.Subscribe<CharacterTranscriptReceived>(transcripts.Add);

            context.Gateway.ProcessIncoming(CreateInboundPacket("bot-llm-started", "participant-1"));
            context.Gateway.ProcessIncoming(CreateBotTranscriptionPacket(
                "bot-output", "Interrupted answer", "participant-1"));
            context.Gateway.ProcessIncoming(CreateInboundPacket("bot-llm-started", "participant-1"));
            context.Gateway.ProcessIncoming(CreateBotTranscriptionPacket(
                "bot-output", "Next answer", "participant-1"));

            Assert.AreEqual(2, transcripts.Count);
            Assert.AreNotEqual(transcripts[0].ResponseId, transcripts[1].ResponseId);
            Assert.AreNotEqual(transcripts[0].TurnId, transcripts[1].TurnId);
        }

        [Test]
        public void ServerMessage_BotEmotion_PublishesResolvedEmotion()
        {
            RtviTestContext context = CreateContext();
            context.Registry.RegisterCharacter(new TestCharacterAgent("char-1", "Camila"));
            context.Registry.SetParticipantId("char-1", "participant-1");
            CharacterEmotionChanged captured = default;
            context.EventHub.Subscribe<CharacterEmotionChanged>(evt => captured = evt);

            context.Gateway.ProcessIncoming(CreateServerMessagePacket(
                "bot-emotion",
                new Newtonsoft.Json.Linq.JObject { ["emotion"] = "happy", ["scale"] = 3 },
                "participant-1"));

            Assert.AreEqual("char-1", captured.CharacterId);
            Assert.AreEqual("happy", captured.Emotion);
            Assert.AreEqual(3, captured.Intensity);
        }
    }
}
