using System;
using Convai.Editor.ConfigurationWindow.Services;
using Convai.Editor.Inspectors;
using Convai.Runtime;
using Convai.Runtime.Adapters.Networking;
using Convai.Runtime.Room;
using Convai.Runtime.Vision.Sources;
using Convai.Shared.Interfaces;
using Convai.Shared.Types;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    [CustomEditor(typeof(ConvaiRoomManager))]
    public class ConvaiRoomManagerEditor : UnityEditor.Editor
    {
        private const string EditorStateHostId = "ConvaiRoomManagerEditor";
        private const string SectionConfigurationId = "Configuration";
        private const string SectionRoomControlId = "RoomControl";
        private const string SectionRuntimeId = "Runtime";
        private const string SectionConversationId = "Conversation";
        private const string SectionConnectionId = "Connection";
        private const string SectionValidationId = "Validation";
        private const string SectionAdvancedId = "Advanced";
        private const double ValidationRefreshIntervalSeconds = 0.5d;
        private SerializedProperty _autoMicStartDelaySecondsProp;
        private SetupHealthReport _cachedSetupHealthReport;
        private SerializedProperty _configurationSourceInitializedProp;
        private SerializedProperty _configurationSourceProp;
        private SerializedProperty _connectionTypeProp;
        private SerializedProperty _connectOnStartProp;
        private SerializedProperty _coreServerBaseUrlProp;
        private SerializedProperty _debugProp;
        private GUIStyle _headerStyle;
        private Texture2D _icon;
        private SerializedProperty _maxReconnectAttemptsProp;
        private GUIStyle _miniButtonStyle;
        private double _nextValidationRefreshTime;
        private SerializedProperty _resumePolicyProp;
        private SerializedProperty _roomConfigAssetProp;
        private SerializedObject _roomConfigSerializedObject;

        private ConvaiRoomManager _roomManager;
        private SerializedProperty _roomPushToTalkKeyProp;
        private SerializedProperty _roomRejoinTtlSecondsProp;
        private readonly ConvaiRoomControlInspectorView _roomControlView = new(EditorStateHostId);
        private SerializedProperty _serverEndpointProp;
        private bool _showAdvanced;
        private bool _showConfiguration;
        private bool _showConversation = true;
        private bool _showRoomControl;
        private bool _showRuntime;
        private bool _showConnection = true;
        private bool _showValidation = true;
        private SerializedProperty _spawnAgentOnRejoinProp;
        private SerializedProperty _startWaitTimeoutMsProp;
        private GUIStyle _statusStyle;
        private bool _stylesInitialized;
        private SerializedProperty _turnTakingOptionsProp;
        private SerializedProperty _userVadSettingsProp;
        private SerializedProperty _visionContextModeProp;
        private SerializedProperty _visionInputSettingsProp;
        private SerializedProperty _visionRespondModesProp;

        private bool UsesRoomConfigAsset =>
            (ConvaiConfigSourceMode)_configurationSourceProp.enumValueIndex == ConvaiConfigSourceMode.Asset &&
            _roomConfigAssetProp.objectReferenceValue != null;

        private bool IsAssetModeSelected =>
            (ConvaiConfigSourceMode)_configurationSourceProp.enumValueIndex == ConvaiConfigSourceMode.Asset;

        private bool HasLegacyCoreServerOverride =>
            _coreServerBaseUrlProp != null && !string.IsNullOrWhiteSpace(_coreServerBaseUrlProp.stringValue);

        private void OnEnable()
        {
            _roomManager = (ConvaiRoomManager)target;
            _icon = ConvaiEditorSettings.Instance.ConvaiIconTexture;

            _configurationSourceProp = serializedObject.FindProperty("_configurationSource");
            _configurationSourceInitializedProp = serializedObject.FindProperty("_configurationSourceInitialized");
            _roomConfigAssetProp = serializedObject.FindProperty("_roomConfigAsset");
            _connectionTypeProp = serializedObject.FindProperty("_connectionType");
            _coreServerBaseUrlProp = serializedObject.FindProperty("<CoreServerBaseURL>k__BackingField");
            _serverEndpointProp = serializedObject.FindProperty("<ServerEndpoint>k__BackingField");
            _connectOnStartProp = serializedObject.FindProperty("<ConnectOnStart>k__BackingField");
            _turnTakingOptionsProp = serializedObject.FindProperty("_turnTakingOptions");
            _userVadSettingsProp = serializedObject.FindProperty("_userVadSettings");
            _visionContextModeProp = serializedObject.FindProperty("_visionContextMode");
            _visionInputSettingsProp = serializedObject.FindProperty("_visionInputSettings");
            _visionRespondModesProp = serializedObject.FindProperty("_visionRespondModes");
            _roomPushToTalkKeyProp = serializedObject.FindProperty("_pushToTalkKey");
            _debugProp = serializedObject.FindProperty("<Debug>k__BackingField");
            _roomRejoinTtlSecondsProp = serializedObject.FindProperty("_roomRejoinTtlSeconds");
            _resumePolicyProp = serializedObject.FindProperty("_resumePolicy");
            _maxReconnectAttemptsProp = serializedObject.FindProperty("_maxReconnectAttempts");
            _spawnAgentOnRejoinProp = serializedObject.FindProperty("_spawnAgentOnRejoin");
            _startWaitTimeoutMsProp = serializedObject.FindProperty("_startWaitTimeoutMs");
            _autoMicStartDelaySecondsProp = serializedObject.FindProperty("_autoMicStartDelaySeconds");

            _showConversation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionConversationId, true);
            _showConnection = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionConnectionId, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidationId, true);
            _showAdvanced = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionAdvancedId, false);
            _showConfiguration = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionConfigurationId, true);
            _showRoomControl = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionRoomControlId, true);
            _showRuntime = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionRuntimeId, false);
            _roomControlView.LoadState();

            EnsureConfigurationSourceMigration();
        }

        private void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionConversationId, _showConversation);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionConnectionId, _showConnection);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidationId, _showValidation);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionAdvancedId, _showAdvanced);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionConfigurationId, _showConfiguration);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionRoomControlId, _showRoomControl);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionRuntimeId, _showRuntime);
            _roomControlView.SaveState();
        }

        public override void OnInspectorGUI()
        {
            try
            {
                if (!HasRequiredProperties())
                {
                    DrawDefaultInspector();
                    return;
                }

                InitializeStyles();
                serializedObject.Update();
                EnsureConfigurationSourceMigration();

                DrawInspectorHeader();
                EditorGUILayout.Space(6f);

                DrawConversationSection();
                DrawConnectionSection();
                DrawValidationSection();
                DrawAdvancedSection();

                serializedObject.ApplyModifiedProperties();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiRoomManagerEditor] Falling back to default inspector: {ex.Message}");
                serializedObject.UpdateIfRequiredOrScript();
                DrawDefaultInspector();
            }
        }

        private bool HasRequiredProperties() =>
            _configurationSourceProp != null &&
            _roomConfigAssetProp != null &&
            _connectionTypeProp != null &&
            _connectOnStartProp != null;

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

        private void EnsureConfigurationSourceMigration()
        {
            if (_configurationSourceInitializedProp == null || _roomConfigAssetProp == null ||
                _configurationSourceProp == null)
                return;

            if (_configurationSourceInitializedProp.boolValue) return;

            _configurationSourceProp.enumValueIndex =
                _roomConfigAssetProp.objectReferenceValue != null
                    ? (int)ConvaiConfigSourceMode.Asset
                    : (int)ConvaiConfigSourceMode.Inline;
            _configurationSourceInitializedProp.boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            serializedObject.Update();
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
                        var iconRect = new Rect(rowRect.x, rowRect.y + ((headerHeight - iconSize) * 0.5f), iconSize,
                            iconSize);
                        GUI.DrawTexture(iconRect, _icon, ScaleMode.ScaleToFit, true);
                    }

                    float textX = rowRect.x + iconSize + 6f;
                    var textRect = new Rect(textX, rowRect.y + 11f, rowRect.width - statusRegionWidth - textX, 22f);
                    GUI.Label(textRect, "Convai Room Manager", _headerStyle);

                    string modeText = _roomManager != null &&
                                      _roomManager.EffectiveTurnTakingOptions.Mode == ConversationInputMode.PushToTalk
                        ? "Push To Talk"
                        : "Hands Free";
                    var statusRect = new Rect(rowRect.xMax - statusRegionWidth, rowRect.y, statusRegionWidth,
                        rowRect.height);
                    GUI.Label(statusRect, modeText, _statusStyle);
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

        private void DrawConversationSection()
        {
            _showConversation = DrawSectionHeader(SectionConversationId, "CONVERSATION", _showConversation, "\u266B");
            if (!_showConversation) return;

            DrawSectionBackground(() =>
            {
                SerializedProperty turnTakingOptionsProp =
                    GetConversationTurnTakingOptionsProperty(out bool readOnlyAssetValues);
                if (turnTakingOptionsProp == null)
                {
                    EditorGUILayout.HelpBox(
                        IsAssetModeSelected
                            ? "Assign a Room Manager Profile asset or switch Room Setup Source back to scene defaults."
                            : "Turn-taking settings are not available.",
                        MessageType.Warning);
                    return;
                }

                if (readOnlyAssetValues && _roomConfigAssetProp.objectReferenceValue != null)
                {
                    EditorGUILayout.LabelField(
                        $"Using Room Manager Profile: {_roomConfigAssetProp.objectReferenceValue.name}",
                        EditorStyles.wordWrappedMiniLabel);
                }

                SerializedProperty modeProp = turnTakingOptionsProp.FindPropertyRelative("<Mode>k__BackingField");
                SerializedProperty pushToTalkPolicyProp =
                    turnTakingOptionsProp.FindPropertyRelative("<PushToTalkPolicy>k__BackingField");
                if (modeProp == null || pushToTalkPolicyProp == null)
                    return;

                using (new EditorGUI.DisabledScope(readOnlyAssetValues))
                    EditorGUILayout.PropertyField(modeProp, ConvaiInspectorContent.HowThePlayerTalks);

                if ((ConversationInputMode)modeProp.enumValueIndex != ConversationInputMode.PushToTalk)
                    return;

                if (_roomPushToTalkKeyProp != null)
                    EditorGUILayout.PropertyField(_roomPushToTalkKeyProp, ConvaiInspectorContent.PushToTalkKey);

                // Turn-completion gating and its fallback timeout live in Advanced > Turn Taking only.
                // This section stays at the two decisions a scene author makes while setting the mode up.
                using (new EditorGUI.DisabledScope(readOnlyAssetValues))
                {
                    EditorGUILayout.PropertyField(
                        pushToTalkPolicyProp.FindPropertyRelative("<InterruptBotOnPress>k__BackingField"),
                        ConvaiInspectorContent.InterruptCharacterWhenPressed);
                }
            });
        }

        /// <summary>
        ///     Connection Type and Starts Connected are ordinary setup decisions, not advanced tuning: the
        ///     former decides whether the character can see the scene at all, so both sit at the top rather
        ///     than under Advanced Room Control.
        /// </summary>
        private void DrawConnectionSection()
        {
            _showConnection = DrawSectionHeader(SectionConnectionId, "CONNECTION", _showConnection, "\u2194");
            if (!_showConnection) return;

            DrawSectionBackground(() =>
            {
                SerializedProperty connectionTypeProp = GetConnectionTypeProperty(out bool readOnlyAssetValue);
                SerializedProperty connectOnStartProp = GetConnectOnStartProperty(out _);
                if (connectionTypeProp == null && connectOnStartProp == null)
                {
                    EditorGUILayout.HelpBox(
                        IsAssetModeSelected
                            ? "Assign a Room Manager Profile asset or switch Room Setup Source back to scene defaults."
                            : "Connection settings are not available.",
                        MessageType.Warning);
                    return;
                }

                if (readOnlyAssetValue && _roomConfigAssetProp.objectReferenceValue != null)
                {
                    EditorGUILayout.LabelField(
                        $"Values come from Room Manager Profile: {_roomConfigAssetProp.objectReferenceValue.name}",
                        EditorStyles.wordWrappedMiniLabel);
                }

                using (new EditorGUI.DisabledScope(readOnlyAssetValue))
                {
                    if (connectionTypeProp != null)
                        EditorGUILayout.PropertyField(connectionTypeProp, ConvaiInspectorContent.ConnectionType);

                    if (connectOnStartProp != null)
                        EditorGUILayout.PropertyField(connectOnStartProp, ConvaiInspectorContent.StartsConnected);
                }

                DrawVideoConnectionHint(connectionTypeProp);
            });
        }

        /// <summary>
        ///     Video is the only choice with extra scene requirements, so the components it needs are named
        ///     where the choice is made instead of only in Validation.
        /// </summary>
        private void DrawVideoConnectionHint(SerializedProperty connectionTypeProp)
        {
            if (connectionTypeProp == null ||
                (ConvaiConnectionType)connectionTypeProp.enumValueIndex != ConvaiConnectionType.Video)
                return;

            (bool hasPublisher, bool hasFrameSource) = GetVisionComponentFlags();
            if (hasPublisher && hasFrameSource)
                return;

            EditorGUILayout.HelpBox(
                $"Video connections need {GetMissingVideoRequirementsMessage(hasPublisher, hasFrameSource)} on this GameObject or a child.",
                MessageType.Warning);
        }

        private void DrawValidationSection()
        {
            _showValidation = DrawSectionHeader(SectionValidationId, "VALIDATION", _showValidation, "\u2713");
            if (!_showValidation) return;

            DrawSectionBackground(() =>
            {
                SetupHealthReport report = GetSetupHealthReport();
                if (report.HasBlockingIssues)
                {
                    EditorGUILayout.HelpBox(
                        "Some required setup is still missing. Use the checks below to fix the scene.",
                        MessageType.Error);
                }
                else if (report.HasWarnings || HasRoomSpecificIssues())
                {
                    EditorGUILayout.HelpBox(
                        "Setup is mostly ready, but a few non-blocking issues should be reviewed.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Scene setup looks healthy.",
                        MessageType.Info);
                }

                foreach (SetupHealthCheckResult result in report.Results)
                {
                    EditorGUILayout.LabelField($"{GetStatusIcon(result.Status)} {result.Title}",
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(result.Message, EditorStyles.wordWrappedMiniLabel);
                    GUILayout.Space(3f);
                }

                DrawRoomSpecificValidationMessages();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Run Full Validation", _miniButtonStyle, GUILayout.Width(160f)))
                {
                    InvalidateValidationCache(true);
                    ConvaiSetupWizard.ValidateSceneSetup();
                }

                if (GUILayout.Button("Open Project Settings", _miniButtonStyle, GUILayout.Width(160f)))
                    SettingsService.OpenProjectSettings("Project/Convai SDK");
                EditorGUILayout.EndHorizontal();
            });
        }

        private void DrawAdvancedSection()
        {
            _showAdvanced = DrawSectionHeader(SectionAdvancedId, "ADVANCED SETTINGS", _showAdvanced, "\u2692");
            if (!_showAdvanced) return;

            DrawConfigurationSection();
            DrawRoomControlSection();
            DrawRuntimeSection();
        }

        private void DrawConfigurationSection()
        {
            _showConfiguration = DrawSectionHeader(SectionConfigurationId, "ROOM SOURCE", _showConfiguration, "\u2699");
            if (!_showConfiguration) return;

            DrawSectionBackground(() =>
            {
                EditorGUILayout.PropertyField(_configurationSourceProp, ConvaiInspectorContent.RoomSetupSource);
                if (IsAssetModeSelected)
                    EditorGUILayout.PropertyField(_roomConfigAssetProp, ConvaiInspectorContent.RoomConfigAsset);

                EditorGUILayout.LabelField(GetRoomSourceStatusText(), EditorStyles.wordWrappedMiniLabel);
                if (IsAssetModeSelected && _roomConfigAssetProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign a Room Manager Profile asset or switch Room Setup Source back to scene defaults.",
                        MessageType.Warning);
                }
            });
        }

        private void DrawRoomControlSection()
        {
            _showRoomControl = DrawSectionHeader(SectionRoomControlId, "ADVANCED ROOM CONTROL", _showRoomControl,
                "\u21C4");
            if (!_showRoomControl) return;

            DrawSectionBackground(() =>
            {
                if (IsAssetModeSelected && _roomConfigAssetProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign a Room Manager Profile asset to review advanced room control while Room Setup Source is set to Room Manager Profile Asset.",
                        MessageType.Warning);
                    return;
                }

                bool readOnly = IsAssetModeSelected;
                ConvaiRoomControlProperties properties = readOnly
                    ? BuildProfileRoomControlProperties()
                    : BuildInlineRoomControlProperties();
                if (properties == null)
                    return;

                if (readOnly)
                {
                    EditorGUILayout.LabelField(
                        $"Values come from Room Manager Profile: {_roomConfigAssetProp.objectReferenceValue.name}",
                        EditorStyles.wordWrappedMiniLabel);
                }

                _roomControlView.Draw(
                    properties,
                    readOnly,
                    ShouldShowRoomManagerPushToTalkKey(properties.TurnTakingOptions, readOnly));
            });
        }

        private void DrawRuntimeSection()
        {
            _showRuntime = DrawSectionHeader(SectionRuntimeId, "RUNTIME", _showRuntime, "\u25B6");
            if (!_showRuntime) return;

            DrawSectionBackground(() =>
            {
                string projectServerUrl =
                    ConvaiSettings.Instance != null ? ConvaiSettings.Instance.ServerUrl : string.Empty;
                string legacyOverride = _coreServerBaseUrlProp?.stringValue ?? string.Empty;
                bool hasLegacyOverride = !string.IsNullOrWhiteSpace(legacyOverride);
                string effectiveBaseUrl = hasLegacyOverride ? legacyOverride.Trim() : projectServerUrl;

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Session State", _roomManager.CurrentState.ToString());
                    EditorGUILayout.Toggle("Connected", _roomManager.IsConnected);
                    EditorGUILayout.TextField("Current Room",
                        string.IsNullOrWhiteSpace(_roomManager.CurrentRoomName)
                            ? "Not connected"
                            : _roomManager.CurrentRoomName);
                    EditorGUILayout.TextField("Session ID",
                        string.IsNullOrWhiteSpace(_roomManager.CurrentSessionId)
                            ? "Not connected"
                            : _roomManager.CurrentSessionId);
                    EditorGUILayout.TextField(
                        "Core Server URL Source",
                        hasLegacyOverride ? "Legacy Override (this component)" : "Project Settings");
                    EditorGUILayout.TextField("Effective Core Server Base URL", effectiveBaseUrl);
                    EditorGUILayout.TextField("Dynamic Vision Context",
                        _roomManager.EffectiveVisionContextEnabled
                            ? $"{_roomManager.EffectiveVisionContextMode} (enabled)"
                            : $"{_roomManager.EffectiveVisionContextMode} (disabled)");
                    EditorGUILayout.TextField("Effective Connection Type", _roomManager.EffectiveConnectionType.ToString());
                    if (!string.IsNullOrWhiteSpace(_roomManager.RoomControllerTypeName))
                        EditorGUILayout.TextField("Room Controller", _roomManager.RoomControllerTypeName);
                    if (!string.IsNullOrWhiteSpace(_roomManager.TransportAccessorTypeName))
                        EditorGUILayout.TextField("Transport", _roomManager.TransportAccessorTypeName);
                }

                if (hasLegacyOverride)
                {
                    EditorGUILayout.HelpBox(
                        "This component still has a legacy Core Server Base URL override. Move it to Project Settings, then clear the per-scene value.",
                        MessageType.Warning);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Copy To Project Settings", _miniButtonStyle, GUILayout.Width(170f)))
                        CopyLegacyCoreServerOverrideToProjectSettings();
                    if (GUILayout.Button("Clear Legacy Override", _miniButtonStyle, GUILayout.Width(150f)))
                        ClearLegacyCoreServerOverride();
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Open Convai SDK Project Settings", _miniButtonStyle, GUILayout.Width(220f)))
                    SettingsService.OpenProjectSettings("Project/Convai SDK");

                EditorGUILayout.PropertyField(_debugProp, ConvaiInspectorContent.Debug);
            });
        }

        private string GetRoomSourceStatusText()
        {
            if (!IsAssetModeSelected)
                return "Using scene defaults.";

            return _roomConfigAssetProp.objectReferenceValue != null
                ? $"Using Room Manager Profile: {_roomConfigAssetProp.objectReferenceValue.name}"
                : "Room Manager Profile not assigned.";
        }

        /// <summary>
        ///     Room control values as owned by this component. Connection Type and Starts Connected live in
        ///     the Connection section, and the video track name is never component-editable because the
        ///     backend expects a specific track-name format.
        /// </summary>
        private ConvaiRoomControlProperties BuildInlineRoomControlProperties() =>
            new()
            {
                ServerEndpoint = _serverEndpointProp,
                TurnTakingOptions = _turnTakingOptionsProp,
                PushToTalkKey = _roomPushToTalkKeyProp,
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
            };

        /// <summary>
        ///     The same values as read from the assigned Room Manager Profile, for read-only review. The
        ///     push-to-talk key stays component-owned and therefore editable.
        /// </summary>
        private ConvaiRoomControlProperties BuildProfileRoomControlProperties()
        {
            if (_roomConfigAssetProp.objectReferenceValue is not ConvaiRoomManagerProfile roomConfig)
                return null;

            SerializedObject profile = GetRoomConfigSerializedObject(roomConfig);
            if (profile == null)
                return null;

            return new ConvaiRoomControlProperties
            {
                ServerEndpoint = profile.FindProperty("_serverEndpoint"),
                TurnTakingOptions = profile.FindProperty("_turnTakingOptions"),
                PushToTalkKey = _roomPushToTalkKeyProp,
                UserVadSettings = profile.FindProperty("_userVadSettings"),
                VisionContextMode = profile.FindProperty("_visionContextMode"),
                VisionInputSettings = profile.FindProperty("_visionInputSettings"),
                VisionRespondModes = profile.FindProperty("_visionRespondModes"),
                RoomRejoinTtlSeconds = profile.FindProperty("_roomRejoinTtlSeconds"),
                ResumePolicy = profile.FindProperty("_resumePolicy"),
                MaxReconnectAttempts = profile.FindProperty("_maxReconnectAttempts"),
                SpawnAgentOnRejoin = profile.FindProperty("_spawnAgentOnRejoin"),
                StartWaitTimeoutMs = profile.FindProperty("_startWaitTimeoutMs"),
                AutoMicStartDelaySeconds = profile.FindProperty("_autoMicStartDelaySeconds")
            };
        }

        private bool ShouldShowRoomManagerPushToTalkKey(
            SerializedProperty turnTakingOptionsProp,
            bool usesRoomConfigAsset)
        {
            if (usesRoomConfigAsset)
                return _roomManager != null &&
                       _roomManager.EffectiveTurnTakingOptions.Mode == ConversationInputMode.PushToTalk;

            if (turnTakingOptionsProp == null)
                return false;

            SerializedProperty modeProp = turnTakingOptionsProp.FindPropertyRelative("<Mode>k__BackingField");
            return modeProp != null &&
                   (ConversationInputMode)modeProp.enumValueIndex == ConversationInputMode.PushToTalk;
        }

        private SerializedProperty GetConversationTurnTakingOptionsProperty(out bool readOnlyAssetValues)
        {
            readOnlyAssetValues = false;
            if (!IsAssetModeSelected)
                return _turnTakingOptionsProp;

            if (_roomConfigAssetProp.objectReferenceValue is not ConvaiRoomManagerProfile roomConfig)
                return null;

            SerializedObject roomConfigSerializedObject = GetRoomConfigSerializedObject(roomConfig);
            if (roomConfigSerializedObject == null)
                return null;

            readOnlyAssetValues = true;
            return roomConfigSerializedObject.FindProperty("_turnTakingOptions");
        }

        private SerializedProperty GetConnectionTypeProperty(out bool readOnlyAssetValue)
        {
            readOnlyAssetValue = false;
            if (!IsAssetModeSelected)
                return _connectionTypeProp;

            if (_roomConfigAssetProp.objectReferenceValue is not ConvaiRoomManagerProfile roomConfig)
                return null;

            SerializedObject roomConfigSerializedObject = GetRoomConfigSerializedObject(roomConfig);
            if (roomConfigSerializedObject == null)
                return null;

            readOnlyAssetValue = true;
            return roomConfigSerializedObject.FindProperty("_connectionType");
        }

        private SerializedProperty GetConnectOnStartProperty(out bool readOnlyAssetValue)
        {
            readOnlyAssetValue = false;
            if (!IsAssetModeSelected)
                return _connectOnStartProp;

            if (_roomConfigAssetProp.objectReferenceValue is not ConvaiRoomManagerProfile roomConfig)
                return null;

            SerializedObject roomConfigSerializedObject = GetRoomConfigSerializedObject(roomConfig);
            if (roomConfigSerializedObject == null)
                return null;

            readOnlyAssetValue = true;
            return roomConfigSerializedObject.FindProperty("_connectOnStart");
        }

        private SerializedObject GetRoomConfigSerializedObject(ConvaiRoomManagerProfile roomConfig)
        {
            if (roomConfig == null)
            {
                _roomConfigSerializedObject = null;
                return null;
            }

            if (_roomConfigSerializedObject == null || _roomConfigSerializedObject.targetObject != roomConfig)
                _roomConfigSerializedObject = new SerializedObject(roomConfig);

            _roomConfigSerializedObject.UpdateIfRequiredOrScript();
            return _roomConfigSerializedObject;
        }

        private bool HasRoomSpecificIssues()
        {
            if (IsAssetModeSelected && _roomConfigAssetProp.objectReferenceValue == null)
                return true;

            if (HasLegacyCoreServerOverride)
                return true;

            if (_roomManager != null && _roomManager.EffectiveConnectionType == ConvaiConnectionType.Video)
            {
                (bool hasPublisher, bool hasFrameSource) = GetVisionComponentFlags();
                if (!hasPublisher || !hasFrameSource)
                    return true;
            }

            return false;
        }

        private void DrawRoomSpecificValidationMessages()
        {
            bool anyRoomSpecificIssues = false;

            if (IsAssetModeSelected && _roomConfigAssetProp.objectReferenceValue == null)
            {
                EditorGUILayout.LabelField("\u2717 Room Manager Profile", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Room Manager Profile Asset is required while Room Setup Source is set to Room Manager Profile Asset.",
                    EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(3f);
                anyRoomSpecificIssues = true;
            }

            if (HasLegacyCoreServerOverride)
            {
                EditorGUILayout.LabelField("\u26A0 Legacy Server Override", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "This component still has a legacy Core Server Base URL override. Move it to Project Settings, then clear the per-scene value.",
                    EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(3f);
                anyRoomSpecificIssues = true;
            }

            if (_roomManager != null && _roomManager.EffectiveConnectionType == ConvaiConnectionType.Video)
            {
                (bool hasPublisher, bool hasFrameSource) = GetVisionComponentFlags();
                if (!hasPublisher || !hasFrameSource)
                {
                    EditorGUILayout.LabelField("\u26A0 Dynamic Vision Requirements", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        $"Dynamic vision context is missing required components: {GetMissingVideoRequirementsMessage(hasPublisher, hasFrameSource)}.",
                        EditorStyles.wordWrappedMiniLabel);
                    GUILayout.Space(3f);
                    anyRoomSpecificIssues = true;
                }
            }

            if (!anyRoomSpecificIssues)
                EditorGUILayout.LabelField("No room-specific issues detected.", EditorStyles.wordWrappedMiniLabel);
        }

        private static string GetStatusIcon(SetupHealthStatus status) =>
            status switch
            {
                SetupHealthStatus.Healthy => "\u2713",
                SetupHealthStatus.Warning => "\u26A0",
                _ => "\u2717"
            };

        private SetupHealthReport GetSetupHealthReport()
        {
            Event currentEvent = Event.current;
            bool shouldRefresh =
                _cachedSetupHealthReport == null ||
                (currentEvent != null &&
                 currentEvent.type == EventType.Layout &&
                 EditorApplication.timeSinceStartup >= _nextValidationRefreshTime);

            if (!shouldRefresh)
                return _cachedSetupHealthReport;

            _cachedSetupHealthReport = SetupHealthService.BuildReport();
            _nextValidationRefreshTime = EditorApplication.timeSinceStartup + ValidationRefreshIntervalSeconds;
            return _cachedSetupHealthReport;
        }

        private void InvalidateValidationCache(bool forceImmediateRefresh = false)
        {
            _cachedSetupHealthReport = null;
            _nextValidationRefreshTime = forceImmediateRefresh ? 0d : EditorApplication.timeSinceStartup;
        }

        private (bool hasPublisher, bool hasFrameSource) GetVisionComponentFlags()
        {
            bool hasPublisher = false;
            bool hasFrameSource = false;

            foreach (MonoBehaviour component in _roomManager.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!hasPublisher && component is IVisionPublisher) hasPublisher = true;
                if (!hasFrameSource && component is IVisionFrameSource) hasFrameSource = true;
                if (hasPublisher && hasFrameSource) break;
            }

            return (hasPublisher, hasFrameSource);
        }

        private static string GetMissingVideoRequirementsMessage(bool hasPublisher, bool hasFrameSource)
        {
            if (!hasPublisher && !hasFrameSource)
                return "ConvaiVisionPublisher and a vision frame source";
            if (!hasPublisher)
                return "ConvaiVisionPublisher";
            return "a vision frame source";
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

        private void CopyLegacyCoreServerOverrideToProjectSettings()
        {
            if (!HasLegacyCoreServerOverride) return;

            var settings = ConvaiSettings.Instance;
            if (settings == null) return;

            Undo.RecordObject(settings, "Copy Convai Server URL To Project Settings");
            settings.SetServerUrl(_coreServerBaseUrlProp.stringValue.Trim());
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private void ClearLegacyCoreServerOverride()
        {
            if (!HasLegacyCoreServerOverride) return;

            Undo.RecordObject(_roomManager, "Clear Legacy Convai Server URL Override");
            _coreServerBaseUrlProp.stringValue = string.Empty;
            EditorUtility.SetDirty(_roomManager);
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }
    }
}
