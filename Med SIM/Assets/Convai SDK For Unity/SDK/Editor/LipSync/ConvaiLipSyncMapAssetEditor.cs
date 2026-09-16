#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Convai.Domain.Logging;
using Convai.Domain.Models.LipSync;
using Convai.Editor.Inspectors;
using Convai.Editor.UI;
using Convai.Modules.LipSync.Profiles;
using Convai.Runtime.Logging;
using UnityEditor;
using UnityEngine;

namespace Convai.Modules.LipSync.Editor
{
    /// <summary>
    ///     Production inspector for <see cref="ConvaiLipSyncMapAsset" />.
    ///     Provides mapping authoring, validation signals, and bulk editing workflows.
    /// </summary>
    [CustomEditor(typeof(ConvaiLipSyncMapAsset))]
    internal sealed class ConvaiLipSyncMapAssetEditor : UnityEditor.Editor
    {
        private void DrawHeaderMetric(string label, string value)
        {
            EditorGUILayout.BeginVertical(_headerMetricCellStyle, GUILayout.Width(90), GUILayout.Height(30f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, _headerMetricValueStyle);
            GUILayout.Space(1f);
            GUILayout.Label(label, _headerMetricLabelStyle, GUILayout.Height(14f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private MappingStats BuildMappingStats()
        {
            int totalMappings = _mappingsProp != null ? _mappingsProp.arraySize : 0;
            int enabledCount = 0;
            int mappedEnabledCount = 0;

            for (int i = 0; i < totalMappings; i++)
            {
                SerializedProperty entry = _mappingsProp.GetArrayElementAtIndex(i);
                SerializedProperty enabledProp = entry.FindPropertyRelative("enabled");
                SerializedProperty targetNamesProp = entry.FindPropertyRelative("targetNames");

                if (enabledProp == null || !enabledProp.boolValue) continue;

                enabledCount++;
                if (targetNamesProp != null && targetNamesProp.arraySize > 0) mappedEnabledCount++;
            }

            float coverage = enabledCount > 0
                ? mappedEnabledCount / (float)enabledCount * 100f
                : 0f;
            return new MappingStats(
                totalMappings,
                enabledCount,
                mappedEnabledCount,
                enabledCount - mappedEnabledCount,
                coverage);
        }

        private static string GetProfileDisplayName(LipSyncProfileId profile)
        {
            if (LipSyncProfileCatalog.TryGetProfile(profile, out ConvaiLipSyncProfile profileAsset))
                return profileAsset.DisplayName;

            return profile.IsValid ? profile.Value : "(none)";
        }

        #region Configuration Section

        private void DrawConfigurationSection()
        {
            _showConfiguration = DrawSectionHeader(SectionConfigurationId, "CONFIGURATION", _showConfiguration, "⚙",
                iconFontSize: SectionIconFontSize);

            if (!_showConfiguration) return;

            DrawSectionBackground(() =>
            {
                DrawTargetProfileSelector();

                GUILayout.Space(4);

                EditorGUILayout.PropertyField(_descriptionProp, new GUIContent("Description"));

                GUILayout.Space(8);

                EditorGUILayout.LabelField("Global Modifiers", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_globalMultiplierProp,
                    new GUIContent("Multiplier", "Applied to all values"));
                EditorGUILayout.PropertyField(_globalOffsetProp, new GUIContent("Offset", "Added to all values"));
                EditorGUILayout.PropertyField(_allowUnmappedPassthroughProp,
                    new GUIContent("Allow Unmapped", "Pass through Source Blendshapes not in this list"));
                EditorGUI.indentLevel--;
            });
        }

        #endregion

        #region Tools Section

        private void DrawToolsSection()
        {
            _showTools = DrawSectionHeader(SectionToolsId, "TOOLS", _showTools, "🔧",
                iconFontSize: SectionIconFontSize);

            if (!_showTools) return;

            DrawSectionBackground(() =>
            {
                bool hasPreviewMesh = HasPreviewMeshForDropdown();

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("ADD BLENDSHAPES", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    "Choose one input method for creating mapping entries.",
                    EditorStyles.wordWrappedMiniLabel);

                GUILayout.Space(6);
                EditorGUILayout.LabelField("From Mesh (Auto-Detect)", EditorStyles.miniBoldLabel);
                DrawPreviewMeshListEditor();

                int totalBlendshapes = GetPreviewMeshBlendshapeCount();
                if (totalBlendshapes >= 0)
                {
                    GUI.color = totalBlendshapes > 0 ? ConvaiGreen : ConvaiWarning;
                    EditorGUILayout.LabelField($"Detected Blendshapes: {totalBlendshapes}", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }

                EditorGUI.BeginDisabledGroup(!hasPreviewMesh);
                GUI.backgroundColor = ConvaiInfo;
                if (GUILayout.Button("Auto-Detect From Mesh", GUILayout.Height(24))) ShowAutoDetectMenu();
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup();

                GUILayout.Space(6);
                EditorGUILayout.LabelField("From Mapping Text", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = ConvaiGreenLight;
                if (GUILayout.Button("Import Mapping File...", _miniButtonStyle)) ImportMappingFromFile();
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Paste Mapping Text", _miniButtonStyle)) ImportMappingFromClipboard();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                GUILayout.Space(4);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("MAPPING ACTIONS", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = ConvaiGreen;
                if (GUILayout.Button("Initialize Defaults", GUILayout.Height(24)))
                {
                    if (EditorUtility.DisplayDialog(
                            "Initialize Defaults",
                            "This will clear all existing mappings and create default entries for the current profile. Continue?",
                            "Yes", "Cancel"))
                    {
                        Undo.RecordObject(_mapping, "Initialize Lip Sync Defaults");
                        _mapping.InitializeWithDefaults();
                        EditorUtility.SetDirty(_mapping);
                    }
                }

                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Clear All", _miniButtonStyle))
                {
                    if (EditorUtility.DisplayDialog(
                            "Clear All Mappings",
                            "This will remove all mapping entries. Continue?",
                            "Yes", "Cancel"))
                    {
                        Undo.RecordObject(_mapping, "Clear Lip Sync Mappings");
                        _mapping.ClearMappings();
                        EditorUtility.SetDirty(_mapping);
                    }
                }

                if (GUILayout.Button("Sort A-Z", _miniButtonStyle)) SortMappings();

                if (GUILayout.Button("Copy Mapping JSON", _miniButtonStyle)) CopyMappingAsJsonToClipboard();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            });
        }

        #endregion

        private void DrawPreviewMeshListEditor()
        {
            _previewMeshes ??= new List<SkinnedMeshRenderer>();

            for (int i = 0; i < _previewMeshes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                float labelWidth = GetDynamicLabelWidth($"Preview Mesh {i + 1}");
                Rect rowRect = EditorGUILayout.GetControlRect();
                var labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
                var fieldRect = new Rect(labelRect.xMax + 4f, rowRect.y, rowRect.width - labelWidth - 32f,
                    rowRect.height);
                var removeRect = new Rect(fieldRect.xMax + 4f, rowRect.y, 28f, rowRect.height);

                EditorGUI.LabelField(labelRect, $"Preview Mesh {i + 1}");
                EditorGUI.BeginChangeCheck();
                _previewMeshes[i] = (SkinnedMeshRenderer)EditorGUI.ObjectField(fieldRect, _previewMeshes[i],
                    typeof(SkinnedMeshRenderer), true);
                if (EditorGUI.EndChangeCheck()) _meshNamesNeedRefresh = true;

                if (GUI.Button(removeRect, "X"))
                {
                    _previewMeshes.RemoveAt(i);
                    _meshNamesNeedRefresh = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Preview Mesh", _miniButtonStyle, GUILayout.Width(130)))
            {
                _previewMeshes.Add(null);
                _meshNamesNeedRefresh = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        private static float GetDynamicLabelWidth(string label)
        {
            float textWidth = EditorStyles.label.CalcSize(new GUIContent(label)).x + 48f;
            return Mathf.Clamp(textWidth, PreviewMeshLabelMinWidth, PreviewMeshLabelMaxWidth);
        }

        #region Bulk Operations Section

        private void DrawBulkOperationsSection()
        {
            _showBulkOps = DrawSectionHeader(SectionBulkOperationsId, "BULK OPERATIONS", _showBulkOps, "⚡",
                iconFontSize: SectionIconFontSize);

            if (!_showBulkOps) return;

            DrawSectionBackground(() =>
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Enable All", _miniButtonStyle)) SetAllEnabled(true);

                if (GUILayout.Button("Disable All", _miniButtonStyle)) SetAllEnabled(false);

                if (GUILayout.Button("Reset Multipliers", _miniButtonStyle)) ResetAllMultipliers();

                if (GUILayout.Button("Reset Offsets", _miniButtonStyle)) ResetAllOffsets();

                if (GUILayout.Button("Reset Curves", _miniButtonStyle)) ResetAllCurveExponents();

                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Enable Eyes Only", _miniButtonStyle))
                    EnableCategory(new[] { "Eye", "Blink", "Look", "Squint", "Wide" });

                if (GUILayout.Button("Enable Mouth Only", _miniButtonStyle))
                    EnableCategory(new[] { "Mouth", "Jaw", "Lip", "Smile", "Frown" });

                if (GUILayout.Button("Enable Brows Only", _miniButtonStyle)) EnableCategory(new[] { "Brow" });

                EditorGUILayout.EndHorizontal();
            });
        }

        #endregion

        private readonly struct MappingStats
        {
            public MappingStats(
                int totalCount,
                int enabledCount,
                int mappedEnabledCount,
                int unmappedEnabledCount,
                float coveragePercent)
            {
                TotalCount = totalCount;
                EnabledCount = enabledCount;
                MappedEnabledCount = mappedEnabledCount;
                UnmappedEnabledCount = Mathf.Max(0, unmappedEnabledCount);
                CoveragePercent = coveragePercent;
            }

            public int TotalCount { get; }
            public int EnabledCount { get; }
            public int MappedEnabledCount { get; }
            public int UnmappedEnabledCount { get; }
            public float CoveragePercent { get; }
        }

        #region Constants & Colors

        private static readonly Color ConvaiGreen = ConvaiInspectorThemeTokens.Accent;
        private static readonly Color ConvaiGreenLight = ConvaiInspectorThemeTokens.AccentEmphasis;
        private static readonly Color ConvaiWarning = ConvaiInspectorThemeTokens.Warning;
        private static readonly Color ConvaiError = ConvaiInspectorThemeTokens.Error;
        private static readonly Color ConvaiInfo = ConvaiInspectorThemeTokens.Info;

        private static readonly Color HeaderBg = ConvaiInspectorThemeTokens.HeaderBackground;
        private static readonly Color SectionBg = ConvaiInspectorThemeTokens.SectionBackground;
        private static readonly Color RowAltBg = ConvaiInspectorThemeTokens.AlternateRowBackground;
        private const int SectionIconFontSize = ConvaiInspectorThemeTokens.SectionIconFontSize;
        private const float PreviewMeshLabelMinWidth = 220f;
        private const float PreviewMeshLabelMaxWidth = 360f;
        private const float SourceBlendshapeColumnWidth = 230f;
        private const float MappingTableLeadingPadding = 4f;
        private const float MappingTableToggleColumnWidth = 20f;
        private const float MappingTableArrowColumnWidth = 20f;
        private const float MappingTableNumericColumnWidth = 45f;
        private const float MappingTableExpandButtonWidth = 24f;
        private const float MappingTableDeleteButtonWidth = 22f;
        private const float MappingTableHeaderHeight = 24f;
        private const float MappingTableRowHeight = 22f;
        private const int SourceBlendshapeTruncateLength = 32;
        private const string EditorStateHostId = "MapAssetEditor";
        private const string SectionConfigurationId = "Configuration";
        private const string SectionToolsId = "Tools";
        private const string SectionMappingsId = "Mappings";
        private const string SectionBulkOperationsId = "BulkOperations";
        private const float MappingsScrollMinHeight = 460f;
        private const float MappingsScrollMaxHeight = 560f;

        #endregion

        #region Private Fields

        private ConvaiLipSyncMapAsset _mapping;
        private Texture2D _convaiIcon;

        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private bool _showOnlyUnmapped;
        private bool _showOnlyEnabled = true;
        private List<SkinnedMeshRenderer> _previewMeshes = new();

        private bool _showConfiguration = true;
        private bool _showTools = true;
        private bool _showMappings = true;
        private bool _showBulkOps;

        private SerializedProperty _targetProfileProp;
        private SerializedProperty _descriptionProp;
        private SerializedProperty _mappingsProp;
        private SerializedProperty _globalMultiplierProp;
        private SerializedProperty _globalOffsetProp;
        private SerializedProperty _allowUnmappedPassthroughProp;

        private List<string> _meshBlendshapeNames;
        private bool _meshNamesNeedRefresh = true;

        private GUIStyle _headerStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _miniButtonStyle;
        private GUIStyle _searchFieldStyle;
        private GUIStyle _headerMetricValueStyle;
        private GUIStyle _headerMetricLabelStyle;
        private GUIStyle _headerIssueStyle;
        private GUIStyle _headerMetricCellStyle;
        private GUIStyle _mappingTableHeaderStyle;
        private GUIStyle _mappingTableHeaderCenteredStyle;
        private GUIStyle _emptyFilterStateStyle;
        private bool _stylesInitialized;

        /// <summary>Array index of the mapping entry whose advanced options are expanded. -1 when none.</summary>
        private int _expandedMappingIndex = -1;

        #endregion

        #region Unity Editor Lifecycle

        private void OnEnable()
        {
            _mapping = (ConvaiLipSyncMapAsset)target;
            _convaiIcon = ConvaiBrandedIconProvider.GetConvaiIcon();
            CacheSerializedProperties();
            _showConfiguration = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionConfigurationId, true);
            _showTools = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionToolsId, true);
            _showMappings = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionMappingsId, true);
            _showBulkOps = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionBulkOperationsId, false);
        }

        private void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionConfigurationId, _showConfiguration);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionToolsId, _showTools);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionMappingsId, _showMappings);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionBulkOperationsId, _showBulkOps);
        }

