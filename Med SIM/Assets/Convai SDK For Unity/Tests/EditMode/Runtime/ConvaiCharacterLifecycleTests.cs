using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Convai.Domain.DomainEvents.Runtime;
using Convai.Domain.EventSystem;
using Convai.Domain.Logging;
using Convai.Runtime.Components;
using Convai.Runtime.Core.DependencyInjection;
using Convai.Tests.EditMode.Mocks;
using NUnit.Framework;
using UnityEngine;
using ILogger = Convai.Domain.Logging.ILogger;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.Runtime
{
    /// <summary>
    ///     Tests for ConvaiCharacter lifecycle, initialization, and configuration.
    /// </summary>
    public class ConvaiCharacterLifecycleTests
    {
        private readonly List<GameObject> _createdObjects = new();
        private MockRoomAudioService _audioService;
        private MockRoomConnectionService _connectionService;
        private EventHub _eventHub;
        private MockAgentRegistry _agentRegistry;
        private TestLogger _logger;

        [SetUp]
        public void SetUp()
        {
            _eventHub = new EventHub(new ImmediateScheduler());
            _connectionService = new MockRoomConnectionService();
            _audioService = new MockRoomAudioService();
            _agentRegistry = new MockAgentRegistry();
            _logger = new TestLogger();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _createdObjects)
                if (go != null)
                    Object.DestroyImmediate(go);
            _createdObjects.Clear();
            _eventHub = null;
        }

        private ConvaiCharacter CreateCharacter(string characterId = "test-char-id",
            string characterName = "TestCharacter")
        {
            var go = new GameObject(characterName);
            _createdObjects.Add(go);

            var character = go.AddComponent<ConvaiCharacter>();
            character.Configure(characterId, characterName);

            return character;
        }

        private ConvaiCharacter CreateAndInjectCharacter(string characterId = "test-char-id",
            string characterName = "TestCharacter")
        {
            ConvaiCharacter character = CreateCharacter(characterId, characterName);
            character.InjectDependencies(CreateDependencies());

            return character;
        }

        private ConvaiCharacterDependencies CreateDependencies() =>
            new(_eventHub, _connectionService, _audioService, _agentRegistry, _logger);

        private void PublishCharacterReady(ConvaiCharacter character)
        {
            _eventHub.Publish(CharacterReady.Create(character.CharacterId, $"participant-{character.CharacterId}"));
        }

        private async Task StartConversationWithReadyAsync(ConvaiCharacter character)
        {
            var startTask = character.StartConversationAsync();
            PublishCharacterReady(character);
            await startTask;
        }

        #region Cleanup Tests

        [Test]
        public void OnDisable_UnregistersFromRegistry()
        {
            var go = new GameObject("TestCharacter");
            _createdObjects.Add(go);

            var character = go.AddComponent<ConvaiCharacter>();
            character.Configure("test-char-id", "TestCharacter");
            character.InjectDependencies(CreateDependencies());

            Assert.IsTrue(_agentRegistry.HasCharacter("test-char-id"),
                "Character should be registered after injection");

            MethodInfo onDisableMethod =
                typeof(ConvaiCharacter).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance);
            onDisableMethod.Invoke(character, null);

            Assert.IsFalse(_agentRegistry.HasCharacter("test-char-id"),
                "Character should be unregistered after OnDisable");
        }

        #endregion

        private sealed class ImmediateScheduler : IUnityScheduler
        {
            public void ScheduleOnMainThread(Action action) => action?.Invoke();
            public void ScheduleOnBackground(Action action) => action?.Invoke();
            public bool IsMainThread() => true;
        }

        private sealed class TestLogger : ILogger
        {
            public List<string> DebugMessages { get; } = new();
            public List<string> WarningMessages { get; } = new();
            public List<string> ErrorMessages { get; } = new();
            public List<string> InfoMessages { get; } = new();

            public void Log(LogLevel level, string message, LogCategory category = LogCategory.SDK) { }

            public void Log(LogLevel level, string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            {
            }

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

        #region Default Configuration Tests

        [Test]
        public void DefaultConfiguration_RemoteAudioIsEnabled()
        {
            ConvaiCharacter character = CreateCharacter();

            Assert.IsTrue(character.EnableRemoteAudioOnStart,
                "EnableRemoteAudioOnStart should default to true (audio enabled by default)");
        }

        [Test]
        public void DefaultConfiguration_CharacterReadyTimeoutIsSet()
        {
            ConvaiCharacter character = CreateCharacter();

            Assert.AreEqual(30f, character.CharacterReadyTimeoutSeconds,
                "CharacterReadyTimeoutSeconds should default to 30 seconds");
        }

        #endregion

        #region Initialization Tests

        [Test]
        public void Initialization_EnablesRemoteAudio()
        {
            ConvaiCharacter character = CreateCharacter();

            character.InjectDependencies(CreateDependencies());

            Assert.IsTrue(_audioService.IsRemoteAudioEnabled(character.CharacterId),
                "Remote audio should be enabled after initialization with default settings");
        }

        [Test]
        public void Initialization_RegistersWithRegistry()
        {
            ConvaiCharacter character = CreateAndInjectCharacter();

            Assert.IsTrue(_agentRegistry.HasCharacter("test-char-id"),
                "Character should be registered with the agent registry after injection");
        }

        [Test]
        public void Initialization_SetsInjectedFlag()
        {
            ConvaiCharacter character = CreateCharacter();

            Assert.IsFalse(character.IsInjected, "IsInjected should be false before injection");

            character.InjectDependencies(CreateDependencies());

            Assert.IsTrue(character.IsInjected, "IsInjected should be true after injection");
        }

        #endregion

        #region Connection Lifecycle Tests

        [Test]
        public async Task StartConversation_ConnectsToService()
        {
            ConvaiCharacter character = CreateAndInjectCharacter();

            Assert.IsFalse(character.IsSessionConnected, "Should not be connected initially");

            await StartConversationWithReadyAsync(character);
            Assert.IsTrue(character.IsSessionConnected,
                "Character should be connected after StartConversationAsync");
        }

        [Test]
        public async Task StopConversation_DisconnectsFromService()
        {
            ConvaiCharacter character = CreateAndInjectCharacter();

            await StartConversationWithReadyAsync(character);
            Assert.IsTrue(character.IsSessionConnected, "Should be connected after StartConversationAsync");

            await character.StopConversationAsync();

            Assert.IsFalse(character.IsSessionConnected,
                "Character should be disconnected after StopConversationAsync");
        }

        [Test]
        public async Task StartConversation_WhenAlreadyConnected_CompletesSuccessfully()
        {
            ConvaiCharacter character = CreateAndInjectCharacter();

            await StartConversationWithReadyAsync(character);
            Assert.IsTrue(character.IsSessionConnected);

            await character.StartConversationAsync();
        }

        [Test]
        public async Task StopConversation_WhenAlreadyDisconnected_DoesNotThrow()
        {
            ConvaiCharacter character = CreateAndInjectCharacter();

            Assert.IsFalse(character.IsSessionConnected, "Should not be connected initially");

            await character.StopConversationAsync();
            Assert.Pass("StopConversationAsync should not throw when already disconnected");
        }

        #endregion

        #region Remote Audio Control Tests

        [Test]
        public void SetRemoteAudioEnabled_UpdatesAudioService()
        {
            ConvaiCharacter character = CreateAndInjectCharacter();

            bool result = character.SetRemoteAudioEnabled(false);

            Assert.IsTrue(result, "SetRemoteAudioEnabled should return true on success");
            Assert.IsFalse(_audioService.IsRemoteAudioEnabled(character.CharacterId),
                "Audio service should reflect disabled state");

            result = character.SetRemoteAudioEnabled(true);

            Assert.IsTrue(result, "SetRemoteAudioEnabled should return true on success");
            Assert.IsTrue(_audioService.IsRemoteAudioEnabled(character.CharacterId),
                "Audio service should reflect enabled state");
        }

        [Test]
        public void IsRemoteAudioEnabled_ReflectsCurrentState()
        {
            ConvaiCharacter character = CreateAndInjectCharacter();

            Assert.IsTrue(character.IsRemoteAudioEnabled,
                "IsRemoteAudioEnabled should be true by default after initialization");

            character.DisableRemoteAudio();
            Assert.IsFalse(character.IsRemoteAudioEnabled,
                "IsRemoteAudioEnabled should be false after DisableRemoteAudio");

            character.EnableRemoteAudio();
            Assert.IsTrue(character.IsRemoteAudioEnabled,
                "IsRemoteAudioEnabled should be true after EnableRemoteAudio");
        }

        [Test]
        public void ToggleRemoteAudio_TogglesState()
        {
            ConvaiCharacter character = CreateAndInjectCharacter();

            Assert.IsTrue(character.IsRemoteAudioEnabled);

            character.ToggleRemoteAudio();
            Assert.IsFalse(character.IsRemoteAudioEnabled,
                "ToggleRemoteAudio should disable when currently enabled");

            character.ToggleRemoteAudio();
            Assert.IsTrue(character.IsRemoteAudioEnabled,
                "ToggleRemoteAudio should enable when currently disabled");
        }

        [Test]
        public void OnRemoteAudioEnabledChanged_RaisedWhenStateChanges()
        {
            ConvaiCharacter character = CreateAndInjectCharacter();
            bool eventRaised = false;
            bool? receivedValue = null;

            character.OnRemoteAudioEnabledChanged += enabled =>
            {
                eventRaised = true;
                receivedValue = enabled;
            };

            character.DisableRemoteAudio();

            Assert.IsTrue(eventRaised, "OnRemoteAudioEnabledChanged should be raised");
            Assert.IsFalse(receivedValue, "Event should receive false when disabling");
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void Configure_UpdatesCharacterIdAndName()
        {
            ConvaiCharacter character = CreateCharacter();

            character.Configure("new-char-id", "NewCharacterName");

            Assert.AreEqual("new-char-id", character.CharacterId);
            Assert.AreEqual("NewCharacterName", character.CharacterName);
        }

        [Test]
        public void Configure_UsesCharacterIdAsNameWhenNameNotProvided()
        {
            ConvaiCharacter character = CreateCharacter();

            character.Configure("my-character-id");

            Assert.AreEqual("my-character-id", character.CharacterId);
            Assert.AreEqual("my-character-id", character.CharacterName,
                "CharacterName should default to CharacterId when not specified");
        }

        [Test]
        public void CharacterReadyTimeoutSeconds_CanBeModified()
        {
            ConvaiCharacter character = CreateCharacter();

            character.CharacterReadyTimeoutSeconds = 60f;
            Assert.AreEqual(60f, character.CharacterReadyTimeoutSeconds);

            character.CharacterReadyTimeoutSeconds = 0f;
            Assert.AreEqual(0f, character.CharacterReadyTimeoutSeconds,
                "Setting to 0 should disable timeout");

            character.CharacterReadyTimeoutSeconds = -10f;
            Assert.AreEqual(0f, character.CharacterReadyTimeoutSeconds,
                "Negative values should be clamped to 0");
        }

        #endregion
    }
}
