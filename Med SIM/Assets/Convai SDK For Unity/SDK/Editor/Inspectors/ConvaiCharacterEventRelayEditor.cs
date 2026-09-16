using Convai.Runtime.Presentation.Events;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    [CustomEditor(typeof(ConvaiCharacterEventRelay))]
    public class ConvaiCharacterEventRelayEditor : UnityEditor.Editor
    {
        private SerializedProperty _autoResolveCharacterProp;
        private SerializedProperty _characterProp;
        private SerializedProperty _onCharacterReadyProp;
        private SerializedProperty _onEmotionChangedProp;
        private SerializedProperty _onSpeechStartedProp;
        private SerializedProperty _onSpeechStoppedProp;
        private SerializedProperty _onTranscriptReceivedProp;
        private SerializedProperty _onTurnCompletedProp;

        private void OnEnable()
        {
            _characterProp = serializedObject.FindProperty("_character");
            _autoResolveCharacterProp = serializedObject.FindProperty("_autoResolveCharacter");
            _onTranscriptReceivedProp = serializedObject.FindProperty("_onTranscriptReceived");
            _onSpeechStartedProp = serializedObject.FindProperty("_onSpeechStarted");
            _onSpeechStoppedProp = serializedObject.FindProperty("_onSpeechStopped");
            _onTurnCompletedProp = serializedObject.FindProperty("_onTurnCompleted");
            _onCharacterReadyProp = serializedObject.FindProperty("_onCharacterReady");
            _onEmotionChangedProp = serializedObject.FindProperty("_onEmotionChanged");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Use this relay when one character should drive animation, VFX, UI, or other local scene reactions. For room-wide orchestration or typed domain events, prefer ConvaiManager.Events.",
                MessageType.Info);

            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_characterProp);
            EditorGUILayout.PropertyField(_autoResolveCharacterProp);

            DrawWarning();
            DrawDivider();

            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_onTranscriptReceivedProp);
            EditorGUILayout.PropertyField(_onSpeechStartedProp);
            EditorGUILayout.PropertyField(_onSpeechStoppedProp);
            EditorGUILayout.PropertyField(_onTurnCompletedProp);
            EditorGUILayout.PropertyField(_onCharacterReadyProp);
            EditorGUILayout.PropertyField(_onEmotionChangedProp);

            EditorGUILayout.HelpBox(
                "These callbacks come from the local ConvaiCharacter component. They are the right fit for character-local reactions, not transcript history or room-wide event routing.",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWarning()
        {
            var relay = (ConvaiCharacterEventRelay)target;
            string warning = relay.GetConfigurationWarning();
            if (!string.IsNullOrWhiteSpace(warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        private static void DrawDivider()
        {
            EditorGUILayout.Space(4);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            EditorGUILayout.Space(4);
        }
    }
}