        private void CacheSerializedProperties()
        {
            _targetProfileProp = serializedObject.FindProperty("_targetProfileId");
            _descriptionProp = serializedObject.FindProperty("_description");
            _mappingsProp = serializedObject.FindProperty("_mappings");
            _globalMultiplierProp = serializedObject.FindProperty("_globalMultiplier");
            _globalOffsetProp = serializedObject.FindProperty("_globalOffset");
            _allowUnmappedPassthroughProp = serializedObject.FindProperty("_allowUnmappedPassthrough");
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized &&
                _headerStyle != null &&
                _sectionHeaderStyle != null &&
                _miniButtonStyle != null &&
                _searchFieldStyle != null &&
                _headerMetricValueStyle != null &&
                _headerMetricLabelStyle != null &&
                _headerIssueStyle != null &&
                _headerMetricCellStyle != null &&
                _mappingTableHeaderStyle != null &&
                _mappingTableHeaderCenteredStyle != null &&
                _emptyFilterStateStyle != null)
                return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14, alignment = TextAnchor.MiddleLeft, normal = { textColor = ConvaiGreenLight }
            };

            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11, normal = { textColor = ConvaiGreen }
            };

            _miniButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10, padding = new RectOffset(8, 8, 3, 3)
            };

            _searchFieldStyle = new GUIStyle(EditorStyles.toolbarSearchField) { fixedHeight = 20 };

            _headerMetricValueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 15
            };
            _headerMetricValueStyle.normal.textColor = ConvaiGreenLight;

            _headerMetricLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            _headerMetricLabelStyle.normal.textColor = new Color(0.72f, 0.72f, 0.76f);

            _headerIssueStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight };

            _headerMetricCellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };

            _mappingTableHeaderStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 24,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };
            _mappingTableHeaderStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            _mappingTableHeaderCenteredStyle = new GUIStyle(_mappingTableHeaderStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _emptyFilterStateStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _stylesInitialized = true;
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();
            serializedObject.Update();

            DrawInspectorHeader();
            EditorGUILayout.Space(6);

            DrawConfigurationSection();
            DrawToolsSection();
            DrawBulkOperationsSection();
            DrawMappingsSection();

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region Header

        private void DrawInspectorHeader()
        {
            MappingStats stats = BuildMappingStats();
            LipSyncProfileId selectedProfile = _targetProfileProp != null
                ? new LipSyncProfileId(_targetProfileProp.stringValue)
                : LipSyncProfileId.ARKit;
            string profileDisplayName = GetProfileDisplayName(selectedProfile);
            const float headerHeight = 58f;
            const float iconSize = 22f;
            const float iconTextSpacing = 6f;
            const float metricCellWidth = 72f;
            const float metricCellGap = 4f;
            const float metricValueHeight = 16f;
            const float metricLabelHeight = 12f;
            const float metricValueLabelGap = 0f;
            float metricGroupHeight = metricValueHeight + metricValueLabelGap + metricLabelHeight;
            const float leftRegionWidth = 300f;

            Rect headerRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(
                new Rect(headerRect.x - 18f, headerRect.y - 4f, headerRect.width + 36f, headerHeight + 4f), HeaderBg);

            EditorGUILayout.BeginVertical(GUILayout.Height(headerHeight));
            Rect rowRect = GUILayoutUtility.GetRect(0f, headerHeight, GUILayout.ExpandWidth(true),
                GUILayout.Height(headerHeight));
            float profileWidth = Mathf.Max(
                120f,
                _headerIssueStyle.CalcSize(new GUIContent(profileDisplayName)).x + 8f);
            var leftRegionRect = new Rect(rowRect.x, rowRect.y, Mathf.Min(leftRegionWidth, rowRect.width),
                rowRect.height);
            var rightRegionRect = new Rect(rowRect.xMax - profileWidth, rowRect.y, profileWidth, rowRect.height);
            var centerRegionRect = new Rect(
                leftRegionRect.xMax,
                rowRect.y,
                Mathf.Max(0f, rightRegionRect.xMin - leftRegionRect.xMax),
                rowRect.height);

            var iconRect = new Rect(
                leftRegionRect.x,
                leftRegionRect.y + ((leftRegionRect.height - iconSize) * 0.5f),
                iconSize,
                iconSize);
            if (_convaiIcon != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(iconRect, _convaiIcon, ScaleMode.ScaleToFit, true);
            var titleRect = new Rect(
                iconRect.xMax + iconTextSpacing,
                leftRegionRect.y,
                Mathf.Max(0f, leftRegionRect.width - (iconSize + iconTextSpacing)),
                leftRegionRect.height);
            GUI.Label(titleRect, "Lip Sync Mapping", _headerStyle);

            _headerIssueStyle.normal.textColor = ConvaiGreenLight;
            GUI.Label(rightRegionRect, profileDisplayName, _headerIssueStyle);

            float totalMetricsWidth = (metricCellWidth * 4f) + (metricCellGap * 3f);
            float idealMetricsStartX = rowRect.center.x - (totalMetricsWidth * 0.5f);
            float minMetricsStartX = leftRegionRect.xMax + 8f;
            float maxMetricsStartX = rightRegionRect.xMin - totalMetricsWidth - 8f;
            float metricsStartX = Mathf.Clamp(idealMetricsStartX, minMetricsStartX,
                Mathf.Max(minMetricsStartX, maxMetricsStartX));
            float metricsStartY =
                centerRegionRect.y + Mathf.Max(0f, (centerRegionRect.height - metricGroupHeight) * 0.5f);

            DrawMetricCell(
                new Rect(metricsStartX + ((metricCellWidth + metricCellGap) * 0f), metricsStartY, metricCellWidth,
                    metricGroupHeight), "Total", stats.TotalCount.ToString(), metricValueHeight, metricValueLabelGap,
                metricLabelHeight);
            DrawMetricCell(
                new Rect(metricsStartX + ((metricCellWidth + metricCellGap) * 1f), metricsStartY, metricCellWidth,
                    metricGroupHeight), "Enabled", stats.EnabledCount.ToString(), metricValueHeight,
                metricValueLabelGap, metricLabelHeight);
            DrawMetricCell(
                new Rect(metricsStartX + ((metricCellWidth + metricCellGap) * 2f), metricsStartY, metricCellWidth,
                    metricGroupHeight), "Mapped", $"{stats.MappedEnabledCount}/{stats.EnabledCount}", metricValueHeight,
                metricValueLabelGap, metricLabelHeight);
            DrawMetricCell(
                new Rect(metricsStartX + ((metricCellWidth + metricCellGap) * 3f), metricsStartY, metricCellWidth,
                    metricGroupHeight), "Coverage", $"{stats.CoveragePercent:0.#}%", metricValueHeight,
                metricValueLabelGap, metricLabelHeight);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }

        private void DrawMetricCell(Rect cellRect, string label, string value, float valueHeight, float valueLabelGap,
            float labelHeight)
        {
            var valueRect = new Rect(cellRect.x, cellRect.y, cellRect.width, valueHeight);
            var labelRect = new Rect(cellRect.x, valueRect.yMax + valueLabelGap, cellRect.width, labelHeight);
            GUI.Label(valueRect, value, _headerMetricValueStyle);
            GUI.Label(labelRect, label, _headerMetricLabelStyle);
        }

        #endregion

        #region Mappings Section

        private void DrawMappingsSection()
        {
            _showMappings = DrawSectionHeader(SectionMappingsId, "MAPPINGS", _showMappings, "⇄",
                iconFontSize: SectionIconFontSize);

            if (!_showMappings) return;

            DrawSectionBackground(() =>
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                int savedIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(-4f);

                _searchFilter =
                    EditorGUILayout.TextField(_searchFilter, _searchFieldStyle, GUILayout.ExpandWidth(true));

                if (GUILayout.Button("x", GUILayout.Width(20)))

                {
                    _searchFilter = "";
                    GUI.FocusControl(null);
                }

                GUILayout.Space(10);

                _showOnlyUnmapped = GUILayout.Toggle(_showOnlyUnmapped, "Unmapped", EditorStyles.miniButton,
                    GUILayout.Width(70));
                _showOnlyEnabled = GUILayout.Toggle(_showOnlyEnabled, "Enabled", EditorStyles.miniButton,
                    GUILayout.Width(60));

                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                DrawAlignedMappingHeader();

                _scrollPosition = EditorGUILayout.BeginScrollView(
                    _scrollPosition,
                    GUILayout.MinHeight(MappingsScrollMinHeight),
                    GUILayout.MaxHeight(MappingsScrollMaxHeight));

                int visibleCount = 0;
                for (int i = 0; i < _mappingsProp.arraySize; i++)
                {
                    SerializedProperty entry = _mappingsProp.GetArrayElementAtIndex(i);

                    if (ShouldShowEntry(entry))
                    {
                        DrawAlignedMappingEntry(entry, i, visibleCount % 2 == 1);
                        visibleCount++;
                    }
                }

                if (visibleCount == 0)
                {
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField("No mappings match the current filter", _emptyFilterStateStyle);
                    GUILayout.Space(20);
                }
                else
                {
                    // Add bottom padding so the final row remains fully visible.
                    GUILayout.Space(6);
                }

                EditorGUILayout.EndScrollView();

                GUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                GUI.backgroundColor = ConvaiGreen;
                if (GUILayout.Button("+ Add Entry", GUILayout.Width(100), GUILayout.Height(22))) AddNewEntry();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
                EditorGUI.indentLevel = savedIndent;
                EditorGUILayout.EndVertical();
            });
        }

        private void DrawMappingHeader()
        {
            if (_mappingTableHeaderStyle == null || _mappingTableHeaderCenteredStyle == null)
            {
                _stylesInitialized = false;
                InitializeStyles();
            }

            var headerBg = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            Rect headerRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(24));
            EditorGUI.DrawRect(headerRect, headerBg);

            GUIStyle fallbackStyle = EditorStyles.miniLabel ?? GUI.skin?.label;
            GUIStyle headerLeftStyle = _mappingTableHeaderStyle ?? fallbackStyle;
            GUIStyle headerCenteredStyle = _mappingTableHeaderCenteredStyle ?? headerLeftStyle;

            if (headerLeftStyle == null)
            {
                headerLeftStyle = GUIStyle.none;
                headerCenteredStyle = GUIStyle.none;
            }

            GUILayout.Space(4);
            GUILayout.Label("✓", headerCenteredStyle, GUILayout.Width(20));
            GUILayout.Label("Source Blendshape", headerLeftStyle, GUILayout.Width(SourceBlendshapeColumnWidth));
            GUILayout.Label("→", headerCenteredStyle, GUILayout.Width(20));
            GUILayout.Label("Target Name(s)", headerLeftStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label("Mult", headerCenteredStyle, GUILayout.Width(45));
            GUILayout.Label("Offs", headerCenteredStyle, GUILayout.Width(45));
            GUILayout.Label("", headerCenteredStyle, GUILayout.Width(24));
            GUILayout.Space(25);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMappingEntry(SerializedProperty entry, int index, bool altRow)
        {
            SerializedProperty sourceBlendshapeProp = entry.FindPropertyRelative("sourceBlendshape");
            SerializedProperty targetNamesProp = entry.FindPropertyRelative("targetNames");
            SerializedProperty multiplierProp = entry.FindPropertyRelative("multiplier");
            SerializedProperty offsetProp = entry.FindPropertyRelative("offset");
            SerializedProperty enabledProp = entry.FindPropertyRelative("enabled");

            string sourceBlendshape = sourceBlendshapeProp?.stringValue ?? "";
            bool isEnabled = enabledProp?.boolValue ?? true;
            bool hasTarget = targetNamesProp != null && targetNamesProp.arraySize > 0;

            Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(22));

            if (altRow) EditorGUI.DrawRect(rowRect, RowAltBg);

            Color statusColor = !isEnabled ? new Color(0.4f, 0.4f, 0.4f) :
                hasTarget ? ConvaiGreen : ConvaiWarning;
            var statusRect = new Rect(rowRect.x + 2, rowRect.y + 7, 8, 8);
            EditorGUI.DrawRect(statusRect, statusColor);

            GUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(20));
            if (EditorGUI.EndChangeCheck() && enabledProp != null) enabledProp.boolValue = newEnabled;

            string displayName = sourceBlendshape.Length > SourceBlendshapeTruncateLength
                ? sourceBlendshape.Substring(0, SourceBlendshapeTruncateLength - 3) + "..."
                : sourceBlendshape;
            GUILayout.Label(new GUIContent(displayName, sourceBlendshape), EditorStyles.textField,
                GUILayout.Width(SourceBlendshapeColumnWidth));

            GUILayout.Label("→", GUILayout.Width(20));

            if (HasPreviewMeshForDropdown())
                DrawTargetDropdown(targetNamesProp);
            else
            {
                string currentTarget = targetNamesProp != null && targetNamesProp.arraySize > 0
                    ? targetNamesProp.GetArrayElementAtIndex(0).stringValue
                    : "";

                EditorGUI.BeginChangeCheck();
                string newTarget = EditorGUILayout.TextField(currentTarget, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && targetNamesProp != null)
                {
                    if (targetNamesProp.arraySize == 0) targetNamesProp.InsertArrayElementAtIndex(0);
                    targetNamesProp.GetArrayElementAtIndex(0).stringValue = newTarget;
                }
            }

            if (multiplierProp != null)
            {
                EditorGUI.BeginChangeCheck();
                float newMult = EditorGUILayout.FloatField(multiplierProp.floatValue, GUILayout.Width(45));
                if (EditorGUI.EndChangeCheck()) multiplierProp.floatValue = Mathf.Clamp(newMult, 0f, 5f);
            }

            if (offsetProp != null)
            {
                EditorGUI.BeginChangeCheck();
                float newOffset = EditorGUILayout.FloatField(offsetProp.floatValue, GUILayout.Width(45));
                if (EditorGUI.EndChangeCheck()) offsetProp.floatValue = Mathf.Clamp(newOffset, -1f, 1f);
            }

            bool isExpanded = _expandedMappingIndex == index;
            if (GUILayout.Button(isExpanded ? "▼" : "▶", _miniButtonStyle, GUILayout.Width(24), GUILayout.Height(18)))
                _expandedMappingIndex = isExpanded ? -1 : index;

            GUI.color = ConvaiError;
            if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
            {
                if (_expandedMappingIndex == index)
                    _expandedMappingIndex = -1;
                else if (_expandedMappingIndex > index) _expandedMappingIndex--;
                _mappingsProp.DeleteArrayElementAtIndex(index);
            }

            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            if (_expandedMappingIndex == index) DrawMappingEntryAdvanced(entry, altRow);
        }

        /// <summary>Draws per-entry advanced options: clamp, override value, ignore global modifiers.</summary>
        private void DrawMappingEntryAdvanced(SerializedProperty entry, bool altRow)
        {
            SerializedProperty useOverrideValueProp = entry.FindPropertyRelative("useOverrideValue");
            SerializedProperty overrideValueProp = entry.FindPropertyRelative("overrideValue");
            SerializedProperty ignoreGlobalModifiersProp = entry.FindPropertyRelative("ignoreGlobalModifiers");
            SerializedProperty curveExponentProp = entry.FindPropertyRelative("curveExponent");
            SerializedProperty clampMinValueProp = entry.FindPropertyRelative("clampMinValue");
            SerializedProperty clampMaxValueProp = entry.FindPropertyRelative("clampMaxValue");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(28);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Per-blendshape overrides", EditorStyles.miniBoldLabel);

            EditorGUI.indentLevel++;

            if (curveExponentProp != null)
            {
                EditorGUILayout.PropertyField(curveExponentProp,
                    new GUIContent("Response Curve",
                        "Exponent applied to the source value before gain. 1 = linear. " +
                        "Below 1 lifts the mid-range for clearer, more intentional articulation; " +
                        "above 1 suppresses low-level noise."));
            }

            if (clampMinValueProp != null)
            {
                EditorGUILayout.PropertyField(clampMinValueProp,
                    new GUIContent("Clamp Min", "Minimum output value for this blendshape."));
            }

            if (clampMaxValueProp != null)
            {
                EditorGUILayout.PropertyField(clampMaxValueProp,
                    new GUIContent("Clamp Max", "Maximum output value for this blendshape."));
            }

            if (ignoreGlobalModifiersProp != null)
            {
                EditorGUILayout.PropertyField(ignoreGlobalModifiersProp,
                    new GUIContent("Ignore Global Modifiers",
                        "Use only this entry's multiplier/offset, not the asset's global multiplier/offset."));
            }

            if (useOverrideValueProp != null)
            {
                EditorGUILayout.PropertyField(useOverrideValueProp,
                    new GUIContent("Use Override Value",
                        "When enabled, output a fixed value instead of the animated value."));
            }

            if (overrideValueProp != null && useOverrideValueProp != null && useOverrideValueProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(overrideValueProp, new GUIContent("Override Value"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
        }

        private void DrawTargetDropdown(SerializedProperty targetNamesProp)
        {
            EnsureMeshNamesCache();

            var options = new List<string> { "(None)" };
            options.AddRange(_meshBlendshapeNames ?? new List<string>());

            string currentValue = targetNamesProp != null && targetNamesProp.arraySize > 0
                ? targetNamesProp.GetArrayElementAtIndex(0).stringValue
                : "";

            int currentIndex = string.IsNullOrEmpty(currentValue) ? 0 : options.IndexOf(currentValue);
            if (currentIndex < 0) currentIndex = 0;

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(currentIndex, options.ToArray(), GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck() && targetNamesProp != null)
            {
                string newValue = newIndex == 0 ? "" : options[newIndex];

                if (string.IsNullOrEmpty(newValue))
                    targetNamesProp.ClearArray();
                else
                {
                    if (targetNamesProp.arraySize == 0) targetNamesProp.InsertArrayElementAtIndex(0);
                    targetNamesProp.GetArrayElementAtIndex(0).stringValue = newValue;
                }
            }
        }

        private void DrawAlignedMappingHeader()
        {
            if (_mappingTableHeaderStyle == null || _mappingTableHeaderCenteredStyle == null)
            {
                _stylesInitialized = false;
                InitializeStyles();
            }

            Rect headerRect = GUILayoutUtility.GetRect(0f, MappingTableHeaderHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, new Color(0.15f, 0.15f, 0.15f, 0.8f));

            Rect alignedHeaderRect = headerRect;
            alignedHeaderRect.width = Mathf.Max(0f, alignedHeaderRect.width - GetMappingScrollbarWidth());

            GUIStyle fallbackStyle = EditorStyles.miniLabel ?? GUI.skin?.label ?? GUIStyle.none;
            GUIStyle headerLeftStyle = _mappingTableHeaderStyle ?? fallbackStyle;
            GUIStyle headerCenteredStyle = _mappingTableHeaderCenteredStyle ?? headerLeftStyle;
            float textInset = GetInputContentLeftInset();

            GetMappingColumnRects(
                alignedHeaderRect,
                out Rect toggleRect,
                out Rect sourceRect,
                out Rect arrowRect,
                out Rect targetRect,
                out Rect multiplierRect,
                out Rect offsetRect,
                out Rect expandRect,
                out Rect deleteRect);

            GUI.Label(toggleRect, "\u2713", headerCenteredStyle);
            GUI.Label(InsetRect(sourceRect, textInset), "Source Blendshape", headerLeftStyle);
            GUI.Label(arrowRect, "\u2192", headerCenteredStyle);
            GUI.Label(InsetRect(targetRect, textInset), "Target Name(s)", headerLeftStyle);
            GUI.Label(multiplierRect, "Mult", headerCenteredStyle);
            GUI.Label(offsetRect, "Offs", headerCenteredStyle);
            GUI.Label(expandRect, string.Empty, headerCenteredStyle);
            GUI.Label(deleteRect, string.Empty, headerCenteredStyle);
        }

        private void DrawAlignedMappingEntry(SerializedProperty entry, int index, bool altRow)
        {
            SerializedProperty sourceBlendshapeProp = entry.FindPropertyRelative("sourceBlendshape");
            SerializedProperty targetNamesProp = entry.FindPropertyRelative("targetNames");
            SerializedProperty multiplierProp = entry.FindPropertyRelative("multiplier");
            SerializedProperty offsetProp = entry.FindPropertyRelative("offset");
            SerializedProperty enabledProp = entry.FindPropertyRelative("enabled");

            string sourceBlendshape = sourceBlendshapeProp?.stringValue ?? string.Empty;
            bool isEnabled = enabledProp?.boolValue ?? true;
            bool hasTarget = targetNamesProp != null && targetNamesProp.arraySize > 0;

            Rect rowRect = GUILayoutUtility.GetRect(0f, MappingTableRowHeight, GUILayout.ExpandWidth(true));
            if (altRow) EditorGUI.DrawRect(rowRect, RowAltBg);

            Color statusColor = !isEnabled
                ? new Color(0.4f, 0.4f, 0.4f)
                : hasTarget
                    ? ConvaiGreen
                    : ConvaiWarning;
            EditorGUI.DrawRect(new Rect(rowRect.x + 2f, rowRect.y + 7f, 8f, 8f), statusColor);

            GetMappingColumnRects(
                rowRect,
                out Rect toggleRect,
                out Rect sourceRect,
                out Rect arrowRect,
                out Rect targetRect,
                out Rect multiplierRect,
                out Rect offsetRect,
                out Rect expandRect,
                out Rect deleteRect);

            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUI.Toggle(toggleRect, isEnabled);
            if (EditorGUI.EndChangeCheck() && enabledProp != null) enabledProp.boolValue = newEnabled;

            string displayName = sourceBlendshape.Length > SourceBlendshapeTruncateLength
                ? sourceBlendshape.Substring(0, SourceBlendshapeTruncateLength - 3) + "..."
                : sourceBlendshape;
            GUI.Label(sourceRect, new GUIContent(displayName, sourceBlendshape), EditorStyles.textField);
            GUI.Label(arrowRect, "\u2192", _mappingTableHeaderCenteredStyle ?? EditorStyles.label);

            if (HasPreviewMeshForDropdown())
                DrawTargetDropdown(targetRect, targetNamesProp);
            else
            {
                string currentTarget = targetNamesProp != null && targetNamesProp.arraySize > 0
                    ? targetNamesProp.GetArrayElementAtIndex(0).stringValue
                    : string.Empty;

                EditorGUI.BeginChangeCheck();
                string newTarget = EditorGUI.TextField(targetRect, currentTarget);
                if (EditorGUI.EndChangeCheck() && targetNamesProp != null)
                {
                    if (targetNamesProp.arraySize == 0) targetNamesProp.InsertArrayElementAtIndex(0);

                    targetNamesProp.GetArrayElementAtIndex(0).stringValue = newTarget;
                }
            }

            if (multiplierProp != null)
            {
                EditorGUI.BeginChangeCheck();
                float newMult = EditorGUI.FloatField(multiplierRect, multiplierProp.floatValue);
                if (EditorGUI.EndChangeCheck()) multiplierProp.floatValue = Mathf.Clamp(newMult, 0f, 5f);
            }

            if (offsetProp != null)
            {
                EditorGUI.BeginChangeCheck();
                float newOffset = EditorGUI.FloatField(offsetRect, offsetProp.floatValue);
                if (EditorGUI.EndChangeCheck()) offsetProp.floatValue = Mathf.Clamp(newOffset, -1f, 1f);
            }

            bool isExpanded = _expandedMappingIndex == index;
            if (GUI.Button(expandRect, isExpanded ? "\u25BC" : "\u25B6", _miniButtonStyle))
                _expandedMappingIndex = isExpanded ? -1 : index;

            Color previousColor = GUI.color;
            GUI.color = ConvaiError;
            if (GUI.Button(deleteRect, "\u2715", _miniButtonStyle))
            {
                if (_expandedMappingIndex == index)
                    _expandedMappingIndex = -1;
                else if (_expandedMappingIndex > index) _expandedMappingIndex--;

                _mappingsProp.DeleteArrayElementAtIndex(index);
            }

            GUI.color = previousColor;

            if (_expandedMappingIndex == index) DrawMappingEntryAdvanced(entry, altRow);
        }

        private void DrawTargetDropdown(Rect rect, SerializedProperty targetNamesProp)
        {
            EnsureMeshNamesCache();

            var options = new List<string> { "(None)" };
            options.AddRange(_meshBlendshapeNames ?? new List<string>());

            string currentValue = targetNamesProp != null && targetNamesProp.arraySize > 0
                ? targetNamesProp.GetArrayElementAtIndex(0).stringValue
                : string.Empty;

            int currentIndex = string.IsNullOrEmpty(currentValue) ? 0 : options.IndexOf(currentValue);
            if (currentIndex < 0) currentIndex = 0;

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(rect, currentIndex, options.ToArray());
            if (EditorGUI.EndChangeCheck() && targetNamesProp != null)
            {
                string newValue = newIndex == 0 ? string.Empty : options[newIndex];
                if (string.IsNullOrEmpty(newValue))
                    targetNamesProp.ClearArray();
                else
                {
                    if (targetNamesProp.arraySize == 0) targetNamesProp.InsertArrayElementAtIndex(0);

                    targetNamesProp.GetArrayElementAtIndex(0).stringValue = newValue;
                }
            }
        }

        private static void GetMappingColumnRects(
            Rect rowRect,
            out Rect toggleRect,
            out Rect sourceRect,
            out Rect arrowRect,
            out Rect targetRect,
            out Rect multiplierRect,
            out Rect offsetRect,
            out Rect expandRect,
            out Rect deleteRect)
        {
            float x = rowRect.x + MappingTableLeadingPadding;
            float y = rowRect.y;
            float height = rowRect.height;

            toggleRect = new Rect(x, y, MappingTableToggleColumnWidth, height);
            x += MappingTableToggleColumnWidth;

            sourceRect = new Rect(x, y, SourceBlendshapeColumnWidth, height);
            x += SourceBlendshapeColumnWidth;

            arrowRect = new Rect(x, y, MappingTableArrowColumnWidth, height);
            x += MappingTableArrowColumnWidth;

            float trailingWidth =
                (MappingTableNumericColumnWidth * 2f) +
                MappingTableExpandButtonWidth +
                MappingTableDeleteButtonWidth;
            float targetWidth = Mathf.Max(0f, rowRect.xMax - trailingWidth - x);
            targetRect = new Rect(x, y, targetWidth, height);
            x += targetWidth;

            multiplierRect = new Rect(x, y, MappingTableNumericColumnWidth, height);
            x += MappingTableNumericColumnWidth;

            offsetRect = new Rect(x, y, MappingTableNumericColumnWidth, height);
            x += MappingTableNumericColumnWidth;

            expandRect = new Rect(x, y + 2f, MappingTableExpandButtonWidth, Mathf.Max(0f, height - 4f));
            x += MappingTableExpandButtonWidth;

            deleteRect = new Rect(x, y + 2f, MappingTableDeleteButtonWidth, Mathf.Max(0f, height - 4f));
        }

        private static Rect InsetRect(Rect rect, float leftInset)
        {
            return new Rect(
                rect.x + leftInset,
                rect.y,
                Mathf.Max(0f, rect.width - leftInset),
                rect.height);
        }

        private static float GetInputContentLeftInset()
        {
            RectOffset padding = EditorStyles.textField?.padding;
            return Mathf.Max(4f, padding?.left ?? 0);
        }

        private static float GetMappingScrollbarWidth()
        {
            GUIStyle scrollbarStyle = GUI.skin?.verticalScrollbar;
            if (scrollbarStyle == null) return 13f;

            float width = scrollbarStyle.fixedWidth;
            if (width <= 0f) width = 13f;

            return width;
        }

        private bool ShouldShowEntry(SerializedProperty entry)
        {
            SerializedProperty sourceBlendshapeProp = entry.FindPropertyRelative("sourceBlendshape");
            SerializedProperty targetNamesProp = entry.FindPropertyRelative("targetNames");
            SerializedProperty enabledProp = entry.FindPropertyRelative("enabled");

            string sourceBlendshape = sourceBlendshapeProp?.stringValue ?? "";
            bool isEnabled = enabledProp?.boolValue ?? true;
            bool hasTarget = targetNamesProp != null && targetNamesProp.arraySize > 0 &&
                             !string.IsNullOrEmpty(targetNamesProp.GetArrayElementAtIndex(0).stringValue);

            if (_showOnlyEnabled && !isEnabled) return false;

            if (_showOnlyUnmapped && hasTarget) return false;

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                string searchLower = _searchFilter.ToLowerInvariant();
                bool matchesSource = sourceBlendshape.ToLowerInvariant().Contains(searchLower);

                bool matchesTarget = false;
                if (targetNamesProp != null)
                {
                    for (int i = 0; i < targetNamesProp.arraySize; i++)
                    {
                        string targetName = targetNamesProp.GetArrayElementAtIndex(i).stringValue;
                        if (!string.IsNullOrEmpty(targetName) && targetName.ToLowerInvariant().Contains(searchLower))
                        {
                            matchesTarget = true;
                            break;
                        }
                    }
                }

                if (!matchesSource && !matchesTarget) return false;
            }

            return true;
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

        private bool HasPreviewMeshForDropdown()
        {
            if (_previewMeshes == null || _previewMeshes.Count == 0) return false;

            for (int i = 0; i < _previewMeshes.Count; i++)
            {
                SkinnedMeshRenderer mesh = _previewMeshes[i];
                if (mesh != null && mesh.sharedMesh != null) return true;
            }

            return false;
        }

        private void EnsureMeshNamesCache()
        {
            if (!_meshNamesNeedRefresh && _meshBlendshapeNames != null) return;

            _meshBlendshapeNames = new List<string>();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddMeshBlendshapes(SkinnedMeshRenderer meshRenderer)
            {
                if (meshRenderer == null || meshRenderer.sharedMesh == null) return;

                Mesh mesh = meshRenderer.sharedMesh;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string name = mesh.GetBlendShapeName(i);
                    if (unique.Add(name)) _meshBlendshapeNames.Add(name);
                }
            }

            if (_previewMeshes != null)
            {
                for (int i = 0; i < _previewMeshes.Count; i++)
                    AddMeshBlendshapes(_previewMeshes[i]);
            }

            _meshNamesNeedRefresh = false;
        }

        private int GetPreviewMeshBlendshapeCount()
        {
            int count = 0;
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_previewMeshes != null)
            {
                for (int meshIndex = 0; meshIndex < _previewMeshes.Count; meshIndex++)
                {
                    SkinnedMeshRenderer meshRenderer = _previewMeshes[meshIndex];
                    if (meshRenderer == null || meshRenderer.sharedMesh == null) continue;

                    Mesh m = meshRenderer.sharedMesh;
                    for (int i = 0; i < m.blendShapeCount; i++)
                    {
                        if (unique.Add(m.GetBlendShapeName(i)))
                            count++;
                    }
                }
            }

            return HasPreviewMeshForDropdown()
                ? count
                : -1;
        }

        private void ShowAutoDetectMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Exact Match"), false, () => RunAutoDetect(BlendshapeMatchMode.Exact));
            menu.AddItem(new GUIContent("Contains Match (Recommended)"), false,
                () => RunAutoDetect(BlendshapeMatchMode.Contains));
            menu.AddItem(new GUIContent("Fuzzy Match"), false, () => RunAutoDetect(BlendshapeMatchMode.Fuzzy));
            menu.ShowAsContext();
        }

        private void RunAutoDetect(BlendshapeMatchMode mode)
        {
            Undo.RecordObject(_mapping, "Auto-Detect Lip Sync Mapping");
            SkinnedMeshRenderer[] previewMeshes = GetPreviewMeshes();
            if (previewMeshes.Length == 0) return;

            _mapping.AutoDetectFromMeshes(previewMeshes, mode);
            EditorUtility.SetDirty(_mapping);
            serializedObject.Update();

            ConvaiLogger.Info($"[Convai LipSync] Auto-detect complete using {mode} matching.", LogCategory.Editor);
        }

        private SkinnedMeshRenderer[] GetPreviewMeshes()
        {
            var meshes = new List<SkinnedMeshRenderer>();
            if (_previewMeshes == null || _previewMeshes.Count == 0) return meshes.ToArray();

            var unique = new HashSet<EntityId>();
            for (int i = 0; i < _previewMeshes.Count; i++)
            {
                SkinnedMeshRenderer mesh = _previewMeshes[i];
                if (mesh == null || mesh.sharedMesh == null) continue;

                if (!unique.Add(mesh.GetEntityId())) continue;

                meshes.Add(mesh);
            }

            return meshes.ToArray();
        }

        private void ImportMappingFromFile()
        {
            string path = EditorUtility.OpenFilePanelWithFilters(
                "Import Lip Sync Mapping",
                UnityEngine.Application.dataPath,
                new[] { "Mapping files", "json,txt,map", "All files", "*" });

            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                string text = File.ReadAllText(path);
                TryImportMappingText(text, Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "Import Failed",
                    $"Could not read file:\n{path}\n\n{ex.Message}",
                    "OK");
            }
        }

        private void ImportMappingFromClipboard()
        {
            string clipboard = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clipboard))
            {
                EditorUtility.DisplayDialog(
                    "Clipboard Empty",
                    "Clipboard does not contain mapping text.",
                    "OK");
                return;
            }

            TryImportMappingText(clipboard, "clipboard");
        }

        private void TryImportMappingText(string rawText, string sourceLabel)
        {
            if (!ConvaiLipSyncMapImportParser.TryParse(rawText,
                    out ConvaiLipSyncMapImportParser.MappingImportData imported, out string error))
            {
                EditorUtility.DisplayDialog(
                    "Import Failed",
                    error ?? "Unsupported mapping format.",
                    "OK");
                return;
            }

            ApplyImportedMappings(imported, $"Import Lip Sync Mapping ({sourceLabel})");

            string summary = $"Imported {imported.Entries.Count} mapping entries from {sourceLabel}.";
            if (imported.Warnings.Count > 0) summary += $"\nSkipped {imported.Warnings.Count} malformed entries.";

            EditorUtility.DisplayDialog("Import Complete", summary, "OK");

            if (imported.Warnings.Count > 0)
            {
                ConvaiLogger.Warning(
                    $"[Convai LipSync] Import warnings:\n- {string.Join("\n- ", imported.Warnings)}",
                    LogCategory.Editor);
            }
        }

        private void ApplyImportedMappings(ConvaiLipSyncMapImportParser.MappingImportData imported, string undoLabel)
        {
            if (imported == null) return;

            Undo.RecordObject(_mapping, undoLabel);
            serializedObject.Update();

            if (!string.IsNullOrWhiteSpace(imported.TargetProfileId) && _targetProfileProp != null)
                _targetProfileProp.stringValue = LipSyncProfileId.Normalize(imported.TargetProfileId);

            if (imported.HasDescription && _descriptionProp != null)
                _descriptionProp.stringValue = imported.Description ?? string.Empty;

            if (imported.GlobalMultiplier.HasValue && _globalMultiplierProp != null)
                _globalMultiplierProp.floatValue = Mathf.Clamp(imported.GlobalMultiplier.Value, 0f, 3f);

            if (imported.GlobalOffset.HasValue && _globalOffsetProp != null)
                _globalOffsetProp.floatValue = Mathf.Clamp(imported.GlobalOffset.Value, -1f, 1f);

            if (imported.AllowUnmappedPassthrough.HasValue && _allowUnmappedPassthroughProp != null)
                _allowUnmappedPassthroughProp.boolValue = imported.AllowUnmappedPassthrough.Value;

            _mappingsProp.ClearArray();
            for (int i = 0; i < imported.Entries.Count; i++)
            {
                ConvaiLipSyncMapImportParser.ImportedEntry sourceEntry = imported.Entries[i];

                int entryIndex = _mappingsProp.arraySize;
                _mappingsProp.InsertArrayElementAtIndex(entryIndex);
                SerializedProperty entry = _mappingsProp.GetArrayElementAtIndex(entryIndex);

                SerializedProperty sourceBlendshapeProp = entry.FindPropertyRelative("sourceBlendshape");
                if (sourceBlendshapeProp != null)
                    sourceBlendshapeProp.stringValue = sourceEntry.SourceBlendshape ?? string.Empty;

                SerializedProperty targetNamesProp = entry.FindPropertyRelative("targetNames");
                if (targetNamesProp != null)
                {
                    List<string> targets = sourceEntry.TargetNames == null
                        ? new List<string>()
                        : sourceEntry.TargetNames
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Select(name => name.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    SetStringArray(targetNamesProp, targets);
                }

                SerializedProperty enabledProp = entry.FindPropertyRelative("enabled");
                if (enabledProp != null) enabledProp.boolValue = sourceEntry.Enabled;

                SerializedProperty multiplierProp = entry.FindPropertyRelative("multiplier");
                if (multiplierProp != null) multiplierProp.floatValue = Mathf.Clamp(sourceEntry.Multiplier, 0f, 5f);

                SerializedProperty offsetProp = entry.FindPropertyRelative("offset");
                if (offsetProp != null) offsetProp.floatValue = Mathf.Clamp(sourceEntry.Offset, -1f, 1f);

                SerializedProperty curveExponentProp = entry.FindPropertyRelative("curveExponent");
                if (curveExponentProp != null)
                {
                    curveExponentProp.floatValue = Mathf.Clamp(sourceEntry.CurveExponent,
                        ConvaiLipSyncMapAsset.MinCurveExponent, ConvaiLipSyncMapAsset.MaxCurveExponent);
                }

                SerializedProperty useOverrideValueProp = entry.FindPropertyRelative("useOverrideValue");
                if (useOverrideValueProp != null) useOverrideValueProp.boolValue = sourceEntry.UseOverrideValue;

                SerializedProperty overrideValueProp = entry.FindPropertyRelative("overrideValue");
                if (overrideValueProp != null) overrideValueProp.floatValue = Mathf.Clamp01(sourceEntry.OverrideValue);

                SerializedProperty ignoreGlobalModifiersProp = entry.FindPropertyRelative("ignoreGlobalModifiers");
                if (ignoreGlobalModifiersProp != null)
                    ignoreGlobalModifiersProp.boolValue = sourceEntry.IgnoreGlobalModifiers;

                float clampMin = Mathf.Clamp01(sourceEntry.ClampMinValue);
                float clampMax = Mathf.Clamp01(sourceEntry.ClampMaxValue);
                if (clampMax < clampMin) clampMax = clampMin;

                SerializedProperty clampMinValueProp = entry.FindPropertyRelative("clampMinValue");
                if (clampMinValueProp != null) clampMinValueProp.floatValue = clampMin;

                SerializedProperty clampMaxValueProp = entry.FindPropertyRelative("clampMaxValue");
                if (clampMaxValueProp != null) clampMaxValueProp.floatValue = clampMax;
            }

            _expandedMappingIndex = -1;
            _meshNamesNeedRefresh = true;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_mapping);
            serializedObject.Update();
        }

        private static void SetStringArray(SerializedProperty arrayProp, List<string> values)
        {
            if (arrayProp == null) return;

            arrayProp.ClearArray();
            if (values == null || values.Count == 0) return;

            for (int i = 0; i < values.Count; i++)
            {
                arrayProp.InsertArrayElementAtIndex(i);
                arrayProp.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private void CopyMappingAsJsonToClipboard()
        {
            string json = BuildMappingJson(true);
            GUIUtility.systemCopyBuffer = json;

            ConvaiLogger.Info(
                $"[Convai LipSync] Mapping JSON copied to clipboard ({_mappingsProp.arraySize} entries).",
                LogCategory.Editor);
            EditorUtility.DisplayDialog("JSON Copied", "Current mapping was copied to clipboard as JSON.", "OK");
        }

        private string BuildMappingJson(bool prettyPrint)
        {
            serializedObject.Update();

            var payload = new MappingExportPayload
            {
                version = ConvaiLipSyncMapImportParser.CurrentVersion,
                targetProfileId =
                    _targetProfileProp != null ? _targetProfileProp.stringValue : LipSyncProfileId.ARKitValue,
                description = _descriptionProp != null ? _descriptionProp.stringValue : string.Empty,
                globalMultiplier = _globalMultiplierProp != null ? _globalMultiplierProp.floatValue : 1f,
                globalOffset = _globalOffsetProp != null ? _globalOffsetProp.floatValue : 0f,
                allowUnmappedPassthrough =
                    _allowUnmappedPassthroughProp != null && _allowUnmappedPassthroughProp.boolValue,
                mappings = new List<MappingExportEntry>()
            };

            for (int i = 0; i < _mappingsProp.arraySize; i++)
            {
                SerializedProperty entry = _mappingsProp.GetArrayElementAtIndex(i);
                if (entry == null) continue;

                var exportEntry = new MappingExportEntry
                {
                    sourceBlendshape = entry.FindPropertyRelative("sourceBlendshape")?.stringValue ?? string.Empty,
                    targetNames = GetStringArray(entry.FindPropertyRelative("targetNames")),
                    multiplier = entry.FindPropertyRelative("multiplier")?.floatValue ?? 1f,
                    offset = entry.FindPropertyRelative("offset")?.floatValue ?? 0f,
                    curveExponent = entry.FindPropertyRelative("curveExponent")?.floatValue ?? 1f,
                    enabled = entry.FindPropertyRelative("enabled")?.boolValue ?? true,
                    useOverrideValue = entry.FindPropertyRelative("useOverrideValue")?.boolValue ?? false,
                    overrideValue = entry.FindPropertyRelative("overrideValue")?.floatValue ?? 0f,
                    ignoreGlobalModifiers = entry.FindPropertyRelative("ignoreGlobalModifiers")?.boolValue ?? false,
                    clampMinValue = entry.FindPropertyRelative("clampMinValue")?.floatValue ?? 0f,
                    clampMaxValue = entry.FindPropertyRelative("clampMaxValue")?.floatValue ?? 1f
                };

                payload.mappings.Add(exportEntry);
            }

            return JsonUtility.ToJson(payload, prettyPrint);
        }

        private static string[] GetStringArray(SerializedProperty arrayProp)
        {
            if (arrayProp == null || !arrayProp.isArray || arrayProp.arraySize == 0) return Array.Empty<string>();

            string[] values = new string[arrayProp.arraySize];
            for (int i = 0; i < arrayProp.arraySize; i++) values[i] = arrayProp.GetArrayElementAtIndex(i).stringValue;

            return values;
        }

        [Serializable]
        private sealed class MappingExportPayload
        {
            public int version = ConvaiLipSyncMapImportParser.CurrentVersion;
            public string targetProfileId;
            public string description;
            public float globalMultiplier = 1f;
            public float globalOffset;
            public bool allowUnmappedPassthrough = true;
            public List<MappingExportEntry> mappings = new();
        }

        [Serializable]
        private sealed class MappingExportEntry
        {
            public string sourceBlendshape;
            public string[] targetNames;
            public float multiplier = 1f;
            public float offset;
            public float curveExponent = 1f;
            public bool enabled = true;
            public bool useOverrideValue;
            public float overrideValue;
            public bool ignoreGlobalModifiers;
            public float clampMinValue;
            public float clampMaxValue = 1f;
        }

        private void DrawTargetProfileSelector()
        {
            IReadOnlyList<ConvaiLipSyncProfile> profiles = LipSyncProfileCatalog.GetProfiles();
            string currentId = LipSyncProfileId.Normalize(_targetProfileProp.stringValue);

            if (profiles == null || profiles.Count == 0)
            {
                EditorGUILayout.PropertyField(_targetProfileProp, new GUIContent("Target Profile ID"));
                return;
            }

            string[] profileOptions = new string[profiles.Count];
            int selectedIndex = -1;
            for (int i = 0; i < profiles.Count; i++)
            {
                ConvaiLipSyncProfile profile = profiles[i];
                profileOptions[i] = profile.DisplayName;
                if (string.Equals(profile.ProfileId.Value, currentId, StringComparison.Ordinal)) selectedIndex = i;
            }

            int popupIndex = selectedIndex >= 0 ? selectedIndex : 0;
            EditorGUI.BeginChangeCheck();
            int newSelection = EditorGUILayout.Popup("Target Profile", popupIndex, profileOptions);
            if (EditorGUI.EndChangeCheck())
            {
                string nextProfileId = profiles[Mathf.Clamp(newSelection, 0, profiles.Count - 1)].ProfileId.Value;
                if (EditorUtility.DisplayDialog(
                        "Profile Changed",
                        "Do you want to reinitialize the mappings for the new profile? This will clear existing mappings.",
                        "Yes, Reinitialize", "No, Keep Current"))
                {
                    _targetProfileProp.stringValue = nextProfileId;
                    serializedObject.ApplyModifiedProperties();
                    _mapping.InitializeWithDefaults();
                    serializedObject.Update();
                }
                else
                    _targetProfileProp.stringValue = nextProfileId;
            }

            if (selectedIndex < 0)
            {
                EditorGUILayout.HelpBox(
                    $"Profile id '{currentId}' is not registered. Select a valid profile.",
                    MessageType.Warning);
            }
        }

        private void SetAllEnabled(bool enabled)
        {
            Undo.RecordObject(_mapping, enabled ? "Enable All Lip Sync" : "Disable All Lip Sync");

            for (int i = 0; i < _mappingsProp.arraySize; i++)
            {
                SerializedProperty enabledProp =
                    _mappingsProp.GetArrayElementAtIndex(i).FindPropertyRelative("enabled");
                if (enabledProp != null) enabledProp.boolValue = enabled;
            }

            EditorUtility.SetDirty(_mapping);
        }

        private void ResetAllMultipliers()
        {
            Undo.RecordObject(_mapping, "Reset Lip Sync Multipliers");

            for (int i = 0; i < _mappingsProp.arraySize; i++)
            {
                SerializedProperty multiplierProp =
                    _mappingsProp.GetArrayElementAtIndex(i).FindPropertyRelative("multiplier");
                if (multiplierProp != null) multiplierProp.floatValue = 1f;
            }

            EditorUtility.SetDirty(_mapping);
        }

        private void ResetAllCurveExponents()
        {
            Undo.RecordObject(_mapping, "Reset Lip Sync Response Curves");

            for (int i = 0; i < _mappingsProp.arraySize; i++)
            {
                SerializedProperty curveExponentProp =
                    _mappingsProp.GetArrayElementAtIndex(i).FindPropertyRelative("curveExponent");
                if (curveExponentProp != null) curveExponentProp.floatValue = 1f;
            }

            EditorUtility.SetDirty(_mapping);
        }

        private void ResetAllOffsets()
        {
            Undo.RecordObject(_mapping, "Reset Lip Sync Offsets");

            for (int i = 0; i < _mappingsProp.arraySize; i++)
            {
                SerializedProperty offsetProp = _mappingsProp.GetArrayElementAtIndex(i).FindPropertyRelative("offset");
                if (offsetProp != null) offsetProp.floatValue = 0f;
            }

            EditorUtility.SetDirty(_mapping);
        }

        private void EnableCategory(string[] keywords)
        {
            Undo.RecordObject(_mapping, "Enable Category");

            for (int i = 0; i < _mappingsProp.arraySize; i++)
            {
                SerializedProperty entry = _mappingsProp.GetArrayElementAtIndex(i);
                SerializedProperty sourceBlendshapeProp = entry.FindPropertyRelative("sourceBlendshape");
                SerializedProperty enabledProp = entry.FindPropertyRelative("enabled");

                if (sourceBlendshapeProp == null || enabledProp == null) continue;

                string name = sourceBlendshapeProp.stringValue;
                bool matchesCategory = keywords.Any(kw => name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);

                enabledProp.boolValue = matchesCategory;
            }

            EditorUtility.SetDirty(_mapping);
        }

        private void SortMappings()
        {
            Undo.RecordObject(_mapping, "Sort Mappings");

            IReadOnlyList<ConvaiLipSyncMapAsset.BlendshapeMappingEntry> mappings = _mapping.Mappings;
            if (mappings == null) return;

            List<ConvaiLipSyncMapImportParser.ImportedEntry> sortedList = mappings
                .OrderBy(m => m.sourceBlendshape)
                .Select(m => new ConvaiLipSyncMapImportParser.ImportedEntry
                {
                    SourceBlendshape = m.sourceBlendshape,
                    TargetNames = m.targetNames != null ? new List<string>(m.targetNames) : new List<string>(),
                    Multiplier = m.multiplier,
                    Offset = m.offset,
                    CurveExponent = m.curveExponent,
                    Enabled = m.enabled,
                    UseOverrideValue = m.useOverrideValue,
                    OverrideValue = m.overrideValue,
                    IgnoreGlobalModifiers = m.ignoreGlobalModifiers,
                    ClampMinValue = m.clampMinValue,
                    ClampMaxValue = m.clampMaxValue
                })
                .ToList();

            serializedObject.Update();
            _mappingsProp.ClearArray();
            for (int i = 0; i < sortedList.Count; i++)
            {
                ConvaiLipSyncMapImportParser.ImportedEntry sourceEntry = sortedList[i];
                int entryIndex = _mappingsProp.arraySize;
                _mappingsProp.InsertArrayElementAtIndex(entryIndex);
                SerializedProperty entry = _mappingsProp.GetArrayElementAtIndex(entryIndex);

                entry.FindPropertyRelative("sourceBlendshape").stringValue =
                    sourceEntry.SourceBlendshape ?? string.Empty;
                SetStringArray(entry.FindPropertyRelative("targetNames"), sourceEntry.TargetNames);
                entry.FindPropertyRelative("multiplier").floatValue = Mathf.Clamp(sourceEntry.Multiplier, 0f, 5f);
                entry.FindPropertyRelative("offset").floatValue = Mathf.Clamp(sourceEntry.Offset, -1f, 1f);
                entry.FindPropertyRelative("curveExponent").floatValue = Mathf.Clamp(sourceEntry.CurveExponent,
                    ConvaiLipSyncMapAsset.MinCurveExponent, ConvaiLipSyncMapAsset.MaxCurveExponent);
                entry.FindPropertyRelative("enabled").boolValue = sourceEntry.Enabled;
                entry.FindPropertyRelative("useOverrideValue").boolValue = sourceEntry.UseOverrideValue;
                entry.FindPropertyRelative("overrideValue").floatValue = Mathf.Clamp01(sourceEntry.OverrideValue);
                entry.FindPropertyRelative("ignoreGlobalModifiers").boolValue = sourceEntry.IgnoreGlobalModifiers;

                float clampMin = Mathf.Clamp01(sourceEntry.ClampMinValue);
                float clampMax = Mathf.Clamp01(sourceEntry.ClampMaxValue);
                if (clampMax < clampMin) clampMax = clampMin;

                entry.FindPropertyRelative("clampMinValue").floatValue = clampMin;
                entry.FindPropertyRelative("clampMaxValue").floatValue = clampMax;
            }

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(_mapping);
            serializedObject.Update();

            ConvaiLogger.Info("[Convai LipSync] Mappings sorted alphabetically.", LogCategory.Editor);
        }

        private void AddNewEntry()
        {
            int newIndex = _mappingsProp.arraySize;
            _mappingsProp.InsertArrayElementAtIndex(newIndex);

            SerializedProperty newEntry = _mappingsProp.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("sourceBlendshape").stringValue = "NewBlendshape";
            newEntry.FindPropertyRelative("targetNames").ClearArray();
            newEntry.FindPropertyRelative("multiplier").floatValue = 1f;
            newEntry.FindPropertyRelative("offset").floatValue = 0f;
            newEntry.FindPropertyRelative("enabled").boolValue = true;
            SerializedProperty useOverride = newEntry.FindPropertyRelative("useOverrideValue");
            if (useOverride != null) useOverride.boolValue = false;
            SerializedProperty overrideVal = newEntry.FindPropertyRelative("overrideValue");
            if (overrideVal != null) overrideVal.floatValue = 0f;
            SerializedProperty ignoreGlobal = newEntry.FindPropertyRelative("ignoreGlobalModifiers");
            if (ignoreGlobal != null) ignoreGlobal.boolValue = false;
            SerializedProperty clampMin = newEntry.FindPropertyRelative("clampMinValue");
            if (clampMin != null) clampMin.floatValue = 0f;
            SerializedProperty clampMax = newEntry.FindPropertyRelative("clampMaxValue");
            if (clampMax != null) clampMax.floatValue = 1f;

            _scrollPosition = new Vector2(0, float.MaxValue);
        }

        #endregion
    }
}
#endif
