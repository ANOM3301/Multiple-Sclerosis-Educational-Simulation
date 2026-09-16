using System;
using System.Collections.Generic;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.Emotion;
using Convai.Domain.Errors;
using Convai.Runtime.Adapters.Networking;
using Convai.Runtime.Behaviors;
using Convai.Runtime.Core.Configuration;
using Convai.Runtime.DynamicContext;
using Convai.Tests.EditMode.Mocks;
using NUnit.Framework;
using UnityEngine;

namespace Convai.Tests.EditMode.Runtime
{
    [TestFixture]
    public sealed class RoomCompositionServiceTests
    {
        [Test]
        public void ValidateAndComposeStartup_WhenNotInjected_DisablesManager()
        {
            var service = new RoomCompositionService();

            RoomCompositionStartupResult result = service.ValidateAndComposeStartup(
                new RoomCompositionContext(),
                new RoomCompositionState { IsInjected = false });

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.DisableManager);
            Assert.That(result.ErrorMessage, Does.Contain("dependencies were not injected"));
        }

        [Test]
        public void ValidateAndComposeStartup_WhenCredentialsMissing_ReturnsCredentialFailure()
        {
            var service = new RoomCompositionService();

            RoomCompositionStartupResult result = service.ValidateAndComposeStartup(
                new RoomCompositionContext
                {
                    CredentialProvider = new TestCredentialProvider(null, "https://core.convai.com")
                },
                new RoomCompositionState { IsInjected = true });

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(RoomStartupFailureReason.MissingRuntimeCredentials, result.FailureReason);
            Assert.AreEqual(SessionErrorCodes.ConfigApiKeyMissing, result.FailureErrorCode);
            Assert.AreEqual("Runtime credentials are not configured", result.FailureRecordMessage);
        }

        [Test]
        public void ValidateAndComposeStartup_WhenCharactersAreCreatedAtRuntime_DefersConnection()
        {
            var service = new RoomCompositionService();
            var player = new TestPlayerAgent();

            RoomCompositionStartupResult result = service.ValidateAndComposeStartup(
                new RoomCompositionContext
                {
                    AgentRegistry = new MockAgentRegistry(),
                    OwnershipProvider = new TestOwnershipProvider(
                        new RoomOwnershipSnapshot(
                            player,
                            Array.Empty<IConvaiCharacterAgent>(),
                            null)),
                    CredentialProvider = new TestCredentialProvider("api-key", "https://core.convai.com"),
                    PostToMainThread = _ => true,
                    ConnectOnStart = true
                },
                new RoomCompositionState { IsInjected = true });

            Assert.IsTrue(result.IsValid);
            Assert.IsFalse(result.ShouldAutoConnect);
            Assert.AreEqual(RoomStartupFailureReason.MissingCharacters, result.FailureReason);
            Assert.IsNull(result.Artifacts);
            Assert.IsNull(result.FailureErrorCode);
        }

        [Test]
        public void HandleOwnedAgentStateChanged_WhenStartupNotCompleted_DefersUntilStartup()
        {
            var service = new RoomCompositionService();

            RoomOwnershipChangeResult result = service.HandleOwnedAgentStateChanged(
                new RoomCompositionContext
                {
                    OwnershipProvider = new TestOwnershipProvider(
                        new RoomOwnershipSnapshot(
                            null,
                            new[] { new TestCharacterAgent("char-1") },
                            new ActiveConversationTarget(new TestCharacterAgent("char-1"))))
                },
                new RoomCompositionState { IsInjected = true, HasStarted = false });

            Assert.AreEqual(RoomOwnershipRebindOutcome.DeferredUntilStartup, result.Outcome);
            Assert.AreEqual("char-1", result.RequestedCharacterId);
        }

        [Test]
        public void HandleOwnedAgentStateChanged_WhenConnected_ReturnsPendingReconnect()
        {
            var service = new RoomCompositionService();

            RoomOwnershipChangeResult result = service.HandleOwnedAgentStateChanged(
                new RoomCompositionContext
                {
                    OwnershipProvider = new TestOwnershipProvider(
                        new RoomOwnershipSnapshot(
                            null,
                            new[] { new TestCharacterAgent("char-2") },
                            new ActiveConversationTarget(new TestCharacterAgent("char-2"))))
                },
                new RoomCompositionState
                {
                    IsInjected = true,
                    HasStarted = true,
                    CurrentState = SessionState.Connected
                });

            Assert.AreEqual(RoomOwnershipRebindOutcome.PendingReconnect, result.Outcome);
            Assert.IsTrue(result.SetPendingReconnect);
            Assert.AreEqual("char-2", result.PendingReconnectCharacterId);
        }

        [Test]
        public void HandleOwnedAgentStateChanged_WhenConnectionIsTransitioning_RejectsRebind()
        {
            var service = new RoomCompositionService();

            RoomOwnershipChangeResult result = service.HandleOwnedAgentStateChanged(
                new RoomCompositionContext
                {
                    OwnershipProvider = new TestOwnershipProvider(
                        new RoomOwnershipSnapshot(
                            null,
                            new[] { new TestCharacterAgent("char-3") },
                            new ActiveConversationTarget(new TestCharacterAgent("char-3"))))
                },
                new RoomCompositionState
                {
                    IsInjected = true,
                    HasStarted = true,
                    CurrentState = SessionState.Connecting
                });

            Assert.AreEqual(RoomOwnershipRebindOutcome.RejectedTransitionState, result.Outcome);
            Assert.AreEqual("char-3", result.RequestedCharacterId);
        }

        private sealed class TestCredentialProvider : ICredentialProvider
        {
            private readonly string _apiKey;
            private readonly string _serverUrl;

            public TestCredentialProvider(string apiKey, string serverUrl)
            {
                _apiKey = apiKey;
                _serverUrl = serverUrl;
            }

            public bool HasValidCredentials => !string.IsNullOrEmpty(_apiKey);
            public string GetApiKey() => _apiKey;
            public string GetServerUrl() => _serverUrl;
            public void Refresh() { }
        }

        private sealed class TestOwnershipProvider : IRoomOwnershipProvider
        {
            private readonly RoomOwnershipSnapshot _snapshot;

            public TestOwnershipProvider(RoomOwnershipSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public RoomOwnershipSnapshot CaptureOwnership() => _snapshot;
        }

        private sealed class TestCharacterAgent : IConvaiCharacterAgent
        {
            public TestCharacterAgent(string characterId, string characterName = "Test Character")
            {
                CharacterId = characterId;
                CharacterName = characterName;
            }

            public string CharacterId { get; }
            public string CharacterName { get; }
            public Color NameTagColor => Color.white;
            public bool EnableSessionResume => false;
            public string InitialDynamicInfoText => string.Empty;
            public bool InitialDynamicInfoKeepInContext => false;
            public IConvaiDynamicContext DynamicContext { get; } = new MockDynamicContext();
            public EmotionDetectionMode EmotionDetectionMode => Convai.Domain.Emotion.EmotionDetectionMode.Off;
            public void SendTrigger(string triggerName) { }
            public void SendNarrativeEvent(string eventMessage) { }
            public void SendNarrativeSpeech(string speechText) { }
            public void UpdateTemplateKeys(Dictionary<string, string> templateKeys) { }
        }

        private sealed class TestPlayerAgent : IConvaiPlayerAgent
        {
            public string PlayerName => "Test Player";
            public string PlayerId => "test-player";
            public Color NameTagColor => Color.white;
            public event Action<string> OnTextMessageSent;

            public void SendTextMessage(string message) => OnTextMessageSent?.Invoke(message);
        }
    }
}
