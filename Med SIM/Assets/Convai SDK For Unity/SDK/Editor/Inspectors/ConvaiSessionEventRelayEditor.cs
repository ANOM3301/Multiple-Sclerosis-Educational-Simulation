using Convai.Runtime.Presentation.Events;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    [CustomEditor(typeof(ConvaiSessionEventRelay))]
    public class ConvaiSessionEventRelayEditor : UnityEditor.Editor
    {
        private SerializedProperty _autoResolveManagerProp;
        private SerializedProperty _managerProp;
        private SerializedProperty _onConnectedProp;
        private SerializedProperty _onDisconnectedProp;
        private SerializedProperty _onReconnectedProp;
        private SerializedProperty _onReconnectingProp;
        private SerializedProperty _onSessionErrorProp;
        private SerializedProperty _onSessionStateChangedProp;
        private SerializedProperty _onUsageLimitReachedProp;

        private void OnEnable()
        {
            _managerProp = serializedObject.FindProperty("_manager");
            _autoResolveManagerProp = serializedObject.FindProperty("_autoResolveManager");
            _onConnectedProp = serializedObject.FindProperty("_onConnected");
            _onDisconnectedProp = serializedObject.FindProperty("_onDisconnected");
            _onReconnectingProp = serializedObject.FindProperty("_onReconnecting");
            _onReconnectedProp = serializedObject.FindProperty("_onReconnected");
            _onUsageLimitReachedProp = serializedObject.FindProperty("_onUsageLimitReached");
            _onSessionStateChangedProp = serializedObject.FindProperty("_onSessionStateChanged");
            _onSessionErrorProp = serializedObject.FindProperty("_onSessionError");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Use this relay when designers need no-code hooks for connection, reconnection, and runtime error flows. For typed C# integrations, use ConvaiManager.Events directly.",
                MessageType.Info);

            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_managerProp);
            EditorGUILayout.PropertyField(_autoResolveManagerProp);

            DrawWarning();
            DrawDivider();

            EditorGUILayout.LabelField("Lifecycle Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_onConnectedProp);
            EditorGUILayout.PropertyField(_onDisconnectedProp);
            EditorGUILayout.PropertyField(_onReconnectingProp);
            EditorGUILayout.PropertyField(_onReconnectedProp);
            EditorGUILayout.PropertyField(_onUsageLimitReachedProp);

            DrawDivider();

            EditorGUILayout.LabelField("Detailed Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_onSessionStateChangedProp);
            EditorGUILayout.PropertyField(_onSessionErrorProp);

            EditorGUILayout.HelpBox(
                "Use On Session State Changed when you need the full transition payload. Use On Session Error for UI, telemetry, or fallback messaging.",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWarning()
        {
            var relay = (ConvaiSessionEventRelay)target;
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
