using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Convai.Domain.Abstractions;
using Convai.Domain.DomainEvents.Runtime;
using Convai.Domain.EventSystem;
using Convai.Domain.Identity;
using Convai.Infrastructure.Networking;
using Convai.Infrastructure.Networking.Models;
using Convai.Infrastructure.Networking.Transport;
using Convai.Runtime;
using Convai.Runtime.Adapters.Networking;
using Convai.Runtime.Behaviors;
using Convai.Runtime.Core;
using Convai.Runtime.Components;
using Convai.Runtime.Core.Configuration;
using Convai.Runtime.Core.Modules;
using Convai.Shared.Types;
using Convai.Shared.Abstractions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ILogger = Convai.Domain.Logging.ILogger;
using Object = UnityEngine.Object;
using SessionState = Convai.Domain.DomainEvents.Session.SessionState;

namespace Convai.Tests.PlayMode.Runtime
{
    public class ReleaseGatePlayModeTests
    {
        private const float AsyncOperationTimeoutSeconds = 5f;

        [UnityTest]
        public IEnumerator Bootstrap_Ownership_ProfileDefinition_Diagnostics()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                CleanupBootstrapState();

                var managerGo = new GameObject("[Test] ConvaiManager");
                managerGo.SetActive(false);

                var manager = managerGo.AddComponent<TestConvaiManager>();
                manager.SetTestTransportProvider(new TestTransportProvider());

                var playerGo = new GameObject("[Test] Player");
                var player = playerGo.AddComponent<ConvaiPlayer>();
                player.Configure("Player One", "speaker-1");

                var characterAGo = new GameObject("[Test] Character A");
                var characterA = characterAGo.AddComponent<ConvaiCharacter>();
                characterA.Configure("char-a", "Alpha");

                var characterBGo = new GameObject("[Test] Character B");
                var characterB = characterBGo.AddComponent<ConvaiCharacter>();
                characterB.Configure("char-b", "Beta");

                manager.SetExplicitPlayer(player);
                manager.SetExplicitCharacters(new[] { characterA, characterB });
                manager.SetExplicitConversationTarget(characterB);

                var roomManager = managerGo.AddComponent<ConvaiRoomManager>();
                SetAutoPropertyBackingField(roomManager, "ConnectOnStart", false);

                var roomConfig = ScriptableObject.CreateInstance<ConvaiRoomManagerProfile>();
                SetPrivateField(roomConfig, "_connectOnStart", false);
                SetPrivateField(roomConfig, "_autoMicStartDelaySeconds", 9999f);
                SetPrivateField(roomManager, "_roomConfigAsset", roomConfig);

                var agentDefinition = ScriptableObject.CreateInstance<ConvaiCharacterProfile>();
                SetPrivateField(agentDefinition, "_characterId", "char-b");
                SetPrivateField(agentDefinition, "_characterName", "Beta");
                SetPrivateField(characterB, "_characterConfigAsset", agentDefinition);

                manager.GetOrCreateHost().OverrideService<ICredentialProvider>(
                    new TestCredentialProvider(new RuntimeCredentials("test-api-key", "https://api.convai.test")));

                managerGo.SetActive(true);

                yield return null;
                yield return null;

                Assert.IsTrue(manager.IsInitialized, "ConvaiManager should bootstrap runtime services in play mode.");
                Assert.AreSame(characterB, manager.ActiveConversationCharacter,
                    "Explicit active conversation target should remain stable.");
                Assert.AreEqual(2, manager.Characters.Count);

                Assert.IsFalse(roomManager.IsConnected, "Room should not auto-connect when ConnectOnStart is disabled.");
                Assert.AreSame(roomConfig, roomManager.RoomConfigAsset);
                Assert.IsFalse(roomManager.EffectiveConnectOnStart);

                Assert.IsTrue(manager.TryGetDiagnosticsService(out IConvaiRuntimeDiagnosticsService diagnosticsService),
                    "Diagnostics service should be resolvable via TryGetDiagnosticsService.");
                ConvaiRuntimeDiagnosticsSnapshot snapshot = diagnosticsService.CaptureSnapshot();

