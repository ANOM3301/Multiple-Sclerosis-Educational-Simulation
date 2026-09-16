using System;
using System.Collections.Generic;
using Convai.Domain.DomainEvents.Runtime;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.EventSystem;
using Convai.Domain.Logging;
using Convai.Modules.Attention.Components;
using Convai.Runtime.Actions;
using Convai.Runtime.Animation;
using Convai.Runtime.Components;
using Convai.Runtime.Core.DependencyInjection;
using Convai.Runtime;
using Convai.Runtime.DynamicContext;
using Convai.Runtime.Embodiment;
using Convai.Runtime.SceneMetadata;
using Convai.Shared.Actions;
using Convai.Tests.EditMode.Fixtures;
using Convai.Tests.EditMode.Mocks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using ILogger = Convai.Domain.Logging.ILogger;

namespace Convai.Tests.EditMode.Attention
{
    [TestFixture]
    public sealed class ConvaiAttentionDynamicContextBridgeTests
    {
        private readonly List<GameObject> _createdObjects = new();
        private MockRoomConnectionService _connectionService;
        private EventHub _eventHub;
        private MockAgentRegistry _agentRegistry;
        private TestLogger _logger;

        [SetUp]
        public void SetUp()
        {
            ConvaiMetadataRegistry.Clear();
            _eventHub = new EventHub(new ImmediateScheduler());
            _connectionService = new MockRoomConnectionService();
            _agentRegistry = new MockAgentRegistry();
            _logger = new TestLogger();
        }

        [TearDown]
        public void TearDown()
        {
            ConvaiMetadataRegistry.Clear();

            foreach (GameObject go in _createdObjects)
                if (go != null)
                    Object.DestroyImmediate(go);

            _createdObjects.Clear();
        }

        [Test]
        public void EmbodimentTick_WithFocusedWorldObject_StagesAttentionForFlush()
        {
            GameObject root = new("CharacterRig");
            _createdObjects.Add(root);

            var character = root.AddComponent<ConvaiCharacter>();
            character.Configure("bridge-char", "BridgeCharacter");
            character.InjectDependencies(new ConvaiCharacterDependencies(
                _eventHub,
                _connectionService,
                new MockRoomAudioService(),
                _agentRegistry,
                _logger));

            EmbodimentContext context = root.AddComponent<EmbodimentContext>();
            context.Populate(_eventHub, _logger);

            ConfigureActionObject(character, "chest");
            GameObject chest = CreateWorldObject("chest");
            var fakeAttention = new FakeAttentionSource();
            context.RegisterAttentionSource(fakeAttention);

            var bridge = root.AddComponent<ConvaiAttentionDynamicContextBridge>();
            fakeAttention.SetValid(chest.transform, chest.transform.position, commitment: 1f, generationId: 7);

            MakeReady(character);
            _connectionService.SentDynamicContextUpdates.Clear();

            ((IEmbodimentTickable)bridge).EmbodimentTick(1f / 60f);
            character.DynamicContext.Flush();

            Assert.AreEqual(1, _connectionService.SentDynamicContextUpdates.Count);
            Assert.AreEqual("chest", _connectionService.SentDynamicContextUpdates[0].CurrentAttentionObject);
        }

        [Test]
        public void EmbodimentTick_WhenAttentionClears_DoesNotSendUntilFlush()
        {
            GameObject root = new("CharacterRig");
            _createdObjects.Add(root);

            var character = root.AddComponent<ConvaiCharacter>();
            character.Configure("bridge-char", "BridgeCharacter");
            character.InjectDependencies(new ConvaiCharacterDependencies(
                _eventHub,
                _connectionService,
                new MockRoomAudioService(),
                _agentRegistry,
                _logger));

            EmbodimentContext context = root.AddComponent<EmbodimentContext>();
            context.Populate(_eventHub, _logger);

            ConfigureActionObject(character, "chest");
            GameObject chest = CreateWorldObject("chest");
            var fakeAttention = new FakeAttentionSource();
            context.RegisterAttentionSource(fakeAttention);

            var bridge = root.AddComponent<ConvaiAttentionDynamicContextBridge>();
            fakeAttention.SetValid(chest.transform, chest.transform.position, commitment: 1f, generationId: 1);
            ((IEmbodimentTickable)bridge).EmbodimentTick(1f / 60f);

            fakeAttention.SetInvalid();
            ((IEmbodimentTickable)bridge).EmbodimentTick(1f / 60f);

            Assert.AreEqual(0, _connectionService.SentDynamicContextUpdates.Count);

            MakeReady(character);
            character.DynamicContext.Flush();

            Assert.AreEqual(1, _connectionService.SentDynamicContextUpdates.Count);
            Assert.AreEqual(string.Empty, _connectionService.SentDynamicContextUpdates[0].CurrentAttentionObject);
        }

        private static void ConfigureActionObject(ConvaiCharacter character, string objectName)
        {
            var source = character.gameObject.AddComponent<ConvaiActionConfigSource>();
            var executor = character.gameObject.AddComponent<UnityEventActionExecutor>();
            SetPrivateField(source, "_definitions", new List<ConvaiActionDefinition>
            {
                new() { ActionName = "Move To", Executor = executor }
            });
            SetPrivateField(source, "_objects", new List<ConvaiActionObjectDefinition>
            {
                new() { Name = objectName }
            });
        }

        private GameObject CreateWorldObject(string objectName)
        {
            var go = new GameObject(objectName);
            _createdObjects.Add(go);
            go.SetActive(false);

            var metadata = go.AddComponent<ConvaiObjectMetadata>();
            metadata.ObjectName = objectName;
            metadata.ObjectDescription = $"{objectName} description";

            go.SetActive(true);
            if (!metadata.IsRegistered)
                ConvaiMetadataRegistry.RegisterMetadata(metadata);

            return go;
        }

        private void MakeReady(ConvaiCharacter character)
        {
            _connectionService.RaiseConnected();
            _eventHub.Publish(CharacterReady.Create(character.CharacterId, $"participant-{character.CharacterId}"));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private sealed class ImmediateScheduler : IUnityScheduler
        {
            public void ScheduleOnMainThread(Action action) => action?.Invoke();
            public void ScheduleOnBackground(Action action) => action?.Invoke();
            public bool IsMainThread() => true;
        }

        private sealed class TestLogger : ILogger
        {
            public void Log(LogLevel level, string message, LogCategory category = LogCategory.SDK) { }

            public void Log(LogLevel level, string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            {
            }

            public void Debug(string message, LogCategory category = LogCategory.SDK) { }

            public void Debug(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            {
            }

            public void Info(string message, LogCategory category = LogCategory.SDK) { }

            public void Info(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            {
            }

            public void Warning(string message, LogCategory category = LogCategory.SDK) { }

            public void Warning(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            {
            }

            public void Error(string message, LogCategory category = LogCategory.SDK) { }

            public void Error(string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            {
            }

            public void Error(Exception exception, string message = null, LogCategory category = LogCategory.SDK) { }

            public void Error(Exception exception, string message, IReadOnlyDictionary<string, object> context,
                LogCategory category = LogCategory.SDK)
            {
            }

            public bool IsEnabled(LogLevel level, LogCategory category) => true;
        }
    }
}
