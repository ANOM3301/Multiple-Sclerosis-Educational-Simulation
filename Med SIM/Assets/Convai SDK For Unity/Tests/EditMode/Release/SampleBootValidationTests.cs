using System;
using System.Reflection;
using Convai.Modules.LipSync;
using Convai.Runtime.Actions;
using Convai.Runtime.Adapters.Networking;
using Convai.Runtime.Components;
using Convai.Runtime.Presentation.Views.Notifications;
using Convai.Shared.Actions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.Release
{
    [Category("Release")]
    public sealed class SampleBootValidationTests
    {
        private const string BasicSampleScenePath =
            "Packages/com.convai.convai-sdk-for-unity/Samples/BasicSample/Scenes/Basic Sample.unity";

        private const string LipSyncSampleScenePath =
            "Packages/com.convai.convai-sdk-for-unity/Samples/LipSyncSample/Scenes/LipSync Sample.unity";

        [TearDown]
        public void TearDown()
        {
            // Ensure subsequent tests do not inherit the sample scene state.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void BasicSample_Loads_AndContainsCoreRuntimeObjects()
        {
            Scene scene = EditorSceneManager.OpenScene(BasicSampleScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "Expected Basic sample scene to load.");

            ConvaiManager manager = Object.FindAnyObjectByType<ConvaiManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(manager,
                "Basic sample should contain ConvaiManager.");
            Assert.IsNotNull(ResolveOrProvisionRoomManager(manager),
                "Basic sample should expose a ConvaiRoomManager path (serialized or manager-provisioned).");
            Assert.IsNotNull(Object.FindAnyObjectByType<ConvaiPlayer>(FindObjectsInactive.Include),
                "Basic sample should contain ConvaiPlayer.");

            ConvaiCharacter[] characters = Object.FindObjectsByType<ConvaiCharacter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.GreaterOrEqual(characters.Length, 1, "Basic sample should contain at least one ConvaiCharacter.");
            AssertBasicSampleActionSetup(characters[0]);

            Assert.IsNotNull(Object.FindAnyObjectByType<NotificationHandler>(FindObjectsInactive.Include),
                "Basic sample should include the NotificationSystem prefab instance.");

            AssertNoEditorOnlyBehavioursInScene();
        }

        [Test]
        public void LipSyncSample_Loads_AndContainsLipSyncComponent()
        {
            Scene scene = EditorSceneManager.OpenScene(LipSyncSampleScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "Expected LipSync sample scene to load.");

            ConvaiManager manager = Object.FindAnyObjectByType<ConvaiManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(manager,
                "LipSync sample should contain ConvaiManager.");
            Assert.IsNotNull(ResolveOrProvisionRoomManager(manager),
                "LipSync sample should expose a ConvaiRoomManager path (serialized or manager-provisioned).");

            ConvaiCharacter[] characters = Object.FindObjectsByType<ConvaiCharacter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.GreaterOrEqual(characters.Length, 1, "LipSync sample should contain at least one ConvaiCharacter.");

            Assert.IsNotNull(Object.FindAnyObjectByType<ConvaiLipSyncComponent>(FindObjectsInactive.Include),
                "LipSync sample should include at least one ConvaiLipSyncComponent.");

            AssertNoEditorOnlyBehavioursInScene();
        }

        private static void AssertNoEditorOnlyBehavioursInScene()
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null) continue;

                Type type = behaviour.GetType();
                string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
                if (assemblyName.EndsWith(".Editor", StringComparison.Ordinal))
                {
                    Assert.Fail(
                        $"Scene contains editor-only behaviour '{type.FullName}' from assembly '{assemblyName}'.");
                }

                string ns = type.Namespace ?? string.Empty;
                if (ns.Contains(".Editor", StringComparison.Ordinal))
                {
                    Assert.Fail($"Scene contains editor-only behaviour '{type.FullName}' (namespace '{ns}').");
                }
            }
        }

        private static void AssertBasicSampleActionSetup(ConvaiCharacter character)
        {
            Assert.IsNotNull(character, "Basic sample action setup needs a ConvaiCharacter.");

            ConvaiActionConfigSource source = character.GetComponent<ConvaiActionConfigSource>();
            Assert.IsNotNull(source, "Basic sample should include ConvaiActionConfigSource.");

            ConvaiActionDispatcher dispatcher = character.GetComponent<ConvaiActionDispatcher>();
            Assert.IsNotNull(dispatcher, "Basic sample should include ConvaiActionDispatcher.");

            Assert.IsNotNull(character.GetComponent<ConvaiActionDebugProbe>(),
                "Basic sample should include ConvaiActionDebugProbe.");
            Assert.IsNotNull(FindComponent(character, "Convai.Sample.Behaviors.TransformMoveToActionExecutor"),
                "Basic sample should include TransformMoveToActionExecutor.");
            Assert.IsNotNull(character.GetComponent<LookAtTargetActionExecutor>(),
                "Basic sample should include LookAtTargetActionExecutor.");

            AssertActionDefinition(source, "Move To", "Convai.Sample.Behaviors.TransformMoveToActionExecutor");
            AssertActionDefinition(source, "Look At", typeof(LookAtTargetActionExecutor));
            AssertNoActionDefinition(source, "Wait");

            ConvaiActionConfig config = source.BuildActionConfig();
            Assert.IsNotNull(config, "Basic sample should build a non-empty action_config.");
            Assert.That(config.Actions, Is.EquivalentTo(new[] { "Move To", "Look At" }));
            Assert.That(config.CurrentAttentionObject, Is.EqualTo("blue_cube"));

            AssertActionObject(config, "blue_cube");
            AssertActionObject(config, "red_cube");
        }

        private static void AssertActionDefinition(
            ConvaiActionConfigSource source,
            string actionName,
            Type executorType)
        {
            AssertActionDefinition(source, actionName, executorType.FullName);
        }

        private static void AssertActionDefinition(
            ConvaiActionConfigSource source,
            string actionName,
            string executorTypeName)
        {
            foreach (ConvaiActionDefinition definition in source.Definitions)
            {
                if (!string.Equals(definition?.ActionName, actionName, StringComparison.Ordinal))
                    continue;

                Assert.That(definition.TargetRequirement, Is.EqualTo(ConvaiActionTargetRequirement.Object));
                Assert.IsNotNull(definition.Executor, $"{actionName} should have an executor.");
                Assert.That(definition.Executor.GetType().FullName, Is.EqualTo(executorTypeName));
                return;
            }

            Assert.Fail($"Missing Basic sample action definition '{actionName}'.");
        }

        private static void AssertNoActionDefinition(ConvaiActionConfigSource source, string prefix)
        {
            foreach (ConvaiActionDefinition definition in source.Definitions)
            {
                if (definition?.ActionName?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                    Assert.Fail($"Basic sample should not expose broken action '{definition.ActionName}'.");
            }
        }

        private static void AssertActionObject(ConvaiActionConfig config, string objectName)
        {
            foreach (ConvaiActionObjectDefinition actionObject in config.Objects)
            {
                if (!string.Equals(actionObject?.Name, objectName, StringComparison.Ordinal))
                    continue;

                Assert.IsNotNull(actionObject.GameObjectReference, $"{objectName} should reference a scene object.");
                return;
            }

            Assert.Fail($"Missing Basic sample action object '{objectName}'.");
        }

        private static MonoBehaviour FindComponent(ConvaiCharacter character, string typeName)
        {
            MonoBehaviour[] behaviours = character.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().FullName == typeName)
                    return behaviour;
            }

            return null;
        }

        private static ConvaiRoomManager ResolveOrProvisionRoomManager(ConvaiManager manager)
        {
            if (manager == null) return null;

            ConvaiRoomManager roomManager = Object.FindAnyObjectByType<ConvaiRoomManager>(FindObjectsInactive.Include);
            if (roomManager != null) return roomManager;

            MethodInfo ensureRoomManagerReference = typeof(ConvaiManager).GetMethod(
                "EnsureRoomManagerReference",
                BindingFlags.Instance | BindingFlags.NonPublic);
            ensureRoomManagerReference?.Invoke(manager, null);

            return manager.GetComponent<ConvaiRoomManager>()
                   ?? Object.FindAnyObjectByType<ConvaiRoomManager>(FindObjectsInactive.Include);
        }
    }
}