                Assert.IsTrue(snapshot.Graph.HasRuntimeContext,
                    "Builder-built runtime should be present (HasRuntimeContext = ConvaiRuntime != null).");
                Assert.IsTrue(snapshot.Graph.HasManager);
                Assert.IsTrue(snapshot.Graph.HasRoomManager);
                Assert.IsTrue(snapshot.Config.RuntimeCredentialsConfigured);
                Assert.AreEqual(nameof(TestCredentialProvider), snapshot.Config.RuntimeCredentialProviderType);

                Object.Destroy(managerGo);
                Object.Destroy(playerGo);
                Object.Destroy(characterAGo);
                Object.Destroy(characterBGo);
                Object.Destroy(roomConfig);
                Object.Destroy(agentDefinition);

                yield return null;

                CleanupBootstrapState();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator ConnectDisconnect_Lifecycle_IsLocalAndCoherent()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                CleanupBootstrapState();

                var managerGo = new GameObject("[Test] ConvaiManager");
                managerGo.SetActive(false);

                var manager = managerGo.AddComponent<TestConvaiManager>();
                manager.SetTestTransportProvider(new TestTransportProvider());

                var playerGo = new GameObject("[Test] Player");
                var player = playerGo.AddComponent<ConvaiPlayer>();
                player.Configure("Player One", "speaker-1");

                var characterGo = new GameObject("[Test] Character");
                var character = characterGo.AddComponent<ConvaiCharacter>();
                character.Configure("char-1", "Guide");

                manager.SetExplicitPlayer(player);
                manager.SetExplicitCharacters(new[] { character });

                var roomManager = managerGo.AddComponent<ConvaiRoomManager>();
                SetAutoPropertyBackingField(roomManager, "ConnectOnStart", false);

                var roomConfig = ScriptableObject.CreateInstance<ConvaiRoomManagerProfile>();
                SetPrivateField(roomConfig, "_connectOnStart", false);
                SetPrivateField(roomConfig, "_autoMicStartDelaySeconds", 9999f);
                SetPrivateField(roomManager, "_roomConfigAsset", roomConfig);

                bool managerConnected = false;
                bool managerDisconnected = false;
                manager.OnConnected += () => managerConnected = true;
                manager.OnDisconnected += () => managerDisconnected = true;

                manager.GetOrCreateHost().OverrideService<ICredentialProvider>(
                    new TestCredentialProvider(new RuntimeCredentials("test-api-key", "https://api.convai.test")));

                managerGo.SetActive(true);

                yield return null;
                yield return null;

                Assert.IsTrue(manager.IsInitialized);
                Assert.AreEqual(SessionState.Disconnected, roomManager.CurrentState);
                Assert.AreEqual(0, roomManager.ConnectAttemptCount);

                var connectOperation = roomManager.ConnectAsync();
                yield return WaitUntilOrFail(
                    () => connectOperation.IsCompleted,
                    AsyncOperationTimeoutSeconds,
                    () => $"ConnectAsync timed out. State={roomManager.CurrentState}, Attempts={roomManager.ConnectAttemptCount}, IsConnected={roomManager.IsConnected}");

                Assert.IsTrue(connectOperation.AsTask().Result.IsValid,
                    "Stub controller should allow a local connect success.");

                yield return WaitUntilOrFail(
                    () => roomManager.CurrentState == SessionState.Connected,
                    AsyncOperationTimeoutSeconds,
                    () => $"Room never reached Connected. State={roomManager.CurrentState}, Attempts={roomManager.ConnectAttemptCount}, IsConnected={roomManager.IsConnected}");

                Assert.IsTrue(roomManager.IsConnected);
                Assert.AreEqual(1, roomManager.ConnectAttemptCount);
                Assert.AreEqual(0, roomManager.ReconnectCount, "First connect should not count as a reconnect.");

