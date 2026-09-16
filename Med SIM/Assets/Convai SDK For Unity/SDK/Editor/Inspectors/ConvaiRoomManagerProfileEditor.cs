using System;
using Convai.Editor.Inspectors;
using Convai.Runtime;
using Convai.Shared.Types;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    [CustomEditor(typeof(ConvaiRoomManagerProfile))]
    public class ConvaiRoomManagerProfileEditor : UnityEditor.Editor
    {
        private const string EditorStateHostId = "ConvaiRoomManagerProfileEditor";
        private const string SectionRoomControlId = "RoomControl";
        private SerializedProperty _autoMicStartDelaySecondsProp;

        private SerializedProperty _connectionTypeProp;
        private SerializedProperty _connectOnStartProp;
        private GUIStyle _headerStyle;
        private Texture2D _icon;
        private SerializedProperty _maxReconnectAttemptsProp;
        private SerializedProperty _resumePolicyProp;
        private readonly ConvaiRoomControlInspectorView _roomControlView = new(EditorStateHostId);
        private SerializedProperty _roomRejoinTtlSecondsProp;
        private SerializedProperty _serverEndpointProp;
        private bool _showRoomControl = true;
        private SerializedProperty _spawnAgentOnRejoinProp;
        private SerializedProperty _startWaitTimeoutMsProp;
        private GUIStyle _statusStyle;
        private bool _stylesInitialized;
        private SerializedProperty _turnTakingOptionsProp;
        private SerializedProperty _userVadSettingsProp;
        private SerializedProperty _videoTrackNameProp;
        private SerializedProperty _visionContextModeProp;
        private SerializedProperty _visionInputSettingsProp;
        private SerializedProperty _visionRespondModesProp;

        private void OnEnable()
        {
            _icon = ConvaiEditorSettings.Instance.ConvaiIconTexture;
            _connectionTypeProp = serializedObject.FindProperty("_connectionType");
            _videoTrackNameProp = serializedObject.FindProperty("_videoTrackName");
            _serverEndpointProp = serializedObject.FindProperty("_serverEndpoint");
            _connectOnStartProp = serializedObject.FindProperty("_connectOnStart");
            _turnTakingOptionsProp = serializedObject.FindProperty("_turnTakingOptions");
            _userVadSettingsProp = serializedObject.FindProperty("_userVadSettings");
            _visionContextModeProp = serializedObject.FindProperty("_visionContextMode");
            _visionInputSettingsProp = serializedObject.FindProperty("_visionInputSettings");
            _visionRespondModesProp = serializedObject.FindProperty("_visionRespondModes");
            _roomRejoinTtlSecondsProp = serializedObject.FindProperty("_roomRejoinTtlSeconds");
            _resumePolicyProp = serializedObject.FindProperty("_resumePolicy");
            _maxReconnectAttemptsProp = serializedObject.FindProperty("_maxReconnectAttempts");
            _spawnAgentOnRejoinProp = serializedObject.FindProperty("_spawnAgentOnRejoin");
            _startWaitTimeoutMsProp = serializedObject.FindProperty("_startWaitTimeoutMs");
            _autoMicStartDelaySecondsProp = serializedObject.FindProperty("_autoMicStartDelaySeconds");

            _showRoomControl = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionRoomControlId, true);
            _roomControlView.LoadState();
        }

        private void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionRoomControlId, _showRoomControl);
            _roomControlView.SaveState();
        }

        public override void OnInspectorGUI()
        {
            try
            {
                InitializeStyles();
                serializedObject.Update();

                DrawInspectorHeader();
                EditorGUILayout.Space(6f);

                DrawRoomControlSection();

                serializedObject.ApplyModifiedProperties();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiRoomManagerProfileEditor] Falling back to default inspector: {ex.Message}");
                serializedObject.UpdateIfRequiredOrScript();
                DrawDefaultInspector();
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            ConvaiInspectorStyleCache.EnsureInitialized();
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ConvaiInspectorThemeTokens.AccentEmphasis }
            };
            _statusStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.76f, 0.76f, 0.79f, 0.95f) }
            };
            _stylesInitialized = true;
        }

        private void DrawInspectorHeader()
        {
            const float headerHeight = 44f;
            const float iconSize = 22f;
            const float statusRegionWidth = 150f;

            Rect headerRect = EditorGUILayout.BeginVertical();
            try
            {
                EditorGUI.DrawRect(
                    new Rect(headerRect.x - 18f, headerRect.y - 4f, headerRect.width + 36f, headerHeight + 4f),
                    ConvaiInspectorThemeTokens.HeaderBackground);

                EditorGUILayout.BeginVertical(GUILayout.Height(headerHeight));
                try
                {
                    Rect rowRect = GUILayoutUtility.GetRect(0f, headerHeight, GUILayout.ExpandWidth(true),
                        GUILayout.Height(headerHeight));

                    if (_icon != null && Event.current.type == EventType.Repaint)
                    {
                        Rect iconRect = new(rowRect.x, rowRect.y + ((headerHeight - iconSize) * 0.5f), iconSize,
                            iconSize);
                        GUI.DrawTexture(iconRect, _icon, ScaleMode.ScaleToFit, true);
                    }

                    float textX = rowRect.x + iconSize + 6f;
                    Rect textRect = new(textX, rowRect.y + 11f, rowRect.width - statusRegionWidth - textX, 22f);
                    GUI.Label(textRect, "Convai Room Manager Profile", _headerStyle);

                    Rect statusRect = new(rowRect.xMax - statusRegionWidth, rowRect.y, statusRegionWidth,
                        rowRect.height);
                    GUI.Label(statusRect, "Reusable Asset", _statusStyle);
                }
                finally
                {
                    EditorGUILayout.EndVertical();
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }

            GUILayout.Space(2f);
        }

        /// <summary>
        ///     Draws the same groups, in the same order, as Advanced Room Control on ConvaiRoomManager. The
        ///     profile owns two extra fields the scene component does not surface: the video track name and
        ///     Connect On Start.
        /// </summary>
        private void DrawRoomControlSection()
        {
            _showRoomControl = DrawSectionHeader(SectionRoomControlId, "ROOM CONTROL", _showRoomControl, "\u2699");
            if (!_showRoomControl) return;

            DrawSectionBackground(() =>
                _roomControlView.Draw(
                    new ConvaiRoomControlProperties
                    {
                        ConnectionType = _connectionTypeProp,
                        VideoTrackName = _videoTrackNameProp,
                        ServerEndpoint = _serverEndpointProp,
                        ConnectOnStart = _connectOnStartProp,
                        TurnTakingOptions = _turnTakingOptionsProp,
                        UserVadSettings = _userVadSettingsProp,
                        VisionContextMode = _visionContextModeProp,
                        VisionInputSettings = _visionInputSettingsProp,
                        VisionRespondModes = _visionRespondModesProp,
                        RoomRejoinTtlSeconds = _roomRejoinTtlSecondsProp,
                        ResumePolicy = _resumePolicyProp,
                        MaxReconnectAttempts = _maxReconnectAttemptsProp,
                        SpawnAgentOnRejoin = _spawnAgentOnRejoinProp,
                        StartWaitTimeoutMs = _startWaitTimeoutMsProp,
                        AutoMicStartDelaySeconds = _autoMicStartDelaySecondsProp
                    },
                    false,
                    false));
        }

        private static void DrawSectionBackground(Action drawContent)
        {
            ConvaiInspectorSectionChrome.BeginBody();
            try
            {
                drawContent?.Invoke();
            }
            finally
            {
                ConvaiInspectorSectionChrome.EndBody();
            }
        }

        private bool DrawSectionHeader(string sectionId, string title, bool expanded, string icon) =>
            ConvaiInspectorSectionChrome.DrawHeader(
                new ConvaiInspectorSectionHeaderSpec(
                    EditorStateHostId,
                    sectionId,
                    title,
                    icon,
                    ConvaiInspectorThemeTokens.Accent),
                expanded);
    }
}
