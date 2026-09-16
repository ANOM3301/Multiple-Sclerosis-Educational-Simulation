using System;
using System.Linq;
using Convai.Editor.AI;
using Convai.Runtime;
using Convai.Runtime.Adapters.Networking;
using Convai.Runtime.Actions;
using Convai.Runtime.Components;
using Convai.Runtime.Vision.Context;
using Convai.Shared.Types;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.AI
{
    public sealed class ConvaiMcpAuthoringTests
    {
        private const string CharacterId = "69090ea4-0c5e-11f1-9d63-42010a7be02c";
        private Scene _previousScene;
        private Scene _testScene;
        private bool _singleSceneMode;
        private string _temporaryTestScenePath;

        [SetUp]
        public void SetUp()
        {
            _previousScene = SceneManager.GetActiveScene();
            _testScene = ConvaiMcpSceneFixture.CreateTestScene(out _singleSceneMode);
            if (!_singleSceneMode) SceneManager.SetActiveScene(_testScene);
        }

        [TearDown]
        public void TearDown()
        {
            if (!_singleSceneMode && _previousScene.IsValid() && _previousScene.isLoaded)
                SceneManager.SetActiveScene(_previousScene);
            if (_testScene.IsValid() && _testScene.isLoaded)
                EditorSceneManager.CloseScene(_testScene, true);
            if (!string.IsNullOrEmpty(_temporaryTestScenePath))
            {
                AssetDatabase.DeleteAsset(_temporaryTestScenePath);
                _temporaryTestScenePath = null;
            }
            if (_singleSceneMode)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _singleSceneMode = false;
        }

        [Test]
        public void SetupConversationScene_DryRunDoesNotMutateEmptyScene()
        {
            JObject response = Json(ConvaiMcpTools.SetupConversationScene(
                new ConvaiSetupConversationSceneRequest
                {
                    CharacterId = CharacterId,
                    DryRun = true
                }));

            Assert.That(response.Value<bool>("success"), Is.True);
            Assert.That(response["data"]?.Value<bool>("dryRun"), Is.True);
            Assert.That(_testScene.GetRootGameObjects(), Is.Empty);
            Assert.That(response["data"]?["changes"]?.Values<string>(),
                Does.Contain("Create standalone Convai Player with ConvaiPlayer."));
        }

        [Test]
        public void SetupConversationScene_ApplyCreatesRunnableFoundationAndIsIdempotent()
        {
            var request = new ConvaiSetupConversationSceneRequest
            {
                CharacterId = CharacterId,
                DryRun = false
            };

            JObject first = Json(ConvaiMcpTools.SetupConversationScene(request));
            JObject second = Json(ConvaiMcpTools.SetupConversationScene(request));

            Assert.That(first.Value<bool>("success"), Is.True);
            Assert.That(_testScene.GetRootGameObjects().Count(root => root.name == "[Convai Manager]"), Is.EqualTo(1));
            Assert.That(_testScene.GetRootGameObjects().Count(root => root.name == "Convai Player"), Is.EqualTo(1));
            Assert.That(_testScene.GetRootGameObjects().Count(root => root.name == "Convai Character"), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<ConvaiManager>(FindObjectsInactive.Include).Count(item => item.gameObject.scene == _testScene), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<ConvaiRoomManager>(FindObjectsInactive.Include).Count(item => item.gameObject.scene == _testScene), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<ConvaiPlayer>(FindObjectsInactive.Include).Count(item => item.gameObject.scene == _testScene), Is.EqualTo(1));
            ConvaiCharacter character = Object.FindObjectsByType<ConvaiCharacter>(FindObjectsInactive.Include)
                .Single(item => item.gameObject.scene == _testScene);
            Assert.That(character.GetComponent<AudioSource>(), Is.Not.Null);
            Assert.That(character.GetComponent<ConvaiAudioOutput>(), Is.Not.Null);
            Assert.That(character.CharacterId, Is.EqualTo(request.CharacterId));
            Assert.That(second["data"]?["changes"]?.Count(), Is.EqualTo(0));
        }

        [Test]
        public void ConfigureCharacter_MissingIdAuthorsComponentsButReportsIncomplete()
        {
            GameObject managerObject = new("[Convai Manager]");
            managerObject.AddComponent<ConvaiManager>();
            GameObject characterObject = new("NPC");

            JObject response = Json(ConvaiMcpTools.ConfigureCharacter(new ConvaiConfigureCharacterRequest
            {
                TargetInstanceId = Id(characterObject),
                ManagerInstanceId = Id(managerObject),
                CharacterName = "NPC",
                DryRun = false
            }));

            Assert.That(response.Value<bool>("success"), Is.True);
            Assert.That(response["data"]?.Value<bool>("complete"), Is.False);
            Assert.That(response["data"]?["requiredInputs"]?.Values<string>(), Does.Contain("characterId"));
            Assert.That(characterObject.GetComponent<ConvaiCharacter>(), Is.Not.Null);
            Assert.That(characterObject.GetComponent<ConvaiAudioOutput>(), Is.Not.Null);
        }

        [Test]
        public void ConfigureCharacter_InvalidIdFailsWithoutMutation()
        {
            GameObject target = new("NPC");

            JObject response = Json(ConvaiMcpTools.ConfigureCharacter(new ConvaiConfigureCharacterRequest
            {
                TargetInstanceId = Id(target),
                CharacterId = "not-a-character-id",
                DryRun = false
            }));

            Assert.That(response.Value<bool>("success"), Is.False);
            Assert.That(target.GetComponent<ConvaiCharacter>(), Is.Null);
        }

        [Test]
        public void ConfigureActions_DryRunApplyAndRepeatStayConsistent()
        {
            GameObject target = new("NPC");
            target.AddComponent<ConvaiCharacter>();
            var request = new ConvaiConfigureActionsRequest
            {
                CharacterInstanceId = Id(target),
                Definitions = new[]
                {
                    new ConvaiActionDefinitionInput { Name = "Wave", Description = "Wave to the player" }
                },
                DryRun = true
            };

            JObject preview = Json(ConvaiMcpTools.ConfigureActions(request));
            Assert.That(preview.Value<bool>("success"), Is.True);
            Assert.That(preview["data"]?.Value<bool>("complete"), Is.False);
            Assert.That(preview["data"]?["blockedSteps"]?.Values<string>().Single(), Does.Contain("Wave"));
            Assert.That(target.GetComponent<ConvaiActionConfigSource>(), Is.Null);

            request.DryRun = false;
            JObject applied = Json(ConvaiMcpTools.ConfigureActions(request));
            JObject repeated = Json(ConvaiMcpTools.ConfigureActions(request));

            Assert.That(applied.Value<bool>("success"), Is.True);
            Assert.That(target.GetComponent<ConvaiActionConfigSource>(), Is.Not.Null);
            Assert.That(target.GetComponent<ConvaiActionDispatcher>(), Is.Not.Null);
            Assert.That(target.GetComponents<UnityEventActionExecutor>(), Has.Length.EqualTo(1));
            Assert.That(repeated["data"]?["changes"]?.Count(), Is.EqualTo(0));
            Assert.That(target.GetComponents<UnityEventActionExecutor>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void ConfigureActions_InvalidExecutorFailsBeforeMutation()
        {
            GameObject target = new("NPC");
            ConvaiCharacter character = target.AddComponent<ConvaiCharacter>();
            var request = new ConvaiConfigureActionsRequest
            {
                CharacterInstanceId = Id(target),
                Definitions = new[]
                {
                    new ConvaiActionDefinitionInput
                    {
                        Name = "Wave",
                        ExecutorInstanceId = Id(character)
                    }
                },
                DryRun = false
            };

            JObject response = Json(ConvaiMcpTools.ConfigureActions(request));

            Assert.That(response.Value<bool>("success"), Is.False);
            Assert.That(target.GetComponent<ConvaiActionConfigSource>(), Is.Null);
            Assert.That(target.GetComponent<ConvaiActionDispatcher>(), Is.Null);
            Assert.That(target.GetComponent<UnityEventActionExecutor>(), Is.Null);
        }

        [Test]
        public void ConfigureTranscripts_ApplyIsIdempotent()
        {
            GameObject target = new("[Convai Manager]");
            target.AddComponent<ConvaiManager>();
            var request = new ConvaiConfigureTranscriptsRequest
            {
                ManagerInstanceId = Id(target),
                HostInstanceId = Id(target),
                Mode = ConvaiTranscriptToolMode.EventRelay,
                DryRun = false
            };

            JObject applied = Json(ConvaiMcpTools.ConfigureTranscripts(request));
            JObject repeated = Json(ConvaiMcpTools.ConfigureTranscripts(request));

            Assert.That(applied.Value<bool>("success"), Is.True);
            Assert.That(target.GetComponents<Convai.Runtime.Presentation.Events.ConvaiTranscriptEventRelay>(), Has.Length.EqualTo(1));
            Assert.That(repeated["data"]?["changes"]?.Count(), Is.EqualTo(0));
        }

        [Test]
        public void ConfigureTranscripts_EveryAdvertisedModeProducesDryRunChanges()
        {
            GameObject target = new("[Convai Manager]");
            target.AddComponent<ConvaiManager>();

            foreach (ConvaiTranscriptToolMode mode in Enum.GetValues(typeof(ConvaiTranscriptToolMode)))
            {
                JObject response = Json(ConvaiMcpTools.ConfigureTranscripts(
                    new ConvaiConfigureTranscriptsRequest
                    {
                        ManagerInstanceId = Id(target),
                        HostInstanceId = Id(target),
                        Mode = mode,
                        DryRun = true
                    }));

                Assert.That(response.Value<bool>("success"), Is.True, $"{mode} should have a handler.");
                Assert.That(response["data"]?.Value<bool>("dryRun"), Is.True, $"{mode} should produce a dry-run plan.");
                Assert.That(response["data"]?["changes"]?.Count(), Is.GreaterThan(0),
                    $"{mode} should produce at least one planned change.");
            }
        }

        [Test]
        public void ConfigureRoom_DryRunApplyIdempotencyUndoAndNoSave()
        {
            GameObject target = new("Room");
            var request = new ConvaiConfigureRoomRequest
            {
                TargetInstanceId = Id(target),
                PushToTalkKey = "T",
                DryRun = true
            };

            JObject preview = Json(ConvaiMcpTools.ConfigureRoom(request));
            Assert.That(preview.Value<bool>("success"), Is.True);
            Assert.That(target.GetComponent<ConvaiManager>(), Is.Null);
            Assert.That(target.GetComponent<ConvaiRoomManager>(), Is.Null);

            request.DryRun = false;
            JObject applied = Json(ConvaiMcpTools.ConfigureRoom(request));
            JObject repeated = Json(ConvaiMcpTools.ConfigureRoom(request));
            ConvaiRoomManager room = target.GetComponent<ConvaiRoomManager>();

            Assert.That(applied.Value<bool>("success"), Is.True);
            Assert.That(target.GetComponent<ConvaiManager>(), Is.Not.Null);
            Assert.That(room, Is.Not.Null);
            Assert.That(room.PushToTalkKey, Is.EqualTo(KeyCode.T));
            Assert.That(repeated["data"]?["changes"]?.Count(), Is.EqualTo(0));
            Assert.That(_testScene.path, Is.Empty);
            Assert.That(applied["data"]?.Value<bool>("sceneSaved"), Is.False);

            Undo.PerformUndo();
            Assert.That(target.GetComponent<ConvaiManager>(), Is.Null);
            Assert.That(target.GetComponent<ConvaiRoomManager>(), Is.Null);
        }

        [Test]
        public void ConfigurePlayer_DryRunApplyIdempotencyUndoAndLeavesCameraAlone()
        {
            GameObject managerObject = new("[Convai Manager]");
            ConvaiManager manager = managerObject.AddComponent<ConvaiManager>();
            GameObject target = new("Player Target");
            GameObject cameraObject = new("Main Camera");
            cameraObject.AddComponent<Camera>();
            var request = new ConvaiConfigurePlayerRequest
            {
                TargetInstanceId = Id(target),
                ManagerInstanceId = Id(managerObject),
                PlayerName = "Player",
                DryRun = true
            };

            Assert.That(Json(ConvaiMcpTools.ConfigurePlayer(request)).Value<bool>("success"), Is.True);
            Assert.That(target.GetComponent<ConvaiPlayer>(), Is.Null);

            request.DryRun = false;
            JObject applied = Json(ConvaiMcpTools.ConfigurePlayer(request));
            JObject repeated = Json(ConvaiMcpTools.ConfigurePlayer(request));
            ConvaiPlayer player = target.GetComponent<ConvaiPlayer>();

            Assert.That(applied.Value<bool>("success"), Is.True);
            Assert.That(player, Is.Not.Null);
            Assert.That(ReadReference(manager, "_explicitPlayer"), Is.EqualTo(player));
            Assert.That(cameraObject.GetComponent<ConvaiPlayer>(), Is.Null);
            Assert.That(repeated["data"]?["changes"]?.Count(), Is.EqualTo(0));

            Undo.PerformUndo();
            Assert.That(target.GetComponent<ConvaiPlayer>(), Is.Null);
            Assert.That(ReadReference(manager, "_explicitPlayer"), Is.Null);
        }

        [Test]
        public void ConfigureCharacter_DryRunApplyIdempotencyUndoAndOwnership()
        {
            GameObject managerObject = new("[Convai Manager]");
            ConvaiManager manager = managerObject.AddComponent<ConvaiManager>();
            GameObject target = new("NPC");
            var request = new ConvaiConfigureCharacterRequest
            {
                TargetInstanceId = Id(target),
                ManagerInstanceId = Id(managerObject),
                CharacterId = CharacterId,
                CharacterName = "NPC",
                DryRun = true
            };

            Assert.That(Json(ConvaiMcpTools.ConfigureCharacter(request)).Value<bool>("success"), Is.True);
            Assert.That(target.GetComponent<ConvaiCharacter>(), Is.Null);

            request.DryRun = false;
            JObject applied = Json(ConvaiMcpTools.ConfigureCharacter(request));
            JObject repeated = Json(ConvaiMcpTools.ConfigureCharacter(request));
            ConvaiCharacter character = target.GetComponent<ConvaiCharacter>();

            Assert.That(applied.Value<bool>("success"), Is.True);
            Assert.That(character, Is.Not.Null);
            Assert.That(target.GetComponent<AudioSource>(), Is.Not.Null);
            Assert.That(target.GetComponent<ConvaiAudioOutput>(), Is.Not.Null);
            Assert.That(ReadReferences(manager, "_explicitCharacters"), Does.Contain(character));
            Assert.That(ReadReference(manager, "_explicitConversationTarget"), Is.EqualTo(character));
            Assert.That(repeated["data"]?["changes"]?.Count(), Is.EqualTo(0));

            Undo.PerformUndo();
            Assert.That(target.GetComponent<ConvaiCharacter>(), Is.Null);
            Assert.That(target.GetComponent<ConvaiAudioOutput>(), Is.Null);
            Assert.That(ReadReferences(manager, "_explicitCharacters"), Is.Empty);
        }

        [Test]
        public void MutatorRejectsTargetFromNonActiveSceneBeforeMutation()
        {
            GameObject target = new("Wrong Scene Target");
            Scene wrongScene;
            if (_previousScene.IsValid() && _previousScene.isLoaded)
            {
                wrongScene = _previousScene;
            }
            else
            {
                _temporaryTestScenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/__ConvaiMcpActiveTest.unity");
                Assert.That(EditorSceneManager.SaveScene(_testScene, _temporaryTestScenePath), Is.True);
                wrongScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }
            bool createdWrongScene = wrongScene != _previousScene;
            try
            {
                SceneManager.MoveGameObjectToScene(target, wrongScene);
                SceneManager.SetActiveScene(_testScene);

                JObject response = Json(ConvaiMcpTools.ConfigureRoom(new ConvaiConfigureRoomRequest
                {
                    TargetInstanceId = Id(target),
                    DryRun = false
                }));

                Assert.That(response.Value<bool>("success"), Is.False);
                Assert.That(response.Value<string>("message"), Does.Contain("active scene"));
                Assert.That(target.GetComponent<ConvaiManager>(), Is.Null);
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
                if (createdWrongScene && wrongScene.IsValid() && wrongScene.isLoaded)
                    EditorSceneManager.CloseScene(wrongScene, true);
            }
        }

        [Test]
        public void ExistingProfilesAreAssignedAndInvalidProfileIdentityFailsBeforeMutation()
        {
            string roomPath = AssetDatabase.GenerateUniqueAssetPath("Assets/ConvaiMcpRoomProfileTest.asset");
            string characterPath = AssetDatabase.GenerateUniqueAssetPath("Assets/ConvaiMcpCharacterProfileTest.asset");
            ConvaiRoomManagerProfile roomProfile = ScriptableObject.CreateInstance<ConvaiRoomManagerProfile>();
            ConvaiCharacterProfile characterProfile = ScriptableObject.CreateInstance<ConvaiCharacterProfile>();
            AssetDatabase.CreateAsset(roomProfile, roomPath);
            AssetDatabase.CreateAsset(characterProfile, characterPath);
            try
            {
                var characterSerialized = new SerializedObject(characterProfile);
                characterSerialized.FindProperty("_characterId").stringValue = CharacterId;
                characterSerialized.FindProperty("_characterName").stringValue = "Profile NPC";
                characterSerialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject managerObject = new("[Convai Manager]");
                managerObject.AddComponent<ConvaiManager>();
                GameObject characterObject = new("NPC");
                JObject roomResponse = Json(ConvaiMcpTools.ConfigureRoom(new ConvaiConfigureRoomRequest
                {
                    TargetInstanceId = Id(managerObject),
                    ConfigurationMode = ConvaiToolConfigurationMode.ExistingProfile,
                    ProfileAssetPath = roomPath,
                    DryRun = false
                }));
                JObject characterResponse = Json(ConvaiMcpTools.ConfigureCharacter(
                    new ConvaiConfigureCharacterRequest
                    {
                        TargetInstanceId = Id(characterObject),
                        ManagerInstanceId = Id(managerObject),
                        ConfigurationMode = ConvaiToolConfigurationMode.ExistingProfile,
                        ProfileAssetPath = characterPath,
                        DryRun = false
                    }));

                Assert.That(roomResponse.Value<bool>("success"), Is.True);
                Assert.That(managerObject.GetComponent<ConvaiRoomManager>().RoomConfigAsset, Is.EqualTo(roomProfile));
                Assert.That(characterResponse.Value<bool>("success"), Is.True);
                Assert.That(characterObject.GetComponent<ConvaiCharacter>().CharacterConfigAsset,
                    Is.EqualTo(characterProfile));

                characterSerialized.Update();
                characterSerialized.FindProperty("_characterId").stringValue = "invalid";
                characterSerialized.ApplyModifiedPropertiesWithoutUndo();
                GameObject invalidTarget = new("Invalid Profile NPC");
                JObject invalidResponse = Json(ConvaiMcpTools.ConfigureCharacter(
                    new ConvaiConfigureCharacterRequest
                    {
                        TargetInstanceId = Id(invalidTarget),
                        ManagerInstanceId = Id(managerObject),
                        ConfigurationMode = ConvaiToolConfigurationMode.ExistingProfile,
                        ProfileAssetPath = characterPath,
                        DryRun = false
                    }));
                Assert.That(invalidResponse.Value<bool>("success"), Is.False);
                Assert.That(invalidTarget.GetComponent<ConvaiCharacter>(), Is.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(roomPath);
                AssetDatabase.DeleteAsset(characterPath);
            }
        }

        [Test]
        public void DiagnoseConversation_ReportsOwnershipAndDuplicateIdentityCodes()
        {
            GameObject managerObject = new("[Convai Manager]");
            managerObject.AddComponent<ConvaiManager>();
            managerObject.AddComponent<ConvaiRoomManager>();
            new GameObject("Player").AddComponent<ConvaiPlayer>();
            CreateConfiguredCharacter("NPC A", CharacterId);
            CreateConfiguredCharacter("NPC B", CharacterId);

            JObject response = Json(ConvaiMcpTools.DiagnoseConversation(
                new ConvaiDiagnoseConversationRequest()));
            string[] codes = IssueCodes(response);

            Assert.That(codes, Does.Contain("PLAYER_OWNERSHIP_MISSING"));
            Assert.That(codes, Does.Contain("CHARACTER_OWNERSHIP_MISSING"));
            Assert.That(codes, Does.Contain("CONVERSATION_TARGET_MISSING"));
            Assert.That(codes, Does.Contain("CHARACTER_ID_DUPLICATE"));
        }

        [Test]
        public void DiagnoseConversation_ReportsIncompleteVideoPipeline()
        {
            GameObject managerObject = new("[Convai Manager]");
            ConvaiManager manager = managerObject.AddComponent<ConvaiManager>();
            ConvaiRoomManager room = managerObject.AddComponent<ConvaiRoomManager>();
            GameObject playerObject = new("Player");
            ConvaiPlayer player = playerObject.AddComponent<ConvaiPlayer>();
            ConvaiCharacter character = CreateConfiguredCharacter("NPC", CharacterId);
            Bind(manager, player, character);
            var serialized = new SerializedObject(room);
            serialized.FindProperty("_connectionType").intValue = (int)ConvaiConnectionType.Video;
            serialized.FindProperty("_visionContextMode").intValue = (int)ConvaiVisionContextMode.Auto;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            JObject response = Json(ConvaiMcpTools.DiagnoseConversation(
                new ConvaiDiagnoseConversationRequest()));

            Assert.That(IssueCodes(response), Does.Contain("VIDEO_PIPELINE_INCOMPLETE"));
        }

        [Test]
        public void DiagnoseConversation_ReturnsStableMissingFoundationCodes()
        {
            JObject response = Json(ConvaiMcpTools.DiagnoseConversation(
                new ConvaiDiagnoseConversationRequest()));
            string[] codes = response["data"]?["issues"]?
                .Select(issue => issue.Value<string>("code"))
                .ToArray();

            Assert.That(response.Value<bool>("success"), Is.True);
            Assert.That(codes, Does.Contain("MANAGER_MISSING"));
            Assert.That(codes, Does.Contain("ROOM_MISSING"));
            Assert.That(codes, Does.Contain("PLAYER_MISSING"));
            Assert.That(codes, Does.Contain("CHARACTER_MISSING"));
        }

        private static JObject Json(object value) => JObject.FromObject(value);

        private static long Id(Object value) => unchecked((long)EntityId.ToULong(value.GetEntityId()));

        private static string[] IssueCodes(JObject response) => response["data"]?["issues"]?
            .Select(issue => issue.Value<string>("code"))
            .ToArray() ?? Array.Empty<string>();

        private static Object ReadReference(ConvaiManager manager, string propertyName) =>
            new SerializedObject(manager).FindProperty(propertyName).objectReferenceValue;

        private static Object[] ReadReferences(ConvaiManager manager, string propertyName)
        {
            SerializedProperty values = new SerializedObject(manager).FindProperty(propertyName);
            return Enumerable.Range(0, values.arraySize)
                .Select(index => values.GetArrayElementAtIndex(index).objectReferenceValue)
                .ToArray();
        }

        private static ConvaiCharacter CreateConfiguredCharacter(string name, string characterId)
        {
            GameObject target = new(name);
            ConvaiCharacter character = target.AddComponent<ConvaiCharacter>();
            target.AddComponent<AudioSource>();
            target.AddComponent<ConvaiAudioOutput>();
            var serialized = new SerializedObject(character);
            serialized.FindProperty("_characterId").stringValue = characterId;
            serialized.FindProperty("_characterName").stringValue = name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return character;
        }

        private static void Bind(ConvaiManager manager, ConvaiPlayer player, ConvaiCharacter character)
        {
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("_explicitPlayer").objectReferenceValue = player;
            SerializedProperty characters = serialized.FindProperty("_explicitCharacters");
            characters.arraySize = 1;
            characters.GetArrayElementAtIndex(0).objectReferenceValue = character;
            serialized.FindProperty("_explicitConversationTarget").objectReferenceValue = character;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
