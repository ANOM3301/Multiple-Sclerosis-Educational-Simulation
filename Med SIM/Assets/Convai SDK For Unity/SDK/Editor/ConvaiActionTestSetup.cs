using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Convai.Runtime.Actions;
using Convai.Runtime.Components;
using Convai.Shared.Types;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Convai.Editor
{
    /// <summary>
    ///     One-click scene wiring for validating action-response receipt and local execution.
    /// </summary>
    public static class ConvaiActionTestSetup
    {
        private const string MenuPath = "GameObject/Convai/Prepare Action Test Setup";
        private const string TestTargetName = "[Action Test] Cube Target";
        private const string TestObjectName = "cube";
        private const string TestActionName = "Move To";
        private const string TestObjectDescription = "Visible cube target for action-system testing.";
        private static readonly Vector3 TargetPosition = new(2f, 0.5f, 0f);
        private static readonly Vector3 TargetScale = Vector3.one;
        private static readonly Dictionary<string, Type> TypeCache = new(StringComparer.Ordinal);

        [MenuItem(MenuPath, false, 12)]
        public static void PrepareActionTestSetup() => PrepareActionTestSetupCore(showDialog: true);

        [MenuItem("Convai/Developer/Prepare Action Test Setup (No Dialog)", false, 110)]
        public static void PrepareActionTestSetupNoDialog() => PrepareActionTestSetupCore(showDialog: false);

        [MenuItem("Convai/Developer/Run Local Action Execution Test (No Dialog)", false, 111)]
        public static void RunLocalActionExecutionTestNoDialog() => RunLocalActionExecutionTestCore(showDialog: false);

        private static void PrepareActionTestSetupCore(bool showDialog)
        {
            try
            {
                ConvaiSceneSetupApi.BootstrapScene();

                ConvaiCharacter character = ResolveTargetCharacter();
                if (character == null)
                {
                    if (showDialog)
                    {
                        EditorUtility.DisplayDialog(
                            "Action Test Setup",
                            "No ConvaiCharacter was found in the active scene. Add or select a character first.",
                            "OK");
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[ConvaiActionTestSetup] No ConvaiCharacter was found in the active scene. Add or select a character first.");
                    }
                    return;
                }

                GameObject target = EnsureTargetCube(character.transform.root);
                ConvaiActionConfigSource actionSource = EnsureComponent<ConvaiActionConfigSource>(character.gameObject);
                ConvaiActionDispatcher dispatcher = EnsureComponent<ConvaiActionDispatcher>(character.gameObject);
                ConvaiActionDebugProbe probe = EnsureComponent<ConvaiActionDebugProbe>(character.gameObject);

                MonoBehaviour executor = EnsureMoveExecutor(character.gameObject);
                ConfigureActionSource(actionSource, target, executor);
                ConfigureDispatcher(dispatcher);
                ConfigureProbe(probe, character, dispatcher);

                Selection.activeGameObject = character.gameObject;
                EditorSceneManager.MarkSceneDirty(character.gameObject.scene);
                AssetDatabase.SaveAssets();

                const string successMessage =
                    "Prepared the active character for action-system testing.\n\n" +
                    "What was added:\n" +
                    "• ConvaiActionConfigSource with Move To definition bound to TransformMoveToActionExecutor\n" +
                    "• ConvaiActionDispatcher\n" +
                    "• ConvaiActionDebugProbe\n" +
                    "• TransformMoveToActionExecutor\n" +
                    "• Visible cube target in the scene\n\n" +
                    "Play the scene, connect, and ask for 'move to cube'. Watch the Console for the structured action batch and resolved dispatcher steps.";

                if (showDialog)
                    EditorUtility.DisplayDialog("Action Test Setup Ready", successMessage, "OK");

                Debug.Log($"[ConvaiActionTestSetup] {successMessage}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConvaiActionTestSetup] Failed to prepare action test setup: {ex}");
                if (showDialog)
                    EditorUtility.DisplayDialog("Action Test Setup Failed", ex.Message, "OK");
            }
        }

        private static void RunLocalActionExecutionTestCore(bool showDialog)
        {
            try
            {
                PrepareActionTestSetupCore(showDialog: false);

                ConvaiCharacter character = ResolveTargetCharacter();
                if (character == null)
                    throw new InvalidOperationException("No ConvaiCharacter found for the local action test.");

                ConvaiActionDispatcher dispatcher = character.GetComponent<ConvaiActionDispatcher>();
                if (dispatcher == null)
                    throw new InvalidOperationException("ConvaiActionDispatcher is missing from the target character.");

                SetField(dispatcher, "_character", character);
                dispatcher.EnqueueActions(new[] { new ConvaiActionCommand(TestActionName, TestObjectName) });
                EditorSceneManager.MarkSceneDirty(character.gameObject.scene);

                const string successMessage =
                    "Queued a local test batch: { name: 'Move To', target: 'cube' }. If the dispatcher and executor are wired correctly, the character should move to the cube target immediately.";

                if (showDialog)
                    EditorUtility.DisplayDialog("Local Action Execution Test", successMessage, "OK");

                Debug.Log($"[ConvaiActionTestSetup] {successMessage}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConvaiActionTestSetup] Failed to run local action execution test: {ex}");
                if (showDialog)
                    EditorUtility.DisplayDialog("Local Action Execution Test Failed", ex.Message, "OK");
            }
        }

        private static ConvaiCharacter ResolveTargetCharacter()
        {
            if (Selection.activeGameObject != null &&
                Selection.activeGameObject.TryGetComponent(out ConvaiCharacter selectedCharacter))
                return selectedCharacter;

            if (Selection.activeGameObject != null)
            {
                ConvaiCharacter selectedParent = Selection.activeGameObject.GetComponentInParent<ConvaiCharacter>();
                if (selectedParent != null)
                    return selectedParent;
            }

            return Object.FindAnyObjectByType<ConvaiCharacter>();
        }

        private static GameObject EnsureTargetCube(Transform preferredParent)
        {
            GameObject existing = GameObject.Find(TestTargetName);
            if (existing != null)
            {
                Undo.RecordObject(existing.transform, "Configure Action Test Target");
                existing.transform.SetPositionAndRotation(TargetPosition, Quaternion.identity);
                existing.transform.localScale = TargetScale;
                return existing;
            }

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(target, "Create Action Test Target");
            target.name = TestTargetName;
            target.transform.SetPositionAndRotation(TargetPosition, Quaternion.identity);
            target.transform.localScale = TargetScale;

            if (preferredParent != null)
                Undo.SetTransformParent(target.transform, preferredParent, "Parent Action Test Target");

            return target;
        }

        private static void ConfigureActionSource(
            ConvaiActionConfigSource actionSource,
            GameObject target,
            MonoBehaviour executor)
        {
            var definition = new ConvaiActionDefinition
            {
                ActionName = TestActionName,
                TargetRequirement = ConvaiActionTargetRequirement.Object,
                Executor = executor
            };
            SetField(actionSource, "_definitions", new List<ConvaiActionDefinition> { definition });
            SetField(actionSource, "_objects", CreateActionObjectList(target));
            SetField(actionSource, "_characters", CreateEmptyCharacterList());
            SetField(actionSource, "_initialAttentionObject", TestObjectName);
            EditorUtility.SetDirty(actionSource);
        }

        private static void ConfigureDispatcher(ConvaiActionDispatcher dispatcher)
        {
            SetField(dispatcher, "_batchPolicy", ConvaiActionBatchPolicy.Queue);
            EditorUtility.SetDirty(dispatcher);
        }

        private static void ConfigureProbe(
            ConvaiActionDebugProbe probe,
            ConvaiCharacter character,
            ConvaiActionDispatcher dispatcher)
        {
            SetField(probe, "_character", character);
            SetField(probe, "_dispatcher", dispatcher);
            SetField(probe, "_logToConsole", true);
            probe.ResetProbeState();
            EditorUtility.SetDirty(probe);
        }

        private static MonoBehaviour EnsureMoveExecutor(GameObject target)
        {
            Type executorType = FindType("Convai.Sample.Behaviors.TransformMoveToActionExecutor");
            if (executorType == null)
                throw new InvalidOperationException(
                    "Could not find Convai.Sample.Behaviors.TransformMoveToActionExecutor. Make sure sample scripts are imported and compiled.");

            Component executor = target.GetComponent(executorType);
            if (executor == null)
                executor = Undo.AddComponent(target, executorType);

            SetField(executor, "_moveRoot", target.transform);
            SetField(executor, "_offset", Vector3.zero);
            EditorUtility.SetDirty(executor);
            return executor as MonoBehaviour;
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(gameObject);
        }

        private static void SetField<TValue>(object target, string fieldName, TValue value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(target.GetType().FullName, fieldName);

            field.SetValue(target, value);
        }

        private static Type FindType(string fullName)
        {
            if (TypeCache.TryGetValue(fullName, out Type cachedType))
                return cachedType;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                {
                    TypeCache[fullName] = type;
                    return type;
                }
            }

            TypeCache[fullName] = null;
            return null;
        }

        private static object CreateActionObjectList(GameObject target)
        {
            Type objectDefinitionType = FindType("Convai.Shared.Actions.ConvaiActionObjectDefinition");
            if (objectDefinitionType == null)
                throw new InvalidOperationException("Could not resolve Convai.Shared.Actions.ConvaiActionObjectDefinition.");

            object entry = Activator.CreateInstance(objectDefinitionType);
            SetProperty(entry, "Name", TestObjectName);
            SetProperty(entry, "Description", TestObjectDescription);
            SetProperty(entry, "GameObjectReference", target);

            Type objectListType = typeof(List<>).MakeGenericType(objectDefinitionType);
            IList list = (IList)Activator.CreateInstance(objectListType);
            list.Add(entry);
            return list;
        }

        private static object CreateEmptyCharacterList()
        {
            Type characterDefinitionType = FindType("Convai.Shared.Actions.ConvaiActionCharacterDefinition");
            if (characterDefinitionType == null)
                throw new InvalidOperationException(
                    "Could not resolve Convai.Shared.Actions.ConvaiActionCharacterDefinition.");

            Type listType = typeof(List<>).MakeGenericType(characterDefinitionType);
            return Activator.CreateInstance(listType);
        }

        private static void SetProperty<TValue>(object target, string propertyName, TValue value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
                throw new MissingMemberException(target.GetType().FullName, propertyName);

            property.SetValue(target, value);
        }
    }
}
