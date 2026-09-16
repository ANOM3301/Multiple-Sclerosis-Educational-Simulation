using System.Collections.Generic;
using Convai.Editor.AI;
using Convai.Runtime.Presentation.Views;
using Convai.Runtime.Presentation.Views.Transcript;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.AI
{
    public sealed class ConvaiTranscriptUiDiagnosticTests
    {
        private readonly List<GameObject> _createdObjects = new();
        private Scene _previousScene;
        private Scene _testScene;
        private bool _singleSceneMode;

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
            foreach (GameObject createdObject in _createdObjects)
                if (createdObject != null)
                    Object.DestroyImmediate(createdObject);
            _createdObjects.Clear();

            if (!_singleSceneMode && _previousScene.IsValid() && _previousScene.isLoaded)
                SceneManager.SetActiveScene(_previousScene);
            if (_testScene.IsValid() && _testScene.isLoaded)
                EditorSceneManager.CloseScene(_testScene, true);
            if (_singleSceneMode)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _singleSceneMode = false;
        }

        [Test]
        public void CountShippedTranscriptUis_CountsActiveAndInactiveTypedViewsOnly()
        {
            GameObject transcriptDisplayObject = CreateObject("Transcript Display");
            transcriptDisplayObject.SetActive(false);
            transcriptDisplayObject.AddComponent<ConvaiTranscriptDisplay>();

            GameObject chatTranscriptObject = CreateObject("Chat Transcript");
            chatTranscriptObject.AddComponent<ChatTranscriptUI>();

            GameObject plainBehaviourObject = CreateObject("Plain Behaviour");
            plainBehaviourObject.AddComponent<TranscriptUiPlainBehaviour>();

            Assert.That(ConvaiMcpTools.CountShippedTranscriptUis(_testScene), Is.EqualTo(2));
        }

        private GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }
    }

    public sealed class TranscriptUiPlainBehaviour : MonoBehaviour
    {
    }
}
