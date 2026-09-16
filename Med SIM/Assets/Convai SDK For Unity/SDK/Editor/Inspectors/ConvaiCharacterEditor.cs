using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Convai.Domain.Logging;
using Convai.Editor.Utilities;
using Convai.Editor.Inspectors;
using Convai.RestAPI;
using Convai.Runtime;
using Convai.Runtime.Components;
using Convai.Runtime.Logging;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    [CustomEditor(typeof(ConvaiCharacter))]
    public class ConvaiCharacterEditor : UnityEditor.Editor
    {
        private const string EditorStateHostId = "ConvaiCharacterEditor";
        private const string SectionConfigurationId = "Configuration";
        private const string SectionIdentityId = "Identity";
        private const string SectionAudioId = "Audio";
        private const string SectionSessionId = "Session";
        private const string SectionValidationId = "Validation";
        private const string SectionDebugId = "Debug";

        private static readonly Regex GuidRegex = new(
            @"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$",
            RegexOptions.Compiled);

        internal static readonly string[] PrimarySectionTitles =
        {
            "Configuration", "Identity", "Audio", "Session", "Validation"
        };

        private SerializedProperty _autoConnectProp;

        private ConvaiCharacter _character;
        private SerializedProperty _characterConfigAssetProp;
        private SerializedProperty _characterIdProp;
        private SerializedProperty _characterNameProp;
        private SerializedProperty _characterReadyTimeoutProp;
        private SerializedProperty _characterSessionIdProp;
        private SerializedProperty _configurationSourceInitializedProp;
        private SerializedProperty _configurationSourceProp;
        private Texture2D _convaiIcon;
        private SerializedProperty _dynamicInfoKeepInContextProp;
        private SerializedProperty _dynamicInfoTextProp;
        private SerializedProperty _enableRemoteAudioProp;
        private SerializedProperty _enableSessionResumeProp;
        private GUIStyle _headerStyle;
        private bool _isFetchingName;
        private GUIStyle _miniButtonStyle;
        private SerializedProperty _nameTagColorProp;
        private SerializedProperty _ownerIdProp;
        private bool _showAudio = true;
        private bool _showConfiguration = true;
        private bool _showDebug;
        private bool _showIdentity = true;
        private bool _showSession = true;
        private bool _showValidation = true;
        private GUIStyle _statusStyle;
        private bool _stylesInitialized;

        private bool UsesCharacterConfigAsset =>
            (ConvaiConfigSourceMode)_configurationSourceProp.enumValueIndex == ConvaiConfigSourceMode.Asset &&
            _characterConfigAssetProp.objectReferenceValue != null;

        private bool IsAssetModeSelected =>
            (ConvaiConfigSourceMode)_configurationSourceProp.enumValueIndex == ConvaiConfigSourceMode.Asset;

        private void OnEnable()
        {
            _character = (ConvaiCharacter)target;
            _convaiIcon = ConvaiEditorSettings.Instance.ConvaiIconTexture;

            _configurationSourceProp = serializedObject.FindProperty("_configurationSource");
            _configurationSourceInitializedProp = serializedObject.FindProperty("_configurationSourceInitialized");
            _characterConfigAssetProp = serializedObject.FindProperty("_characterConfigAsset");
            _characterIdProp = serializedObject.FindProperty("_characterId");
            _characterNameProp = serializedObject.FindProperty("_characterName");
            _nameTagColorProp = serializedObject.FindProperty("_nameTagColor");
            _autoConnectProp = serializedObject.FindProperty("_autoConnect");
            _enableRemoteAudioProp = serializedObject.FindProperty("_enableRemoteAudio");
            _enableSessionResumeProp = serializedObject.FindProperty("_enableSessionResume");
            _characterSessionIdProp = serializedObject.FindProperty("_characterSessionId");
            _ownerIdProp = serializedObject.FindProperty("_ownerId");
            _characterReadyTimeoutProp = serializedObject.FindProperty("_characterReadyTimeoutSeconds");
            _dynamicInfoTextProp = serializedObject.FindProperty("_initialDynamicInfoText");
            _dynamicInfoKeepInContextProp = serializedObject.FindProperty("_initialDynamicInfoKeepInContext");

            _showConfiguration = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionConfigurationId, true);
            _showIdentity = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionIdentityId, true);
            _showAudio = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionAudioId, true);
            _showSession = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionSessionId, true);
            _showValidation = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidationId, true);
            _showDebug = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionDebugId, false);

            EnsureConfigurationSourceMigration();
        }

        private void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionConfigurationId, _showConfiguration);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionIdentityId, _showIdentity);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionAudioId, _showAudio);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionSessionId, _showSession);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidationId, _showValidation);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionDebugId, _showDebug);
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

                DrawValidationSummary();
                DrawConfigurationSection();
                DrawIdentitySection();
                DrawAudioSection();
                DrawSessionSection();
                DrawValidationSection();

                if (UnityEngine.Application.isPlaying)
                    DrawDebugSection();

                serializedObject.ApplyModifiedProperties();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiCharacterEditor] Falling back to default inspector: {ex.Message}");
                serializedObject.UpdateIfRequiredOrScript();
                DrawDefaultInspector();
            }
        }

        private bool HasRequiredProperties() =>
            _configurationSourceProp != null &&
            _characterConfigAssetProp != null &&
            _characterIdProp != null &&
            _characterNameProp != null &&
            _enableRemoteAudioProp != null &&
            _enableSessionResumeProp != null &&
            _characterSessionIdProp != null;

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
            if (_configurationSourceProp == null || _configurationSourceInitializedProp == null ||
                _characterConfigAssetProp == null)
                return;

            if (_configurationSourceInitializedProp.boolValue) return;

            _configurationSourceProp.enumValueIndex =
                _characterConfigAssetProp.objectReferenceValue != null
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

                    if (_convaiIcon != null && Event.current.type == EventType.Repaint)
                    {
                        var iconRect = new Rect(rowRect.x, rowRect.y + ((headerHeight - iconSize) * 0.5f), iconSize,
                            iconSize);
                        GUI.DrawTexture(iconRect, _convaiIcon, ScaleMode.ScaleToFit, true);
                    }

                    float textX = rowRect.x + iconSize + 6f;
                    var textRect = new Rect(textX, rowRect.y + 11f, rowRect.width - statusRegionWidth - textX, 22f);
                    GUI.Label(textRect, "Convai Character", _headerStyle);

                    string modeText = IsAssetModeSelected ? "Character Profile" : "Inline";
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

        private void DrawValidationSummary()
        {
            string characterId = _character.CharacterId;
            string validationMessage = ValidateCharacterId(characterId);
            if (!string.IsNullOrEmpty(validationMessage))
                DrawHelpBox("Character ID", validationMessage, MessageType.Warning);

            if (IsAssetModeSelected && _characterConfigAssetProp.objectReferenceValue == null)
            {
                DrawHelpBox(
                    "Character Profile Missing",
                    "Character Setup Source is set to Character Profile Asset, but no Character Profile asset is assigned.",
                    MessageType.Error);
            }
        }

        private void DrawConfigurationSection()
        {
            _showConfiguration =
                DrawSectionHeader(SectionConfigurationId, "CONFIGURATION", _showConfiguration, "\u2699");
            if (!_showConfiguration) return;

            DrawSectionBackground(() =>
            {
                EditorGUILayout.PropertyField(_configurationSourceProp, new GUIContent("Character Setup Source"));

                var selectedMode = (ConvaiConfigSourceMode)_configurationSourceProp.enumValueIndex;
                if (selectedMode == ConvaiConfigSourceMode.Asset)
                {
                    EditorGUILayout.PropertyField(_characterConfigAssetProp, new GUIContent("Character Profile Asset"));

                    if (_characterConfigAssetProp.objectReferenceValue == null)
                    {
                        DrawHelpBox(
                            "Assign a Character Profile asset to make asset-backed values active, or switch Character Setup Source back to Inline.",
                            MessageType.Warning);
                        return;
                    }

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("Character ID", _character.CharacterId);
                        EditorGUILayout.TextField("Character Name", _character.CharacterName);
                        EditorGUILayout.ColorField("Name Tag Color", _character.NameTagColor);
                        EditorGUILayout.Toggle("Remote Audio On Start", _character.EnableRemoteAudioOnStart);
                        EditorGUILayout.Toggle("Session Resume", _character.EnableSessionResume);
                    }

                    EditorGUILayout.HelpBox(
                        "Character identity and default audio/session behavior are coming from the assigned Character Profile asset.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Inline values on this component are active. Switch to Asset when you want to reuse a Character Profile across scenes or prefabs.",
                        MessageType.Info);
                }
            });
        }

        private void DrawIdentitySection()
        {
            _showIdentity = DrawSectionHeader(SectionIdentityId, "IDENTITY", _showIdentity, "\u263A");
            if (!_showIdentity) return;

            DrawSectionBackground(() =>
            {
                if (IsAssetModeSelected)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("Character ID", _character.CharacterId);
                        EditorGUILayout.TextField("Character Name", _character.CharacterName);
                        EditorGUILayout.ColorField("Name Tag Color", _character.NameTagColor);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(_characterIdProp, new GUIContent("Character ID"));
                    EditorGUILayout.PropertyField(_characterNameProp, new GUIContent("Character Name"));
                    EditorGUILayout.PropertyField(_nameTagColorProp, new GUIContent("Name Tag Color"));
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = !IsAssetModeSelected;
                if (GUILayout.Button("Fetch Name", _miniButtonStyle, GUILayout.Width(90f)))
                    FetchCharacterNameFromApi();
                GUI.enabled = true;

                if (GUILayout.Button("Copy ID", _miniButtonStyle, GUILayout.Width(70f)))
                    GUIUtility.systemCopyBuffer = _character.CharacterId ?? string.Empty;

                if (GUILayout.Button("Dashboard", _miniButtonStyle, GUILayout.Width(80f)))
                {
                    string url = string.IsNullOrEmpty(_character.CharacterId)
                        ? ConvaiEditorLinks.DashboardHomeUrl
                        : $"{ConvaiEditorLinks.CharacterDashboardBaseUrl}?id={_character.CharacterId}";
                    UnityEngine.Application.OpenURL(url);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            });
        }

        private void DrawAudioSection()
        {
            _showAudio = DrawSectionHeader(SectionAudioId, "AUDIO", _showAudio, "\u266B");
            if (!_showAudio) return;

            DrawSectionBackground(() =>
            {
                if (IsAssetModeSelected)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.Toggle("Remote Audio On Start", _character.EnableRemoteAudioOnStart);
                }
                else
                    EditorGUILayout.PropertyField(_enableRemoteAudioProp, new GUIContent("Remote Audio On Start"));

                bool remoteAudioEnabled = _character.EnableRemoteAudioOnStart;
                bool hasAudioOutput = _character.GetComponent<ConvaiAudioOutput>() != null;

                if (remoteAudioEnabled && !hasAudioOutput)
                {
                    DrawHelpBox(
                        "Remote audio is enabled, but this GameObject does not have a ConvaiAudioOutput component yet.",
                        MessageType.Warning);

                    if (GUILayout.Button("Add ConvaiAudioOutput", _miniButtonStyle, GUILayout.Width(150f)))
                    {
                        Undo.AddComponent<ConvaiAudioOutput>(_character.gameObject);
                        EditorUtility.SetDirty(_character);
                    }
                }
            });
        }

        private void DrawSessionSection()
        {
            _showSession = DrawSectionHeader(SectionSessionId, "SESSION", _showSession, "\u21BB");
            if (!_showSession) return;

            DrawSectionBackground(() =>
            {
                EditorGUILayout.PropertyField(_autoConnectProp, new GUIContent("Auto Connect"));
                EditorGUILayout.PropertyField(_ownerIdProp, new GUIContent("Owner ID"));

                if (IsAssetModeSelected)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.Toggle("Session Resume", _character.EnableSessionResume);
                }
                else
                    EditorGUILayout.PropertyField(_enableSessionResumeProp, new GUIContent("Session Resume"));

                EditorGUILayout.PropertyField(
                    _characterSessionIdProp,
                    new GUIContent(
                        "Character Session ID",
                        "Used as character_session_id when Session Resume is enabled. Leave empty to start fresh; successful connects populate the current value."));

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy Session ID", _miniButtonStyle, GUILayout.Width(120f)))
                    GUIUtility.systemCopyBuffer = _character.CharacterSessionId;

                if (GUILayout.Button("Clear", _miniButtonStyle, GUILayout.Width(60f)))
                {
                    _characterSessionIdProp.stringValue = string.Empty;
                    serializedObject.ApplyModifiedProperties();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(_dynamicInfoKeepInContextProp,
                    new GUIContent("Keep Initial Dynamic Info In Context"));
                if (_dynamicInfoKeepInContextProp != null && _dynamicInfoKeepInContextProp.boolValue)
                {
                    EditorGUILayout.PropertyField(_dynamicInfoTextProp,
                        new GUIContent("Initial Dynamic Info Text"));
                }

                EditorGUILayout.PropertyField(_characterReadyTimeoutProp, new GUIContent("Ready Timeout (Seconds)"));
            });
        }

        private void DrawValidationSection()
        {
            _showValidation = DrawSectionHeader(SectionValidationId, "VALIDATION", _showValidation, "\u26A0");
            if (!_showValidation) return;

            DrawSectionBackground(() =>
            {
                string characterId = _character.CharacterId ?? string.Empty;
                string validationMessage = ValidateCharacterId(characterId);
                if (string.IsNullOrEmpty(validationMessage))
                    EditorGUILayout.HelpBox("Character ID format looks valid.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);

                if (FindAnyObjectByType<ConvaiManager>() == null)
                    EditorGUILayout.HelpBox("ConvaiManager was not found in the scene.", MessageType.Warning);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Project Settings", _miniButtonStyle, GUILayout.Width(110f)))
                    SettingsService.OpenProjectSettings("Project/Convai SDK");

                if (GUILayout.Button("Quickstart", _miniButtonStyle, GUILayout.Width(80f)))
                    UnityEngine.Application.OpenURL(ConvaiEditorLinks.DocsUnityQuickstartUrl);

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            });
        }

        private void DrawDebugSection()
        {
            _showDebug = DrawSectionHeader(SectionDebugId, "DEBUG", _showDebug, "\u25CF");
            if (!_showDebug) return;

            DrawSectionBackground(() =>
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Toggle("Injected", _character.IsInjected);
                    EditorGUILayout.EnumPopup("Session State", _character.SessionState);
                    EditorGUILayout.Toggle("Character Ready", _character.IsCharacterReady);
                    EditorGUILayout.Toggle("Speaking", _character.IsSpeaking);
                    EditorGUILayout.TextField("Current Emotion", _character.CurrentEmotion ?? string.Empty);
                    EditorGUILayout.IntField("Emotion Intensity", _character.CurrentEmotionIntensity);
                }
            });
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

        private static void DrawHelpBox(string message, MessageType messageType) =>
            EditorGUILayout.HelpBox(message, messageType);

        private static void DrawHelpBox(string title, string message, MessageType messageType) =>
            EditorGUILayout.HelpBox($"{title}: {message}", messageType);

        private static string ValidateCharacterId(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return "Character ID is required for the character to function.";

            if (characterId != characterId.Trim())
                return "Character ID has leading or trailing whitespace.";

            if (characterId.Length != 36)
                return $"Character ID should be 36 characters (current: {characterId.Length}).";

            if (!GuidRegex.IsMatch(characterId))
                return "Character ID format is invalid. Expected: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";

            return string.Empty;
        }

        private void FetchCharacterNameFromApi() => _ = FetchCharacterNameFromApiAsync();

        private async Task FetchCharacterNameFromApiAsync()
        {
            if (_isFetchingName || UsesCharacterConfigAsset) return;

            string characterId = _character?.CharacterId;
            string validationError = ValidateCharacterId(characterId);
            if (!string.IsNullOrEmpty(validationError))
            {
                ConvaiLogger.Warning($"Cannot fetch character name: {validationError}",
                    LogCategory.Editor);
                return;
            }

            var settings = ConvaiSettings.Instance;
            if (settings == null || !settings.HasApiKey)
            {
                ConvaiLogger.Warning("Cannot fetch character name: API key not configured.",
                    LogCategory.Editor);
                return;
            }

            _isFetchingName = true;
            try
            {
                var options = ConvaiRestOptionsFactory.Create(settings.ApiKey);
                using var client = new ConvaiRestClient(options);
                CharacterDetails details = await client.Characters.GetDetailsAsync(characterId);
                if (!string.IsNullOrWhiteSpace(details.CharacterName))
                {
                    string fetchedName = details.CharacterName.Trim();
                    EditorApplication.delayCall += () =>
                    {
                        if (this == null || _character == null || _characterNameProp == null || IsAssetModeSelected)
                            return;

                        Undo.RecordObject(_character, "Fetch Convai Character Name");
                        serializedObject.UpdateIfRequiredOrScript();
                        _characterNameProp.stringValue = fetchedName;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_character);
                        Repaint();
                    };
                }
            }
            catch (Exception ex)
            {
                ConvaiLogger.Error($"Failed to fetch character name: {ex.Message}",
                    LogCategory.Editor);
            }
            finally
            {
                _isFetchingName = false;
            }
        }
    }
}
