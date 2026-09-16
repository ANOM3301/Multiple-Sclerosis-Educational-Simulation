using System;
using System.Collections.Generic;
using Convai.Editor.Inspectors;
using Convai.Runtime.Components;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    [CustomEditor(typeof(ConvaiPlayer))]
    public class ConvaiPlayerEditor : UnityEditor.Editor
    {
        private const string EditorStateHostId = "ConvaiPlayerEditor";
        private const string SectionIdentityId = "Identity";
        private const string SectionValidationId = "Validation";
        private const string SectionRuntimeId = "Runtime";
        private const double ValidationRefreshIntervalSeconds = 0.5d;
        private PlayerValidationReport _cachedValidationReport;
        private GUIStyle _headerStyle;
        private Texture2D _icon;
        private GUIStyle _miniButtonStyle;
        private SerializedProperty _nameTagColorProp;
        private double _nextValidationRefreshTime;

        private ConvaiPlayer _player;
        private SerializedProperty _playerIdProp;
        private SerializedProperty _playerNameProp;
        private bool _showIdentity = true;
        private bool _showRuntime;
        private bool _showValidation = true;
        private GUIStyle _statusStyle;
        private bool _stylesInitialized;

        private void OnEnable()
        {
            _player = (ConvaiPlayer)target;
            _icon = ConvaiEditorSettings.Instance.ConvaiIconTexture;
            _playerNameProp = serializedObject.FindProperty("_playerName");
            _playerIdProp = serializedObject.FindProperty("_playerId");
            _nameTagColorProp = serializedObject.FindProperty("_nameTagColor");

            _showIdentity = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionIdentityId, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidationId, true);
            _showRuntime = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionRuntimeId, false);

            InvalidateValidationCache();
        }

        private void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionIdentityId, _showIdentity);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidationId, _showValidation);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionRuntimeId, _showRuntime);
        }

        public override void OnInspectorGUI()
        {
            try
            {
                if (_playerNameProp == null || _playerIdProp == null || _nameTagColorProp == null)
                {
                    DrawDefaultInspector();
                    return;
                }

                InitializeStyles();
                serializedObject.Update();

                DrawInspectorHeader();
                EditorGUILayout.Space(6f);

                DrawIdentitySection();
                DrawValidationSection();

                if (UnityEngine.Application.isPlaying)
                    DrawRuntimeSection();

                serializedObject.ApplyModifiedProperties();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiPlayerEditor] Falling back to default inspector: {ex.Message}");
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
            _miniButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10, padding = new RectOffset(8, 8, 3, 3)
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
                    GUI.Label(textRect, "Convai Player", _headerStyle);

                    Rect statusRect = new(rowRect.xMax - statusRegionWidth, rowRect.y, statusRegionWidth,
                        rowRect.height);
                    GUI.Label(statusRect, GetHeaderStatusText(), _statusStyle);
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

        private string GetHeaderStatusText()
        {
            string playerName = _player != null ? _player.PlayerName : string.Empty;
            return string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;
        }

        private void DrawIdentitySection()
        {
            _showIdentity = DrawSectionHeader(SectionIdentityId, "IDENTITY", _showIdentity, "\u263A");
            if (!_showIdentity) return;

            DrawSectionBackground(() =>
            {
                EditorGUILayout.PropertyField(_playerNameProp, ConvaiInspectorContent.PlayerName);
                EditorGUILayout.PropertyField(_playerIdProp, ConvaiInspectorContent.PlayerId);
                EditorGUILayout.PropertyField(_nameTagColorProp, ConvaiInspectorContent.PlayerNameTagColor);

                EditorGUILayout.HelpBox(
                    "ConvaiPlayer defines the local player's identity for transcripts and conversation ownership. Microphone and conversation mode live on ConvaiManager.",
                    MessageType.Info);
            });
        }

        private void DrawValidationSection()
        {
            _showValidation = DrawSectionHeader(SectionValidationId, "VALIDATION", _showValidation, "\u2713");
            if (!_showValidation) return;

            DrawSectionBackground(() =>
            {
                PlayerValidationReport report = GetValidationReport();

                EditorGUILayout.HelpBox(report.Summary, report.MessageType);

                foreach (string message in report.Messages)
                    EditorGUILayout.LabelField($"• {message}", EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Project Settings", _miniButtonStyle, GUILayout.Width(120f)))
                    SettingsService.OpenProjectSettings("Project/Convai SDK");

                if (GUILayout.Button("Refresh Checks", _miniButtonStyle, GUILayout.Width(120f)))
                    InvalidateValidationCache(true);
                EditorGUILayout.EndHorizontal();
            });
        }

        private void DrawRuntimeSection()
        {
            _showRuntime = DrawSectionHeader(SectionRuntimeId, "RUNTIME", _showRuntime, "\u25CF");
            if (!_showRuntime) return;

            DrawSectionBackground(() =>
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Effective Player Name", _player.PlayerName ?? string.Empty);
                    EditorGUILayout.TextField("Effective Player ID", _player.PlayerId ?? string.Empty);
                    EditorGUILayout.ColorField("Name Tag Color", _player.NameTagColor);
                }
            });
        }

        private PlayerValidationReport GetValidationReport()
        {
            Event currentEvent = Event.current;
            bool shouldRefresh =
                _cachedValidationReport == null ||
                (currentEvent != null &&
                 currentEvent.type == EventType.Layout &&
                 EditorApplication.timeSinceStartup >= _nextValidationRefreshTime);

            if (!shouldRefresh)
                return _cachedValidationReport;

            _cachedValidationReport = BuildValidationReport();
            _nextValidationRefreshTime = EditorApplication.timeSinceStartup + ValidationRefreshIntervalSeconds;
            return _cachedValidationReport;
        }

        private PlayerValidationReport BuildValidationReport()
        {
            string playerName = _playerNameProp.stringValue?.Trim() ?? string.Empty;
            string playerId = _playerIdProp.stringValue?.Trim() ?? string.Empty;
            var manager = FindAnyObjectByType<ConvaiManager>();
            ConvaiPlayer[] players = FindObjectsByType<ConvaiPlayer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            var messages = new List<string>();
            var messageType = MessageType.Info;
            string summary = "Player setup looks healthy.";

            if (string.IsNullOrWhiteSpace(playerName))
            {
                messageType = MessageType.Warning;
                summary = "Player name is empty.";
                messages.Add("Set Player Name so transcripts and debugging use a clear local identity.");
            }

            if (manager == null)
            {
                messageType = MessageType.Warning;
                summary = "ConvaiManager was not found in the scene.";
                messages.Add("Add one ConvaiManager so the player can participate in room setup and conversations.");
            }

            if (players.Length > 1)
            {
                if (messageType != MessageType.Warning)
                {
                    messageType = MessageType.Warning;
                    summary = "Multiple ConvaiPlayer components were found.";
                }

                messages.Add("Multiple players are valid only if you intentionally manage ownership yourself.");
            }

            if (string.IsNullOrWhiteSpace(playerId))
                messages.Add("Player ID is empty, so the SDK will reuse Player Name for local transcript attribution.");

            if (messages.Count == 0)
                messages.Add("Player identity is configured and a ConvaiManager is present.");

            return new PlayerValidationReport(summary, messageType, messages.ToArray());
        }

        private void InvalidateValidationCache(bool forceImmediateRefresh = false)
        {
            _cachedValidationReport = null;
            _nextValidationRefreshTime = forceImmediateRefresh ? 0d : EditorApplication.timeSinceStartup;
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

        private sealed class PlayerValidationReport
        {
            public PlayerValidationReport(string summary, MessageType messageType, string[] messages)
            {
                Summary = summary;
                MessageType = messageType;
                Messages = messages ?? Array.Empty<string>();
            }

            public string Summary { get; }
            public MessageType MessageType { get; }
            public string[] Messages { get; }
        }
    }
}
