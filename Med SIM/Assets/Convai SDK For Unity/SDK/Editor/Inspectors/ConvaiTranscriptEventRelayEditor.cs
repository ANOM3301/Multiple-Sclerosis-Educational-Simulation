using Convai.Runtime.Presentation.Events;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    [CustomEditor(typeof(ConvaiTranscriptEventRelay))]
    public class ConvaiTranscriptEventRelayEditor : UnityEditor.Editor
    {
        private SerializedProperty _autoResolveManagerProp;
        private SerializedProperty _characterIdFilterProp;
        private SerializedProperty _finalOnlyProp;
        private SerializedProperty _ignoreInterimUpdatesProp;
        private SerializedProperty _managerProp;
        private SerializedProperty _onCharacterTranscriptReceivedProp;
        private SerializedProperty _onFinalCharacterTranscriptReceivedProp;
        private SerializedProperty _onFinalPlayerTranscriptReceivedProp;
        private SerializedProperty _onPlayerTranscriptReceivedProp;
        private SerializedProperty _onTranscriptReceivedProp;

        private void OnEnable()
        {
            _managerProp = serializedObject.FindProperty("_manager");
            _autoResolveManagerProp = serializedObject.FindProperty("_autoResolveManager");
            _finalOnlyProp = serializedObject.FindProperty("_finalOnly");
            _ignoreInterimUpdatesProp = serializedObject.FindProperty("_ignoreInterimUpdates");
            _characterIdFilterProp = serializedObject.FindProperty("_characterIdFilter");
            _onTranscriptReceivedProp = serializedObject.FindProperty("_onTranscriptReceived");
            _onCharacterTranscriptReceivedProp = serializedObject.FindProperty("_onCharacterTranscriptReceived");
            _onPlayerTranscriptReceivedProp = serializedObject.FindProperty("_onPlayerTranscriptReceived");
            _onFinalCharacterTranscriptReceivedProp =
                serializedObject.FindProperty("_onFinalCharacterTranscriptReceived");
            _onFinalPlayerTranscriptReceivedProp =
                serializedObject.FindProperty("_onFinalPlayerTranscriptReceived");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Use this relay for immediate transcript reactions such as keyword triggers or lightweight gameplay hooks. For chat/history use ConvaiManager.Transcripts.Subscribe; for live subtitles use SubscribeCaptions.",
                MessageType.Info);

            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_managerProp);
            EditorGUILayout.PropertyField(_autoResolveManagerProp);

            DrawWarning();
            DrawDivider();

            EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_finalOnlyProp);
            EditorGUILayout.PropertyField(_ignoreInterimUpdatesProp);
            EditorGUILayout.PropertyField(_characterIdFilterProp);

            EditorGUILayout.HelpBox(
                "Final Only is best for gameplay triggers. Ignore Interim Updates keeps streaming text from firing repeated reactions while the player is still talking.",
                MessageType.None);

            DrawDivider();

            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_onTranscriptReceivedProp);
            EditorGUILayout.PropertyField(_onCharacterTranscriptReceivedProp);
            EditorGUILayout.PropertyField(_onPlayerTranscriptReceivedProp);
            EditorGUILayout.PropertyField(_onFinalCharacterTranscriptReceivedProp);
            EditorGUILayout.PropertyField(_onFinalPlayerTranscriptReceivedProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWarning()
        {
            var relay = (ConvaiTranscriptEventRelay)target;
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
