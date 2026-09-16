#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Convai.Domain.Models.LipSync;
using Convai.Editor.Inspectors;
using Convai.Modules.LipSync.Profiles;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Convai.Modules.LipSync.Editor
{
    [CustomEditor(typeof(ConvaiLipSyncComponent))]
    internal sealed class ConvaiLipSyncComponentEditor : UnityEditor.Editor
    {
        #region Editor Mode Info

        private void DrawEditorModeInfo()
        {
            _showLiveStatus = DrawSectionHeader(SectionLiveStatusId, "LIVE STATUS", _showLiveStatus, "\u25cf",
                ConvaiGreen, SectionIconFontSize);
            if (!_showLiveStatus) return;

            DrawSectionBackground(() =>
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.color = new Color(0.76f, 0.76f, 0.79f, 0.95f);
                GUILayout.Label("Offline - Enter Play Mode for live telemetry.", EditorStyles.miniLabel);
                GUI.color = Color.white;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            });
        }

        #endregion

        #region Constants & Colors

        private static readonly Color ConvaiGreen = ConvaiInspectorThemeTokens.Accent;
        private static readonly Color ConvaiGreenLight = ConvaiInspectorThemeTokens.AccentEmphasis;
        private static readonly Color ConvaiWarning = ConvaiInspectorThemeTokens.Warning;
        private static readonly Color ConvaiError = ConvaiInspectorThemeTokens.Error;
        private static readonly Color ConvaiInfo = ConvaiInspectorThemeTokens.Info;

        private static readonly Color SectionBg = ConvaiInspectorThemeTokens.SectionBackground;
        private const int SectionIconFontSize = ConvaiInspectorThemeTokens.SectionIconFontSize;

        private const string DefaultRegistryResourcePath = "LipSync/DefaultMaps/LipSyncDefaultMapRegistry";
        private const string EditorStateHostId = "ComponentEditor";
        private const string SectionCoreSetupId = "CoreSetup";
        private const string SectionPlaybackBehaviorId = "PlaybackBehavior";
        private const string SectionStreamingLatencyId = "StreamingLatency";
        private const string SectionLiveStatusId = "LiveStatus";

        #endregion

        #region Private Fields

        private ConvaiLipSyncComponent _component;

        private bool _showCoreSetup = true;
        private bool _showPlaybackBehavior = true;
        private bool _showStreamingLatency;
        private bool _showLiveStatus = true;

        private ReorderableList _targetMeshesList;

        private SerializedProperty _lockedProfileProp;
        private SerializedProperty _timeOffsetProp;
        private SerializedProperty _fadeOutDurationProp;
        private SerializedProperty _smoothingFactorProp;
        private SerializedProperty _latencyModeProp;
        private SerializedProperty _maxBufferedSecondsProp;
        private SerializedProperty _minResumeHeadroomSecondsProp;
        private SerializedProperty _deliverChunksAheadProp;
        private SerializedProperty _targetMeshesProp;
        private SerializedProperty _mappingProp;

        private GUIStyle _miniButtonStyle;
        private GUIStyle _meshCountStyle;
        private GUIStyle _liveCellLabelStyle;
        private GUIStyle _liveCellValueStyle;
        private GUIStyle _liveHeaderIconStyle;
        private GUIStyle _liveHeaderTextStyle;
        private GUIStyle _liveHeaderProfileStyle;
        private bool _stylesInitialized;

        private ConvaiLipSyncDefaultMapRegistry _defaultMapRegistry;
        private readonly List<SkinnedMeshRenderer> _tempMeshes = new();
        private readonly HashSet<EntityId> _seenMeshIds = new();
        private readonly HashSet<string> _uniqueBlendshapes = new(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Unity Editor Lifecycle

        private void OnEnable()
        {
            _component = (ConvaiLipSyncComponent)target;
            CacheSerializedProperties();
            _defaultMapRegistry = Resources.Load<ConvaiLipSyncDefaultMapRegistry>(DefaultRegistryResourcePath);

            _showCoreSetup = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionCoreSetupId, true);
            _showPlaybackBehavior =
                ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionPlaybackBehaviorId, true);
            _showStreamingLatency =
                ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionStreamingLatencyId, false);
            _showLiveStatus = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionLiveStatusId, true);
        }

        private void OnDisable()
        {
            _stylesInitialized = false;

            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionCoreSetupId, _showCoreSetup);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionPlaybackBehaviorId, _showPlaybackBehavior);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionStreamingLatencyId, _showStreamingLatency);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionLiveStatusId, _showLiveStatus);
        }

        private void CacheSerializedProperties()
        {
            _lockedProfileProp = serializedObject.FindProperty("_lockedProfileId");
            _timeOffsetProp = serializedObject.FindProperty("_timeOffset");
            _fadeOutDurationProp = serializedObject.FindProperty("_fadeOutDuration");
            _smoothingFactorProp = serializedObject.FindProperty("_smoothingFactor");
            _latencyModeProp = serializedObject.FindProperty("_latencyMode");
            _maxBufferedSecondsProp = serializedObject.FindProperty("_maxBufferedSeconds");
            _minResumeHeadroomSecondsProp = serializedObject.FindProperty("_minResumeHeadroomSeconds");
            _deliverChunksAheadProp = serializedObject.FindProperty("_deliverChunksAhead");
            _targetMeshesProp = serializedObject.FindProperty("_targetMeshes");
            _mappingProp = serializedObject.FindProperty("_mapping");
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _miniButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10, padding = new RectOffset(8, 8, 3, 3)
            };

            _meshCountStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            _meshCountStyle.normal.textColor = new Color(0.435f, 0.812f, 0.592f, 0.8f);

            _liveCellLabelStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 9 };
            _liveCellLabelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            _liveCellValueStyle = new GUIStyle(EditorStyles.label) { fontSize = 11 };

            ConvaiInspectorStyleCache.EnsureInitialized();
            _liveHeaderIconStyle = new GUIStyle(ConvaiInspectorStyleCache.SectionHeaderLabelStyle) { fontSize = 14 };
            _liveHeaderTextStyle = new GUIStyle(ConvaiInspectorStyleCache.SectionHeaderLabelStyle) { fontSize = 12, fontStyle = FontStyle.Bold };
            _liveHeaderProfileStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight };

            _stylesInitialized = true;
        }

        private void InitializeReorderableList()
        {
            if (_targetMeshesList != null) return;

            _targetMeshesList = new ReorderableList(serializedObject, _targetMeshesProp, true, true, true, true);

            _targetMeshesList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect,
                    new GUIContent("Target Meshes", "Blendshape target renderers used by lip sync runtime."));
            };

            _targetMeshesList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty element = _targetMeshesProp.GetArrayElementAtIndex(index);

                rect.y += 2;

                var objRect = new Rect(rect.x, rect.y, rect.width - 110f, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(objRect, element, GUIContent.none);

                var countRect = new Rect(rect.xMax - 110f, rect.y, 110f, EditorGUIUtility.singleLineHeight);
                var meshRenderer = element.objectReferenceValue as SkinnedMeshRenderer;

                _meshCountStyle.normal.textColor = new Color(0.435f, 0.812f, 0.592f, 0.8f);

                if (meshRenderer != null && meshRenderer.sharedMesh != null)
                {
                    int bsCount = meshRenderer.sharedMesh.blendShapeCount;
                    string label = bsCount > 0 ? $"{bsCount} blendshapes" : "0 blendshapes";

                    if (bsCount == 0) _meshCountStyle.normal.textColor = ConvaiError;

                    EditorGUI.LabelField(countRect, label, _meshCountStyle);
                }
                else if (meshRenderer != null)
                {
                    _meshCountStyle.normal.textColor = ConvaiWarning;
                    EditorGUI.LabelField(countRect, "No Mesh Data", _meshCountStyle);
                }
                else
                {
                    _meshCountStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    EditorGUI.LabelField(countRect, "Empty", _meshCountStyle);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();
            InitializeReorderableList();
            serializedObject.Update();

            PopulateAssignedMeshes();

            DrawInspectorHeader();
            EditorGUILayout.Space(6);

            DrawValidationWarnings();

            DrawCoreSetupSection();
            DrawPlaybackBehaviorSection();
            DrawStreamingLatencySection();

            if (UnityEngine.Application.isPlaying)
                DrawLiveStatusSection();
            else
                DrawEditorModeInfo();

            serializedObject.ApplyModifiedProperties();

            if (UnityEngine.Application.isPlaying && _component.IsPlaying) Repaint();
        }

        #endregion

        #region Header & Branding

        private void DrawInspectorHeader()
        {
            GetHeaderStatus(out string statusText, out Color statusColor);
            ConvaiBrandedInspectorChrome.DrawHeader("Convai Lip Sync", string.Empty, statusText, statusColor);
        }

        private void GetHeaderStatus(out string statusText, out Color statusColor)
        {
            if (_component == null || !UnityEngine.Application.isPlaying)
            {
                statusColor = new Color(0.75f, 0.75f, 0.78f);
                statusText = "Editor";
                return;
            }

            PlaybackState state = _component.EngineState;
            switch (state)
            {
                case PlaybackState.FadingOut:
                    statusColor = ConvaiWarning;
                    statusText = "Fading";
                    break;
                case PlaybackState.Playing:
                    statusColor = ConvaiGreen;
                    statusText = "Playing";
                    break;
                case PlaybackState.Starving:
                    statusColor = ConvaiWarning;
                    statusText = "Starving";
                    break;
                case PlaybackState.Buffering:
                    statusColor = ConvaiInfo;
                    statusText = "Buffering";
                    break;
                default:
                    statusColor = new Color(0.5f, 0.5f, 0.5f);
                    statusText = "Idle";
                    break;
            }
        }

        #endregion

        #region Validation Warnings

        private void DrawValidationWarnings()
        {
            if (_tempMeshes.Count == 0)
            {
                DrawWarningBox(
                    "Target Mesh Required",
                    "Assign at least one SkinnedMeshRenderer with Blendshapes to enable lip sync.",
                    "Auto-Find",
                    AutoFindMeshesInHierarchy);
            }
            else if (GetTotalUniqueBlendshapeCount() == 0)
            {
                DrawErrorBox(
                    "No Blendshapes Found",
                    "Assigned target meshes do not contain blendshapes. Lip sync requires at least one blendshape.");
            }

            LipSyncProfileId profileId = GetInspectorProfile();
            if (!LipSyncProfileCatalog.TryGetProfile(profileId, out _))
            {
                DrawErrorBox(
                    "Unknown Lip Sync Profile",
                    $"Profile '{profileId}' is not registered in LipSync profile catalog.");
            }
            else if (_mappingProp.objectReferenceValue is ConvaiLipSyncMapAsset assignedMap &&
                     assignedMap.TargetProfileId != profileId)
            {
                DrawWarningBox(
                    "Profile / Mapping Mismatch",
                    $"Selected profile is '{ToDisplayProfileName(profileId)}' but mapping targets '{ToDisplayProfileName(assignedMap.TargetProfileId)}'. " +
                    "Runtime will ignore this mapping and use the selected profile default map.",
                    null,
                    null);
            }
            else if (_mappingProp.objectReferenceValue == null && GetProfileDefaultMap(profileId) == null)
            {
                DrawWarningBox(
                    "Default Mapping Missing",
                    $"No default mapping registered for profile '{ToDisplayProfileName(profileId)}'. Runtime will use safe-disabled map.",
                    null,
                    null);
            }
        }

        private void DrawWarningBox(string title, string message, string buttonText, Action buttonAction)
        {
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.65f, 0.15f, 0.2f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("\u26a0", GUILayout.Width(20));
            EditorGUILayout.BeginVertical();
            GUILayout.Label(title, EditorStyles.boldLabel);
            GUILayout.Label(message, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(buttonText) && buttonAction != null)
            {
                GUILayout.Space(4);
                if (GUILayout.Button(buttonText, _miniButtonStyle, GUILayout.Width(80))) buttonAction();
            }

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = prevBg;
        }

        private void DrawErrorBox(string title, string message)
        {
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.94f, 0.33f, 0.31f, 0.2f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("\u2715", GUILayout.Width(20));
            EditorGUILayout.BeginVertical();
            GUILayout.Label(title, EditorStyles.boldLabel);
            GUILayout.Label(message, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = prevBg;
        }

        #endregion

        #region Editor Blocks

        private void DrawCoreSetupSection()
        {
            _showCoreSetup = DrawSectionHeader(SectionCoreSetupId, "CORE SETUP", _showCoreSetup, "\u2699", ConvaiGreen,
                SectionIconFontSize);
            if (!_showCoreSetup) return;

            DrawSectionBackground(() =>
            {
                DrawProfileSelector();
                EditorGUILayout.PropertyField(_mappingProp,
                    new GUIContent("Mapping", "Lip Sync mapping for the selected profile."));

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create New", _miniButtonStyle, GUILayout.Width(80))) CreateMappingAsset();
                EditorGUI.BeginDisabledGroup(_mappingProp.objectReferenceValue == null);
                if (GUILayout.Button("Edit", _miniButtonStyle, GUILayout.Width(50)))
                    Selection.activeObject = _mappingProp.objectReferenceValue;
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button(
                        new GUIContent("Validator", "Open Lip Sync Mapping Validator to check blendshape mappings."),
                        _miniButtonStyle, GUILayout.Width(60)))
                    ConvaiLipSyncMapDebugWindow.ShowForComponent(_component);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);

                _targetMeshesList.DoLayoutList();

                int count = _tempMeshes.Count > 0 ? GetTotalUniqueBlendshapeCount() : 0;

                EditorGUILayout.BeginHorizontal();
                if (_tempMeshes.Count > 0)
                {
                    GUI.color = count > 0 ? ConvaiGreenLight : ConvaiError;
                    string icon = count > 0 ? "\u2713" : "\u2715";
                    GUILayout.Label($"{icon} {_tempMeshes.Count} Meshes Found ({count} Blendshapes)",
                        EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = ConvaiWarning;
                    GUILayout.Label("\u26a0 No meshes assigned", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }

                GUILayout.FlexibleSpace();
                GUI.backgroundColor = ConvaiGreen;
                if (GUILayout.Button("Auto-Find", _miniButtonStyle, GUILayout.Width(80))) AutoFindMeshesInHierarchy();
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            });
        }

        private void DrawPlaybackBehaviorSection()
        {
            _showPlaybackBehavior = DrawSectionHeader(SectionPlaybackBehaviorId, "PLAYBACK & BEHAVIOR",
                _showPlaybackBehavior, "\u25ce", ConvaiGreen, SectionIconFontSize);
            if (!_showPlaybackBehavior) return;

            DrawSectionBackground(() =>
            {
                EditorGUILayout.PropertyField(_smoothingFactorProp,
                    new GUIContent("Lip Smoothing", "Reduces high-frequency jitter in lip movements."));
                EditorGUILayout.PropertyField(_fadeOutDurationProp,
                    new GUIContent("Fade Transition", "Duration of the blend back to the neutral pose in seconds."));
                if (_timeOffsetProp != null)
                {
                    EditorGUILayout.PropertyField(_timeOffsetProp,
                        new GUIContent("A/V Sync Offset", "Fine-tune the audio-visual synchronization in seconds."));
                }
            });
        }

        private void DrawStreamingLatencySection()
        {
            _showStreamingLatency = DrawSectionHeader(SectionStreamingLatencyId, "STREAMING & LATENCY",
                _showStreamingLatency, "\u21c4", ConvaiGreen, SectionIconFontSize);
            if (!_showStreamingLatency) return;

            DrawSectionBackground(() =>
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_latencyModeProp,
                    new GUIContent("Latency Mode",
                        "Preset strategies governing network resilience vs. playback delay. 'Balanced' (0.12s headroom) is highly recommended for general use."));
                if (EditorGUI.EndChangeCheck()) ApplyLatencyPresetForMode(_latencyModeProp.intValue);
                EditorGUILayout.PropertyField(_deliverChunksAheadProp,
                    new GUIContent("Ahead Chunk Delivery (Preview)",
                        "Opt into indexed NeuroSync chunks delivered ahead of audio playback. Leave disabled unless validating the preview backend contract."));
                if (_latencyModeProp.intValue == (int)LipSyncLatencyMode.Custom)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_maxBufferedSecondsProp,
                        new GUIContent("Ring Buffer Cap (s)",
                            "Maximum capacity (in seconds) of upcoming lip sync data to store in memory. Higher values consume more memory but offer a deeper safety net against persistent lag."));
                    EditorGUILayout.PropertyField(_minResumeHeadroomSecondsProp,
                        new GUIContent("Resume Headroom (s)",
                            "The minimum data cushion (in seconds) that must be received after a network stall before playback resumes. Lower values feel real-time but stutter heavily on bad connections."));
                    EditorGUI.indentLevel--;
                }
            });
        }

        private void ApplyLatencyPresetForMode(int latencyModeValue)
        {
            if (_maxBufferedSecondsProp == null || _minResumeHeadroomSecondsProp == null) return;

            switch ((LipSyncLatencyMode)latencyModeValue)
            {
                case LipSyncLatencyMode.UltraLowLatency:
                    _maxBufferedSecondsProp.floatValue = 1f;
                    _minResumeHeadroomSecondsProp.floatValue = 0.05f;
                    break;
                case LipSyncLatencyMode.Balanced:
                    _maxBufferedSecondsProp.floatValue = 3f;
                    _minResumeHeadroomSecondsProp.floatValue = 0.12f;
                    break;
                case LipSyncLatencyMode.NetworkSafe:
                    _maxBufferedSecondsProp.floatValue = 6f;
                    _minResumeHeadroomSecondsProp.floatValue = 0.25f;
                    break;
                case LipSyncLatencyMode.Custom:
                    break;
            }
        }

        #endregion

        #region Live Status Section

        /// <summary>Draws a progress bar for played and buffered portions of the active stream window.</summary>
        private static void DrawStreamProgressBar(float elapsed, float remaining)
        {
            const float barHeight = 10f;
            const float legendHeight = 14f;

            float total = elapsed + Mathf.Max(0f, remaining);
            if (total <= 0f) return;

            float playedRatio = elapsed / total;
            float bufferedRatio = remaining / total;

            Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(barHeight));
            float w = barRect.width;

            float x = barRect.x;
            if (playedRatio > 0f)
            {
                float segW = w * playedRatio;
                EditorGUI.DrawRect(new Rect(x, barRect.y, segW, barRect.height), ConvaiGreen);
                x += segW;
            }

            if (bufferedRatio > 0f)
            {
                float segW = w * bufferedRatio;
                EditorGUI.DrawRect(new Rect(x, barRect.y, segW, barRect.height), ConvaiInfo);
            }

            GUILayout.Space(2);
            Rect legendRect = GUILayoutUtility.GetRect(1f, legendHeight);
            float legendX = legendRect.x;
            float swatchSize = 8f;
            float gap = 6f;

            void DrawLegendSwatch(Color c, string label)
            {
                EditorGUI.DrawRect(
                    new Rect(legendX, legendRect.y + ((legendRect.height - swatchSize) * 0.5f), swatchSize, swatchSize),
                    c);
                legendX += swatchSize + 4f;
                GUI.Label(new Rect(legendX, legendRect.y, 120f, legendRect.height), label, EditorStyles.miniLabel);
                legendX += 52f + gap;
            }

            DrawLegendSwatch(ConvaiGreen, "Played");
            DrawLegendSwatch(ConvaiInfo, "Buffered");
        }

        private void DrawLiveStatusCell(string label, string value, int cellWidth, Color valueColor,
            bool isBoldValue = false)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(cellWidth));
            GUILayout.Label(label.ToUpper(), _liveCellLabelStyle);
            _liveCellValueStyle.fontStyle = isBoldValue ? FontStyle.Bold : FontStyle.Normal;
            _liveCellValueStyle.normal.textColor = valueColor;
            GUILayout.Label(value, _liveCellValueStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawLiveStatusSection()
        {
            _showLiveStatus = DrawSectionHeader(SectionLiveStatusId, "LIVE STATUS", _showLiveStatus, "\u25c9",
                ConvaiGreen, SectionIconFontSize);
            if (!_showLiveStatus) return;

            Color bgColor = _component.IsPlaying ? new Color(0.32f, 0.72f, 0.53f, 0.1f) : SectionBg;
            DrawSectionBackground(() =>
            {
                EditorGUILayout.BeginHorizontal();

                PlaybackState state = _component.EngineState;
                string statusEmoji;
                string statusText;
                Color statusColor;

                switch (state)
                {
                    case PlaybackState.FadingOut:
                        statusEmoji = "\u2198";
                        statusText = "FADING OUT";
                        statusColor = ConvaiWarning;
                        break;
                    case PlaybackState.Playing:
                        statusEmoji = "\u25b6";
                        statusText = "PLAYING";
                        statusColor = ConvaiGreen;
                        break;
                    case PlaybackState.Starving:
                        statusEmoji = "\u26a0";
                        statusText = "STARVING";
                        statusColor = ConvaiWarning;
                        break;
                    case PlaybackState.Buffering:
                        statusEmoji = "\u21bb";
                        statusText = "BUFFERING";
                        statusColor = ConvaiInfo;
                        break;
                    default:
                        statusEmoji = "\u25cf";
                        statusText = "IDLE";
                        statusColor = new Color(0.5f, 0.5f, 0.5f);
                        break;
                }

                GUI.color = statusColor;
                GUILayout.Label(statusEmoji, _liveHeaderIconStyle, GUILayout.Width(20));
                GUILayout.Label(statusText, _liveHeaderTextStyle);
                GUI.color = Color.white;

                GUILayout.FlexibleSpace();

                GUI.color = new Color(0.4f, 0.6f, 1f, 1f); // Convai Light Blue
                GUILayout.Label($"[{_component.ActiveProfile}]", _liveHeaderProfileStyle);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(12);

                float remaining = _component.GetTalkingTimeRemaining();
                float elapsed = _component.GetTalkingTimeElapsed();
                float totalStream = _component.GetTotalStreamDuration();
                float headroom = _component.GetHeadroom();
                float bufferWindow = _component.GetTotalBufferedDuration();

                float bufferWindowTotal = elapsed + Mathf.Max(0f, remaining);
                if (bufferWindowTotal > 0f)
                {
                    DrawStreamProgressBar(elapsed, remaining);
                    GUILayout.Space(6);
                }

                const int cellWidth = 90;
                var defaultValColor = new Color(0.85f, 0.85f, 0.85f);
                EditorGUILayout.BeginHorizontal();
                DrawLiveStatusCell("Elapsed Time", $"{elapsed:F2} s", cellWidth, defaultValColor);
                DrawLiveStatusCell("Remaining", $"{Mathf.Max(0f, remaining):F2} s", cellWidth, defaultValColor);
                DrawLiveStatusCell("Received Data", $"{totalStream:F2} s", cellWidth, defaultValColor);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();
                Color headroomColor = headroom > 0.1f ? ConvaiGreenLight : headroom > 0f ? ConvaiWarning : ConvaiError;
                DrawLiveStatusCell("Headroom", $"{headroom * 1000f:F0} ms", cellWidth, headroomColor, headroom < 0.1f);
                DrawLiveStatusCell("Buffer Size", $"{bufferWindow:F2} s", cellWidth, ConvaiInfo);

                string talkingStr = _component.IsTalking ? "Yes" : "No";
                Color talkingColor = _component.IsTalking ? ConvaiGreenLight : defaultValColor;
                DrawLiveStatusCell("Is Talking", talkingStr, cellWidth, talkingColor, _component.IsTalking);
                EditorGUILayout.EndHorizontal();
            }, bgColor);
        }

        #endregion

        #region Helper Methods

        private bool DrawSectionHeader(string sectionId, string title, bool isExpanded, string icon,
            Color? customColor = null, int? iconFontSize = null)
        {
            ConvaiInspectorSectionHeaderSpec headerSpec = new(
                EditorStateHostId,
                sectionId,
                title,
                icon,
                customColor ?? ConvaiGreen,
                iconFontSize ?? SectionIconFontSize);
            return ConvaiInspectorSectionChrome.DrawHeader(in headerSpec, isExpanded);
        }

        private void DrawSectionBackground(Action drawContent, Color? bgColor = null)
        {
            ConvaiInspectorSectionChrome.BeginBody(bgColor ?? SectionBg);
            drawContent?.Invoke();
            ConvaiInspectorSectionChrome.EndBody();
        }

        private void PopulateAssignedMeshes()
        {
            _tempMeshes.Clear();
            _seenMeshIds.Clear();

            if (_targetMeshesProp == null || !_targetMeshesProp.isArray) return;

            for (int i = 0; i < _targetMeshesProp.arraySize; i++)
            {
                var mesh = _targetMeshesProp.GetArrayElementAtIndex(i).objectReferenceValue as SkinnedMeshRenderer;
                if (mesh == null || !_seenMeshIds.Add(mesh.GetEntityId())) continue;
                _tempMeshes.Add(mesh);
            }
        }

        private int GetTotalUniqueBlendshapeCount()
        {
            _uniqueBlendshapes.Clear();
            for (int i = 0; i < _tempMeshes.Count; i++)
            {
                SkinnedMeshRenderer mesh = _tempMeshes[i];
                if (mesh == null || mesh.sharedMesh == null) continue;

                Mesh sharedMesh = mesh.sharedMesh;
                for (int j = 0; j < sharedMesh.blendShapeCount; j++)
                    _uniqueBlendshapes.Add(sharedMesh.GetBlendShapeName(j));
            }

            return _uniqueBlendshapes.Count;
        }

        private void AutoFindMeshesInHierarchy()
        {
            Undo.RecordObject(_component, "Auto-Find Lip Sync Meshes");
            AutoFindMeshes();
            EditorUtility.SetDirty(_component);
        }

        private void AutoFindMeshes()
        {
            Transform root = _component.transform;
            SkinnedMeshRenderer[] meshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var discovered = new List<SkinnedMeshRenderer>();
            var seen = new HashSet<EntityId>();
            foreach (SkinnedMeshRenderer mesh in meshes)
            {
                if (mesh == null || mesh.sharedMesh == null || mesh.sharedMesh.blendShapeCount == 0) continue;

                if (!seen.Add(mesh.GetEntityId())) continue;

                discovered.Add(mesh);
            }

            discovered.Sort((a, b) =>
            {
                int scoreA = GetMeshPriority(a != null ? a.name : string.Empty);
                int scoreB = GetMeshPriority(b != null ? b.name : string.Empty);
                if (scoreA != scoreB) return scoreA.CompareTo(scoreB);

                string nameA = a != null ? a.name : string.Empty;
                string nameB = b != null ? b.name : string.Empty;
                return string.CompareOrdinal(nameA, nameB);
            });

            _targetMeshesProp.ClearArray();
            for (int i = 0; i < discovered.Count; i++)
            {
                _targetMeshesProp.InsertArrayElementAtIndex(i);
                _targetMeshesProp.GetArrayElementAtIndex(i).objectReferenceValue = discovered[i];
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static int GetMeshPriority(string meshName)
        {
            string lowerName = meshName.ToLowerInvariant();
            if (lowerName.Contains("cc_base_body") || lowerName.Contains("skinhead") ||
                lowerName.Contains("head") || lowerName.Contains("face"))
                return 0;

            if (lowerName.Contains("teeth") || lowerName.Contains("tooth")) return 1;

            if (lowerName.Contains("tongue")) return 2;

            return 3;
        }

        private void CreateMappingAsset()
        {
            string defaultName = "ConvaiLipSyncMapAsset";
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Lip Sync Mapping",
                defaultName,
                "asset",
                "Create a lip sync mapping asset.");
            if (string.IsNullOrWhiteSpace(path)) return;

            var asset = CreateInstance<ConvaiLipSyncMapAsset>();
            var so = new SerializedObject(asset);
            SerializedProperty targetProfileId = so.FindProperty("_targetProfileId");
            if (targetProfileId != null) targetProfileId.stringValue = GetInspectorProfile().Value;
            so.ApplyModifiedPropertiesWithoutUndo();
            asset.InitializeWithDefaults();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _mappingProp.objectReferenceValue = asset;
            serializedObject.ApplyModifiedProperties();
            EditorGUIUtility.PingObject(asset);
        }

        private LipSyncProfileId GetInspectorProfile()
        {
            return _lockedProfileProp != null
                ? new LipSyncProfileId(_lockedProfileProp.stringValue)
                : _component.ActiveProfile;
        }

        private ConvaiLipSyncMapAsset GetProfileDefaultMap(LipSyncProfileId profile) =>
            _defaultMapRegistry != null ? _defaultMapRegistry.GetForProfile(profile) : null;

        private static string ToDisplayProfileName(LipSyncProfileId profile)
        {
            if (LipSyncProfileCatalog.TryGetProfile(profile, out ConvaiLipSyncProfile asset))
                return asset.DisplayName;

            return profile.IsValid ? profile.Value : "(none)";
        }

        private void DrawProfileSelector()
        {
            IReadOnlyList<ConvaiLipSyncProfile> profiles = LipSyncProfileCatalog.GetProfiles();
            string currentId = LipSyncProfileId.Normalize(_lockedProfileProp.stringValue);

            if (profiles == null || profiles.Count == 0)
            {
                EditorGUILayout.PropertyField(_lockedProfileProp, new GUIContent("Profile ID"));
                return;
            }

            string[] labels = new string[profiles.Count];
            int selectedIndex = -1;
            for (int i = 0; i < profiles.Count; i++)
            {
                ConvaiLipSyncProfile profile = profiles[i];
                labels[i] = profile.DisplayName;
                if (string.Equals(profile.ProfileId.Value, currentId, StringComparison.Ordinal)) selectedIndex = i;
            }

            int fallbackIndex = selectedIndex >= 0 ? selectedIndex : 0;
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup(
                new GUIContent("Profile", "Locked rig profile for this component."),
                fallbackIndex,
                labels);
            if (EditorGUI.EndChangeCheck() && nextIndex >= 0 && nextIndex < profiles.Count)
                _lockedProfileProp.stringValue = profiles[nextIndex].ProfileId.Value;

            if (selectedIndex < 0)
            {
                EditorGUILayout.HelpBox(
                    $"Profile id '{currentId}' is not registered. Select a valid profile from catalog.",
                    MessageType.Warning);
            }
        }

        #endregion
    }
}
#endif