                Assert.IsTrue(managerConnected, "Manager should observe the runtime-connected state via event hub.");

                var disconnectOperation = roomManager.DisconnectAsync();
                yield return WaitUntilOrFail(
                    () => disconnectOperation.IsCompleted,
                    AsyncOperationTimeoutSeconds,
                    () => $"DisconnectAsync timed out. State={roomManager.CurrentState}, Attempts={roomManager.ConnectAttemptCount}, IsConnected={roomManager.IsConnected}");

                Assert.IsFalse(roomManager.IsConnected);
                Assert.AreEqual(SessionState.Disconnected, roomManager.CurrentState);
                Assert.IsTrue(managerDisconnected);

                Object.Destroy(managerGo);
                Object.Destroy(playerGo);
                Object.Destroy(characterGo);
                Object.Destroy(roomConfig);

                yield return null;

                CleanupBootstrapState();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator LateCharacterModule_RefreshesOwnershipAndInjectsRuntimeCharacter()
        {
            LogAssert.ignoreFailingMessages = true;
            GameObject managerGo = null;
            GameObject installerGo = null;
            GameObject installerCharacterGo = null;
            GameObject characterGo = null;
            ConvaiRoomManagerProfile roomConfig = null;

            try
            {
                CleanupBootstrapState();

                managerGo = new GameObject("[Test] ConvaiManager");
                managerGo.SetActive(false);

                var manager = managerGo.AddComponent<TestConvaiManager>();
                manager.SetTestTransportProvider(new TestTransportProvider());

                installerCharacterGo = new GameObject("[Test] Installer Character");
                var installerCharacter = installerCharacterGo.AddComponent<ConvaiCharacter>();
                installerCharacter.Configure("installer-character", "Installer Character");

                installerGo = new GameObject("[Test] Scene Installer");
                var installer = installerGo.AddComponent<ConvaiSceneInstaller>();
                SetPrivateField(installer, "_characters", new List<ConvaiCharacter> { installerCharacter });
                SetPrivateField(installer, "_ownedCharacters", new List<ConvaiCharacter> { installerCharacter });
                SetPrivateField(manager, "_sceneInstaller", installer);

                var roomManager = managerGo.AddComponent<ConvaiRoomManager>();
                SetAutoPropertyBackingField(roomManager, "ConnectOnStart", false);

                roomConfig = ScriptableObject.CreateInstance<ConvaiRoomManagerProfile>();
                SetPrivateField(roomConfig, "_connectOnStart", false);
                SetPrivateField(roomConfig, "_autoMicStartDelaySeconds", 9999f);
                SetPrivateField(roomManager, "_roomConfigAsset", roomConfig);

                managerGo.SetActive(true);

                yield return WaitUntilOrFail(
                    () => manager.ConvaiRuntime?.State == RuntimeState.Running,
                    AsyncOperationTimeoutSeconds,
                    () => $"Runtime never reached Running. State={manager.ConvaiRuntime?.State}");

                Assert.That(manager.Characters, Does.Contain(installerCharacter));

                characterGo = new GameObject("[Test] Runtime Character");
                characterGo.SetActive(false);
                var character = characterGo.AddComponent<ConvaiCharacter>();
                character.Configure("runtime-character", "Runtime Character");
                var lateModule = characterGo.AddComponent<LateOwnedAgentTestModule>();

                characterGo.SetActive(true);

                yield return WaitUntilOrFail(
                    () => character.IsInjected && lateModule.WasStarted,
                    AsyncOperationTimeoutSeconds,
                    () =>
                        $"Late character was not initialized. Injected={character.IsInjected}, ModuleStarted={lateModule.WasStarted}");

                Assert.That(manager.Characters, Does.Contain(character));
                Assert.That(manager.Characters, Does.Contain(installerCharacter),
                    "Installer ownership must remain while adding the late-created module owner.");
            }
            finally
            {
                if (characterGo != null) Object.DestroyImmediate(characterGo);
                if (installerGo != null) Object.DestroyImmediate(installerGo);
                if (installerCharacterGo != null) Object.DestroyImmediate(installerCharacterGo);
                if (managerGo != null) Object.DestroyImmediate(managerGo);
                if (roomConfig != null) Object.DestroyImmediate(roomConfig);

                CleanupBootstrapState();
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator ExplicitPlainRuntimeCharacter_IsInjectedWithoutModule()
        {
            LogAssert.ignoreFailingMessages = true;
            GameObject managerGo = null;
            GameObject characterGo = null;
            ConvaiRoomManagerProfile roomConfig = null;

            try
            {
                CleanupBootstrapState();

                managerGo = new GameObject("[Test] ConvaiManager");
                managerGo.SetActive(false);

                var manager = managerGo.AddComponent<TestConvaiManager>();
                manager.SetTestTransportProvider(new TestTransportProvider());

                var roomManager = managerGo.AddComponent<ConvaiRoomManager>();
                SetAutoPropertyBackingField(roomManager, "ConnectOnStart", false);

                roomConfig = ScriptableObject.CreateInstance<ConvaiRoomManagerProfile>();
                SetPrivateField(roomConfig, "_connectOnStart", false);
                SetPrivateField(roomConfig, "_autoMicStartDelaySeconds", 9999f);
                SetPrivateField(roomManager, "_roomConfigAsset", roomConfig);

                managerGo.SetActive(true);

                yield return WaitUntilOrFail(
                    () => manager.ConvaiRuntime?.State == RuntimeState.Running,
                    AsyncOperationTimeoutSeconds,
                    () => $"Runtime never reached Running. State={manager.ConvaiRuntime?.State}");

                characterGo = new GameObject("[Test] Plain Runtime Character");
                var character = characterGo.AddComponent<ConvaiCharacter>();
                character.Configure("plain-runtime-character", "Plain Runtime Character");

                Assert.IsFalse(character.IsInjected,
                    "Without a module callback, the character should remain uninjected until ownership is registered.");

                manager.SetExplicitCharacters(new[] { character });
                manager.SetExplicitConversationTarget(character);

                Assert.IsTrue(character.IsInjected);
                Assert.That(manager.Characters, Does.Contain(character));
                Assert.AreSame(character, manager.ActiveConversationCharacter);
            }
            finally
            {
                if (characterGo != null) Object.DestroyImmediate(characterGo);
                if (managerGo != null) Object.DestroyImmediate(managerGo);
                if (roomConfig != null) Object.DestroyImmediate(roomConfig);

                CleanupBootstrapState();
                LogAssert.ignoreFailingMessages = false;
            }
        }

        private static void CleanupBootstrapState()
        {
            // Platform/provider selection is explicit now, so there is no global registry state to reset.
        }

        private static IEnumerator WaitUntilOrFail(Func<bool> predicate, float timeoutSeconds, Func<string> timeoutMessage)
        {
            float startTime = Time.realtimeSinceStartup;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup - startTime >= timeoutSeconds)
                    Assert.Fail(timeoutMessage?.Invoke() ?? $"Timed out after {timeoutSeconds:0.##} seconds.");

                yield return null;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            Assert.IsNotNull(target);
            Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.IsNotNull(field, $"Missing private field {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            Assert.IsNotNull(target);
            FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing auto-property backing field for {propertyName}.");
            field.SetValue(target, value);
        }

        private sealed class TestCredentialProvider : ICredentialProvider
        {
            private readonly RuntimeCredentials _credentials;

            public TestCredentialProvider(RuntimeCredentials credentials) => _credentials = credentials;

            public bool HasValidCredentials => _credentials.IsConfigured;

            public string GetApiKey() => _credentials.ApiKey;

            public string GetServerUrl() => _credentials.CoreServerUrl;

            public void Refresh() { }
        }

        private sealed class TestMicrophoneSourceFactory : IMicrophoneSourceFactory
        {
            public IMicrophoneSource Create(string deviceName, int deviceIndex = 0, GameObject hostObject = null) => null;
            public string[] GetAvailableDevices() => Array.Empty<string>();
        }

        private sealed class TestVideoSourceFactory : IVideoSourceFactory
        {
            public IVideoSource CreateFromRenderTexture(RenderTexture texture, string name = null) => null;

            public IVideoSource CreateFromCamera(Camera camera, int width, int height, string name = null) => null;

            public IVideoSource CreateFromCanvasCapture(string name = null, int targetFrameRate = 15) => null;
        }

        private sealed class TestAudioStreamFactory : IAudioStreamFactory
        {
            public IDisposable Create(IRemoteAudioTrack track, AudioSource audioSource) => new NoopDisposable();

            private sealed class NoopDisposable : IDisposable
            {
                public void Dispose() { }
            }
        }

        private sealed class TestTransportProvider : ITransportProvider
        {
            private readonly TestMicrophoneSourceFactory _microphoneSourceFactory = new();
            private readonly TestVideoSourceFactory _videoSourceFactory = new();
            private readonly TestAudioStreamFactory _audioStreamFactory = new();

            public TransportCapabilities Capabilities => TransportCapabilities.Native();

            public IRealtimeTransport CreateTransport() => null;

            public IMicrophoneSourceFactory CreateMicrophoneFactory() => _microphoneSourceFactory;

            public IAudioStreamFactory CreateAudioStreamFactory() => _audioStreamFactory;

            public IVideoSourceFactory CreateVideoSourceFactory() => _videoSourceFactory;

            public IConvaiRoomControllerFactory CreateRoomControllerFactory() => new TestRoomControllerFactory(null);
        }

        private sealed class TestConvaiManager : ConvaiManager
        {
            private ITransportProvider _transportProvider;

            public void SetTestTransportProvider(ITransportProvider transportProvider)
            {
                _transportProvider = transportProvider;
            }

            protected override ITransportProvider GetPlatformTransportProvider() => _transportProvider;
        }

        private sealed class TestRoomControllerFactory : IConvaiRoomControllerFactory
        {
            private readonly IEventHub _eventHub;

            public TestRoomControllerFactory(IEventHub eventHub) => _eventHub = eventHub;

            public IConvaiRoomController Create(
                IAgentRegistry agentRegistry,
                IPlayerSession playerSession,
                ITransportConfiguration transportConfiguration,
                ISessionPersistence sessionPersistence,
                IMainThreadDispatcher dispatcher,
                ILogger logger,
                IEventHub eventHub,
                INarrativeSectionNameResolver sectionNameResolver = null)
            {
                return new TestRoomController(eventHub ?? _eventHub);
            }
        }

        private sealed class TestRoomController : IConvaiRoomController
        {
            private readonly IEventHub _eventHub;
            private Func<string, bool> _audioSubscriptionPolicy;

            public TestRoomController(IEventHub eventHub) => _eventHub = eventHub;

            public bool HasRoomDetails { get; private set; }
            public bool IsConnectedToRoom { get; private set; }
            public bool IsMicMuted { get; private set; }
            public string SessionID { get; private set; }
            public string CharacterSessionID { get; private set; }
            public string RoomName { get; private set; }
            public string RoomURL { get; private set; }
            public string Token { get; private set; }
            public string ResolvedSpeakerId { get; private set; }
            public string RequestTraceId => string.Empty;
            public string ResolvedEndUserId => string.Empty;
            public IReadOnlyDictionary<string, object> ResolvedEndUserMetadata => null;
            public RTVIHandler RTVIHandler => null;
            public IRoomFacade CurrentRoom => null;

#pragma warning disable CS0067
            public event Action OnRoomConnectionSuccessful;
            public event Action OnRoomConnectionFailed;
            public event Action<bool> OnMicMuteChanged;
            public event Action OnRoomReconnecting;
            public event Action OnRoomReconnected;
            public event Action OnUnexpectedRoomDisconnected;
            public event Action<IRemoteAudioTrack, string, string> OnRemoteAudioTrackSubscribed;
            public event Action<string, string> OnRemoteAudioTrackUnsubscribed;
#pragma warning restore CS0067

            public Task<RoomConnectionAttemptResult> InitializeAsync(
                string connectionType,
                string coreServerUrl,
                string characterId,
                string storedSessionId,
                bool enableSessionResume,
                string dynamicInfoText,
                bool keepDynamicInfoInContext)
            {
                return InitializeAsync(connectionType, coreServerUrl, characterId, storedSessionId,
                    enableSessionResume, dynamicInfoText, keepDynamicInfoInContext, null, CancellationToken.None);
            }

            public Task<RoomConnectionAttemptResult> InitializeAsync(
                string connectionType,
                string coreServerUrl,
                string characterId,
                string storedSessionId,
                bool enableSessionResume,
                string dynamicInfoText,
                bool keepDynamicInfoInContext,
                RoomJoinOptions joinOptions,
                CancellationToken cancellationToken = default)
            {
                HasRoomDetails = true;
                IsConnectedToRoom = true;
                RoomName = "test-room";
                RoomURL = "wss://test-room";
                Token = "test-token";
                SessionID = "session-1";
                CharacterSessionID = string.IsNullOrWhiteSpace(storedSessionId) ? "char-session-1" : storedSessionId;
                ResolvedSpeakerId = "speaker-1";

                OnRoomConnectionSuccessful?.Invoke();

                _eventHub?.Publish(CharacterReady.Create(characterId, "participant-1"));
                return Task.FromResult(RoomConnectionAttemptResult.Success());
            }

            public void DisconnectFromRoom()
            {
                IsConnectedToRoom = false;
            }

            public Task DisconnectFromRoomAsync(CancellationToken cancellationToken = default)
            {
                IsConnectedToRoom = false;
                return Task.CompletedTask;
            }

            public void SetMicMuted(bool mute)
            {
                IsMicMuted = mute;
                OnMicMuteChanged?.Invoke(mute);
            }

            public void ToggleMicMute() => SetMicMuted(!IsMicMuted);

            public bool SetCharacterAudioMuted(string characterId, bool mute) => true;

            public bool MuteCharacter(string characterId) => true;

            public bool UnmuteCharacter(string characterId) => true;

            public bool IsCharacterAudioMuted(string characterId) => false;

            public void SetAudioSubscriptionPolicy(Func<string, bool> policy) => _audioSubscriptionPolicy = policy;

            public void ApplyRemoteAudioPreference(string characterId, bool enabled)
            {
                _audioSubscriptionPolicy?.Invoke(characterId);
            }

            public void Dispose()
            {
                IsConnectedToRoom = false;
            }
        }
    }

    public sealed class LateOwnedAgentTestModule : MonoBehaviour, IConvaiModule
    {
        public bool WasStarted { get; private set; }

        public string ModuleId => "late-owned-agent-test";
        public string DisplayName => "Late Owned Agent Test";
        public IReadOnlyList<string> RequiredModules => Array.Empty<string>();
        public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
        public IReadOnlyList<Type> ProvidedServices => Array.Empty<Type>();
        public bool IsActive => isActiveAndEnabled;

        private void Awake() => ConvaiManager.ActiveManager?.RegisterModule(this);

        private void OnDestroy() => ConvaiManager.ActiveManager?.UnregisterModule(this);

        public ValueTask RegisterAsync(IModuleContext context, CancellationToken ct = default) => default;

        public ValueTask StartAsync(IModuleContext context, CancellationToken ct = default)
        {
            WasStarted = true;
            return default;
        }

        public ValueTask PauseAsync(RuntimePauseReason reason, CancellationToken ct = default) => default;
        public ValueTask ResumeAsync(CancellationToken ct = default) => default;
        public ValueTask StopAsync(CancellationToken ct = default) => default;
    }
}
