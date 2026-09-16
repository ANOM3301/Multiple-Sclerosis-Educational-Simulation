#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    ///     Validation window for verifying lip sync mapping between source blendshapes and target meshes.
    ///     Displays mapping health, missing targets, and optional runtime value previews.
    /// </summary>
    internal sealed class ConvaiLipSyncMapDebugWindow : EditorWindow
    {
        #region Filter Section

        private void DrawFilterSection()
        {
            if (_validationEntries.Count == 0) return;

            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField(_searchFilter, _searchFieldStyle, GUILayout.MinWidth(180),
                GUILayout.MaxWidth(280));
            if (GUILayout.Button(new GUIContent("\u2715", "Clear search"), GUILayout.Width(22)) &&
                !string.IsNullOrEmpty(_searchFilter))
            {
                _searchFilter = "";
                GUI.FocusControl(null);
            }

            GUILayout.Space(12);
            _showOnlyProblems = GUILayout.Toggle(
                _showOnlyProblems,
                new GUIContent("Show Only Problems",
                    "Hide valid entries; show only No Mapping, Target Missing, or Disabled."),
                EditorStyles.miniButton,
                GUILayout.Width(130));
            GUILayout.Space(8);
            EditorGUI.BeginDisabledGroup(!UnityEngine.Application.isPlaying);
            bool newShowLive = GUILayout.Toggle(
                _showLiveValues,
                new GUIContent("Live Values",
                    "Show real-time blendshape values when in Play Mode (requires Lip Sync Component)."),
                EditorStyles.miniButton,
                GUILayout.Width(90));
            if (newShowLive != _showLiveValues) _showLiveValues = newShowLive;
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        #endregion

        #region Export

        private void ExportValidationReport()
        {
            string path = EditorUtility.SaveFilePanel(
                "Save Validation Report",
                "",
                $"LipSyncValidation_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                "txt");

            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== LIP SYNC MAPPING VALIDATION REPORT ===");
            sb.AppendLine($"Generated: {DateTime.Now}");
            sb.AppendLine($"Profile: {_selectedProfile}");
            if (_targetMeshes == null || _targetMeshes.Count == 0)
                sb.AppendLine("Target Meshes: None");
            else
            {
                for (int i = 0; i < _targetMeshes.Count; i++)
                {
                    SkinnedMeshRenderer mesh = _targetMeshes[i];
                    sb.AppendLine($"Target Mesh {i + 1}: {(mesh != null ? mesh.name : "None")}");
                }
            }

            sb.AppendLine($"Mapping: {(_mapping != null ? _mapping.name : "None")}");
            sb.AppendLine();

            int valid = _validationEntries.Count(e => e.Status == ValidationStatus.Valid);
            int problems = _validationEntries.Count(e =>
                e.Status == ValidationStatus.TargetBlendshapeMissing || e.Status == ValidationStatus.NoMapping);
            sb.AppendLine($"SUMMARY: {valid} valid, {problems} problems out of {_validationEntries.Count} total");
            sb.AppendLine();

            sb.AppendLine("=== PROBLEMS ===");
            foreach (MappingValidationEntry entry in _validationEntries.Where(e =>
                         e.Status == ValidationStatus.TargetBlendshapeMissing ||
                         e.Status == ValidationStatus.NoMapping))
            {
                sb.AppendLine($"[{entry.Index:D3}] {entry.SourceBlendshape}");
                sb.AppendLine($"      Status: {entry.Status}");
                sb.AppendLine($"      Message: {entry.StatusMessage}");
                if (entry.MappedTargetNames.Count > 0)
                    sb.AppendLine($"      Targets: {string.Join(", ", entry.MappedTargetNames)}");
                sb.AppendLine();
            }

            sb.AppendLine("=== ALL ENTRIES ===");
            sb.AppendLine("Index\tSource Blendshape\tTarget(s)\tStatus");
            foreach (MappingValidationEntry entry in _validationEntries)
            {
                string targets = entry.MappedTargetNames.Count > 0
                    ? string.Join("; ", entry.MappedTargetNames)
                    : "(none)";
                sb.AppendLine($"{entry.Index}\t{entry.SourceBlendshape}\t{targets}\t{entry.Status}");
            }

            sb.AppendLine();
            sb.AppendLine("=== MESH BLENDSHAPES ===");
            for (int i = 0; i < _meshBlendshapeNames.Count; i++) sb.AppendLine($"[{i:D3}] {_meshBlendshapeNames[i]}");

            File.WriteAllText(path, sb.ToString());
            EditorUtility.RevealInFinder(path);
            ConvaiLogger.Info("[Convai LipSync] Validation report exported to: " + path, LogCategory.Editor);
        }

        #endregion

        #region Constants & Colors

        private static readonly Color ConvaiGreen = ConvaiInspectorThemeTokens.Accent;
        private static readonly Color ConvaiGreenLight = ConvaiInspectorThemeTokens.AccentEmphasis;
        private static readonly Color ConvaiWarning = ConvaiInspectorThemeTokens.Warning;
        private static readonly Color ConvaiError = ConvaiInspectorThemeTokens.Error;
        private static readonly Color ConvaiInfo = ConvaiInspectorThemeTokens.Info;
        private static readonly Color HeaderBg = ConvaiInspectorThemeTokens.HeaderBackground;
        private static readonly Color SectionBg = ConvaiInspectorThemeTokens.SectionBackground;
        private static readonly Color RowAltBg = ConvaiInspectorThemeTokens.AlternateRowBackground;
        private static readonly Color TableHeaderBg = ConvaiInspectorThemeTokens.TableHeaderBackground;
        private const int SectionIconFontSize = ConvaiInspectorThemeTokens.SectionIconFontSize;
        private const string EditorStateHostId = "MapDebugWindow";
        private const string SectionConfigurationId = "Configuration";
        private const string SectionValidationResultsId = "ValidationResults";
        private const float StatCellWidth = 52f;
        private const float StatCellGap = 20f;
        private const float StatBarHeight = 40f;

        #endregion

        #region Private Fields

        private ConvaiLipSyncComponent _component;
        private List<SkinnedMeshRenderer> _targetMeshes = new();
        private ConvaiLipSyncMapAsset _mapping;
        private LipSyncProfileId _selectedProfile = LipSyncProfileId.MetaHuman;

        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private bool _showOnlyProblems;
        private bool _showLiveValues;

        private readonly List<string> _meshBlendshapeNames = new();
        private readonly HashSet<string> _meshNameSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<MappingValidationEntry> _validationEntries = new();

        private Texture2D _convaiIcon;
        private bool _showConfiguration = true;
        private bool _showValidationResults = true;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _miniButtonStyle;
        private GUIStyle _searchFieldStyle;
        private GUIStyle _tableHeaderLabelStyle;
        private GUIStyle _statValueStyle;
        private GUIStyle _statLabelStyle;
        private GUIStyle _statValueRightStyle;
        private bool _stylesInitialized;

        #endregion

        #region Data Structures

        private class MappingValidationEntry
        {
            public int Index;
            public bool IsEnabled = true;
            public List<string> MappedTargetNames = new();
            public float Multiplier = 1f;
            public float Offset;
            public string SourceBlendshape;
            public ValidationStatus Status;
            public string StatusMessage;
        }

        private enum ValidationStatus
        {
            Valid,
            NoMapping,
            TargetBlendshapeMissing,
            Disabled,
            MultipleTargets
        }

        #endregion

        #region Window Setup

        /// <summary>Window title used by the mapping validator UI.</summary>
        private const string WindowTitle = "Lip Sync Validator";

        public static void ShowWindow()
        {
            var window = GetWindow<ConvaiLipSyncMapDebugWindow>();
            window.titleContent = new GUIContent(
                WindowTitle,
                "Validate lip sync blendshape mappings between Convai profile and character meshes.");
            window.minSize = new Vector2(960, 620);
            Rect rect = window.position;
            if (rect.width < 980f || rect.height < 640f) window.position = new Rect(rect.x, rect.y, 980f, 640f);
        }

        /// <summary>
        ///     Opens the validator window with a specific Lip Sync component pre-selected.
        /// </summary>
        public static void ShowForComponent(ConvaiLipSyncComponent component)
        {
            var window = GetWindow<ConvaiLipSyncMapDebugWindow>();
            window.titleContent = new GUIContent(
                WindowTitle,
                "Validate lip sync blendshape mappings between Convai profile and character meshes.");
            window.minSize = new Vector2(960, 620);
            Rect rect = window.position;
            if (rect.width < 980f || rect.height < 640f) window.position = new Rect(rect.x, rect.y, 980f, 640f);
            window._component = component;
            window.SyncFromComponent();
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            LoadAssets();
            _showConfiguration = ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionConfigurationId, true);
            _showValidationResults =
                ConvaiInspectorSectionStateStore.Get(EditorStateHostId, SectionValidationResultsId, true);
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionConfigurationId, _showConfiguration);
            ConvaiInspectorSectionStateStore.Set(EditorStateHostId, SectionValidationResultsId, _showValidationResults);
            EditorApplication.update -= OnEditorUpdate;
        }

        private void LoadAssets()
        {
            if (_convaiIcon == null) _convaiIcon = ConvaiBrandedIconProvider.GetConvaiIcon();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white }
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

            _tableHeaderLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            _statValueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold
            };

            _statLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            _statLabelStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);

            _statValueRightStyle = new GUIStyle(_statValueStyle) { alignment = TextAnchor.MiddleRight };

            _stylesInitialized = true;
        }

        private void OnEditorUpdate()
        {
            TryAutoBindComponent();

            if (UnityEngine.Application.isPlaying && _showLiveValues && _component != null) Repaint();
        }

        private void OnGUI()
        {
            InitializeStyles();
            DrawInspectorHeader();
            EditorGUILayout.Space(6);
            DrawSetupSection();
            DrawFilterSection();
            DrawValidationResults();
        }

        #endregion

        #region Header

        private void DrawInspectorHeader()
        {
            const float headerHeight = 50f;
            const float iconSize = 22f;
            const float iconTextSpacing = 6f;
            float contentIndent = iconSize + iconTextSpacing;

            Rect headerRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(
                new Rect(headerRect.x - 18f, headerRect.y - 4f, headerRect.width + 36f, headerHeight + 4f), HeaderBg);

            EditorGUILayout.BeginVertical(GUILayout.Height(headerHeight));
            EditorGUILayout.BeginHorizontal(GUILayout.Height(22f));
            Rect iconCellRect =
                GUILayoutUtility.GetRect(iconSize, 22f, GUILayout.Width(iconSize), GUILayout.Height(22f));
            if (_convaiIcon != null && Event.current.type == EventType.Repaint)
            {
                var iconRect = new Rect(iconCellRect.x, iconCellRect.y, iconSize, iconSize);
                GUI.DrawTexture(iconRect, _convaiIcon, ScaleMode.ScaleToFit, true);
            }

            GUILayout.Space(iconTextSpacing);
            GUILayout.Label("Lip Sync Mapping Validator", _headerStyle, GUILayout.Height(22f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(contentIndent);
            GUILayout.Label(
                "Validate profile blendshapes against character meshes and mapping asset.",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private void DrawStatCell(string label, string value, Color valueColor)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(StatCellWidth), GUILayout.Height(StatBarHeight));
            GUILayout.FlexibleSpace();
            GUILayout.Label(label, _statLabelStyle);
            GUILayout.Space(2);
            GUI.color = valueColor;
            GUILayout.Label(value, _statValueStyle);
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawStatBarRightLabel(string text, Color color)
        {
            EditorGUILayout.BeginVertical(GUILayout.Height(StatBarHeight), GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUI.color = color;
            GUILayout.Label(text, _statValueRightStyle);
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Section Helpers

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

        #endregion

        #region Setup Section

        private void DrawSetupSection()
        {
            _showConfiguration = DrawSectionHeader(SectionConfigurationId, "CONFIGURATION", _showConfiguration,
                "\u2699", ConvaiGreen, SectionIconFontSize);
            if (!_showConfiguration) return;

            DrawSectionBackground(() =>
            {
                if (_component != null)
                {
                    LipSyncProfileId syncedProfile = GetComponentSelectedProfile();
                    if (_selectedProfile != syncedProfile)
                    {
                        _selectedProfile = syncedProfile;
                        RefreshMeshBlendshapes();
                        RefreshValidation();
                    }
                }

                EditorGUI.BeginChangeCheck();
                EditorGUI.BeginDisabledGroup(_component != null);
                _selectedProfile = DrawProfilePopup(
                    new GUIContent("Profile",
                        "Blendshape set (e.g. ARKit, CC4) used for validation. Synced from Lip Sync Component when assigned."),
                    _selectedProfile);
                EditorGUI.EndDisabledGroup();
                if (EditorGUI.EndChangeCheck())
                {
                    RefreshMeshBlendshapes();
                    RefreshValidation();
                }

                if (_component != null)
                {
                    EditorGUILayout.LabelField("Profile synced from selected Lip Sync Component.",
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                _component = (ConvaiLipSyncComponent)EditorGUILayout.ObjectField(
                    new GUIContent("Lip Sync Component",
                        "Assign to auto-fill target meshes and mapping from the component."),
                    _component, typeof(ConvaiLipSyncComponent), true);
                if (EditorGUI.EndChangeCheck() && _component != null) SyncFromComponent();

                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                _mapping = (ConvaiLipSyncMapAsset)EditorGUILayout.ObjectField(
                    new GUIContent("Mapping Asset",
                        "Lip Sync Map Asset that defines source-to-target blendshape mappings."),
                    _mapping, typeof(ConvaiLipSyncMapAsset), false);
                if (EditorGUI.EndChangeCheck()) RefreshValidation();

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("TARGET MESHES", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    "SkinnedMeshRenderers that contain the target blendshapes. Add at least one to run validation.",
                    EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(4);
                DrawTargetMeshListEditor();

                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = ConvaiGreen;
                if (GUILayout.Button(
                        new GUIContent("Refresh Validation",
                            "Re-sync from Lip Sync Component (if assigned), re-scan meshes and mapping, then re-validate all entries."),
                        GUILayout.Height(24))) PerformFullRefresh();
                GUI.backgroundColor = Color.white;

                EditorGUI.BeginDisabledGroup(!HasAnyMesh());
                if (GUILayout.Button(new GUIContent("Export Report", "Save validation results to a text file."),
                        _miniButtonStyle, GUILayout.Height(24))) ExportValidationReport();
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            });
        }

        private void DrawTargetMeshListEditor()
        {
            if (_targetMeshes == null) _targetMeshes = new List<SkinnedMeshRenderer>();

            bool changed = false;
            for (int i = 0; i < _targetMeshes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _targetMeshes[i] = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                    $"Target Mesh {i + 1}", _targetMeshes[i], typeof(SkinnedMeshRenderer), true);
                changed |= EditorGUI.EndChangeCheck();

                if (GUILayout.Button(new GUIContent("X", "Remove this mesh from the list"), GUILayout.Width(24)))
                {
                    _targetMeshes.RemoveAt(i);
                    changed = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Add Mesh Slot", "Add another target mesh slot"), _miniButtonStyle,
                    GUILayout.Width(120)))
            {
                _targetMeshes.Add(null);
                changed = true;
            }

            EditorGUILayout.EndHorizontal();

            if (changed)
            {
                RefreshMeshBlendshapes();
                RefreshValidation();
            }
        }

        /// <summary>
        ///     Re-syncs target meshes, mapping asset, and profile from the assigned Lip Sync Component, then refreshes blendshape
        ///     cache and validation.
        /// </summary>
        private void SyncFromComponent()
        {
            if (_component == null) return;

            _targetMeshes = _component.TargetMeshes != null
                ? _component.TargetMeshes.Where(m => m != null).Distinct().ToList()
                : new List<SkinnedMeshRenderer>();
            _mapping = _component.Mapping != null ? _component.Mapping : _component.EffectiveMapping;
            _selectedProfile = GetComponentSelectedProfile();

            RefreshMeshBlendshapes();
            RefreshValidation();
        }

        /// <summary>
        ///     Performs a full refresh: if a Lip Sync Component is assigned, re-syncs meshes/mapping/profile from it; then
        ///     re-scans mesh blendshapes and re-runs validation.
        ///     Ensures all displayed data is up to date when the user clicks Refresh Validation.
        /// </summary>
        private void PerformFullRefresh()
        {
            if (_component != null)
                SyncFromComponent();
            else
            {
                RefreshMeshBlendshapes();
                RefreshValidation();
            }
        }

        private bool HasAnyMesh() => _targetMeshes != null && _targetMeshes.Any(mesh => mesh != null);

        private static LipSyncProfileId DrawProfilePopup(GUIContent label, LipSyncProfileId selectedProfileId)
        {
            IReadOnlyList<ConvaiLipSyncProfile> profiles = LipSyncProfileCatalog.GetProfiles();
            if (profiles == null || profiles.Count == 0)
            {
                EditorGUILayout.LabelField(label, selectedProfileId.ToString());
                return selectedProfileId;
            }

            string normalized = LipSyncProfileId.Normalize(selectedProfileId.Value);
            string[] options = new string[profiles.Count];
            int selectedIndex = -1;
            for (int i = 0; i < profiles.Count; i++)
            {
                ConvaiLipSyncProfile profile = profiles[i];
                options[i] = $"{profile.DisplayName} ({profile.ProfileId})";
                if (string.Equals(profile.ProfileId.Value, normalized, StringComparison.Ordinal)) selectedIndex = i;
            }

            int popupIndex = selectedIndex >= 0 ? selectedIndex : 0;
            int newIndex = EditorGUILayout.Popup(label, popupIndex, options);
            return profiles[Mathf.Clamp(newIndex, 0, profiles.Count - 1)].ProfileId;
        }

        #endregion

        #region Validation Results

        private void DrawValidationResults()
        {
            const float tableHorizontalPadding = 8f;

            if (!HasAnyMesh())
            {
                EditorGUILayout.HelpBox(
                    "Assign at least one target mesh in Configuration to see validation results.",
                    MessageType.Info);
                return;
            }

            if (_validationEntries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No validation data. Click 'Refresh Validation' in Configuration to analyze mappings.",
                    MessageType.Info);
                return;
            }

            _showValidationResults = DrawSectionHeader(SectionValidationResultsId, "VALIDATION RESULTS",
                _showValidationResults, "\u21c4", ConvaiGreen, SectionIconFontSize);
            if (!_showValidationResults) return;

            DrawSectionBackground(() =>
            {
                Rect headerRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
                EditorGUI.DrawRect(headerRect, TableHeaderBg);
                GUILayout.Space(tableHorizontalPadding);
                GUILayout.Label("Index", _tableHeaderLabelStyle, GUILayout.Width(50));
                GUILayout.Label("Source Blendshape", _tableHeaderLabelStyle, GUILayout.Width(250));
                GUILayout.Label("\u2192", _tableHeaderLabelStyle, GUILayout.Width(20));
                GUILayout.Label("Target Blendshape Name(s)", _tableHeaderLabelStyle, GUILayout.Width(250));
                GUILayout.Label("Mult", _tableHeaderLabelStyle, GUILayout.Width(40));
                if (_showLiveValues) GUILayout.Label("Live Value", _tableHeaderLabelStyle, GUILayout.Width(80));
                GUILayout.Label("Status", _tableHeaderLabelStyle, GUILayout.Width(100));
                GUILayout.Space(tableHorizontalPadding);
                EditorGUILayout.EndHorizontal();

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                BlendshapeSnapshot liveSnapshot = default;
                if (_showLiveValues && UnityEngine.Application.isPlaying && _component != null)
                    liveSnapshot = _component.GetBlendshapeSnapshot();

                for (int i = 0; i < _validationEntries.Count; i++)
                {
                    MappingValidationEntry entry = _validationEntries[i];

                    if (_showOnlyProblems && entry.Status == ValidationStatus.Valid) continue;

                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        bool matchesSearch =
                            entry.SourceBlendshape.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!matchesSearch && entry.MappedTargetNames.Count > 0)
                        {
                            matchesSearch = entry.MappedTargetNames.Any(n =>
                                n.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                        }

                        if (!matchesSearch) continue;
                    }

                    if (i % 2 == 0)
                    {
                        Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(22));
                        rowRect.x = tableHorizontalPadding;
                        rowRect.width = Mathf.Max(0f, position.width - (tableHorizontalPadding * 2f));
                        EditorGUI.DrawRect(rowRect, RowAltBg);
                        GUILayout.Space(-22);
                    }

                    DrawValidationRow(entry, liveSnapshot, tableHorizontalPadding);
                }

                EditorGUILayout.EndScrollView();
                DrawValidationSummary();
            });
        }

        private void DrawValidationRow(MappingValidationEntry entry, BlendshapeSnapshot liveSnapshot,
            float horizontalPadding)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(20));
            GUILayout.Space(horizontalPadding);

            EditorGUILayout.LabelField(entry.Index.ToString(), GUILayout.Width(50));
            EditorGUILayout.LabelField(entry.SourceBlendshape, GUILayout.Width(250));

            GUI.color = entry.Status == ValidationStatus.Valid ? ConvaiGreen :
                entry.Status == ValidationStatus.TargetBlendshapeMissing ? ConvaiError : ConvaiWarning;
            EditorGUILayout.LabelField("\u2192", GUILayout.Width(20));
            GUI.color = Color.white;

            string targetDisplay = entry.MappedTargetNames.Count > 0
                ? string.Join(", ", entry.MappedTargetNames)
                : "(none)";

            if (entry.Status == ValidationStatus.TargetBlendshapeMissing)
                GUI.color = ConvaiError;
            else if (entry.Status == ValidationStatus.NoMapping) GUI.color = ConvaiWarning;
            EditorGUILayout.LabelField(targetDisplay, GUILayout.Width(250));
            GUI.color = Color.white;

            string multStr = Math.Abs(entry.Multiplier - 1f) > 0.001f ? $"×{entry.Multiplier:F1}" : "1.0";
            EditorGUILayout.LabelField(multStr, GUILayout.Width(40));

            if (_showLiveValues)
            {
                if (liveSnapshot.IsValid && liveSnapshot.TryGetValue(entry.SourceBlendshape, out float liveValue))
                {
                    Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                        GUILayout.Width(80), GUILayout.Height(14));
                    EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));
                    if (liveValue > 0.001f)
                    {
                        var fillRect = new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(liveValue),
                            barRect.height);
                        Color barColor = Color.Lerp(ConvaiGreen, ConvaiWarning, liveValue);
                        EditorGUI.DrawRect(fillRect, barColor);
                        GUI.Label(barRect, $"{liveValue:F2}", EditorStyles.miniLabel);
                    }
                }
                else
                    EditorGUILayout.LabelField("-", GUILayout.Width(80));
            }

            DrawStatusBadge(entry.Status, entry.StatusMessage);
            GUILayout.Space(horizontalPadding);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusBadge(ValidationStatus status, string message)
        {
            string text;
            Color color;

            switch (status)
            {
                case ValidationStatus.Valid:
                    text = "✓ OK";
                    color = ConvaiGreen;
                    break;
                case ValidationStatus.NoMapping:
                    text = "⚠ No Map";
                    color = ConvaiWarning;
                    break;
                case ValidationStatus.TargetBlendshapeMissing:
                    text = "Target Missing";
                    color = ConvaiError;
                    break;
                case ValidationStatus.Disabled:
                    text = "○ Disabled";
                    color = new Color(0.5f, 0.5f, 0.5f);
                    break;
                case ValidationStatus.MultipleTargets:
                    text = "◉ Multi";
                    color = ConvaiInfo;
                    break;
                default:
                    text = "?";
                    color = Color.white;
                    break;
            }

            GUI.color = color;
            var content = new GUIContent(text, message);
            EditorGUILayout.LabelField(content, GUILayout.Width(100));
            GUI.color = Color.white;
        }

        private void DrawValidationSummary()
        {
            int total = _validationEntries.Count;
            int valid = _validationEntries.Count(e => e.Status == ValidationStatus.Valid);
            int noMapping = _validationEntries.Count(e => e.Status == ValidationStatus.NoMapping);
            int missingTarget = _validationEntries.Count(e => e.Status == ValidationStatus.TargetBlendshapeMissing);
            int disabled = _validationEntries.Count(e => e.Status == ValidationStatus.Disabled);
            int issues = noMapping + missingTarget;

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(StatBarHeight));

            GUILayout.Space(10);
            DrawStatCell("Total", total.ToString(), Color.white);
            GUILayout.Space(StatCellGap);
            DrawStatCell("Valid", valid.ToString(), ConvaiGreen);
            GUILayout.Space(StatCellGap);
            DrawStatCell("Issues", issues.ToString(), issues > 0 ? ConvaiError : ConvaiGreen);
            GUILayout.Space(StatCellGap);
            DrawStatCell("Disabled", disabled.ToString(),
                disabled > 0 ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.78f, 0.78f, 0.78f));
            GUILayout.FlexibleSpace();

            int meshCount = GetTotalMeshBlendshapeCount();
            if (meshCount >= 0) DrawStatBarRightLabel($"{meshCount} blendshapes", ConvaiGreenLight);
            GUILayout.Space(10);

            EditorGUILayout.EndHorizontal();
        }

        private int GetTotalMeshBlendshapeCount()
        {
            int count = 0;
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_targetMeshes != null)
            {
                for (int meshIndex = 0; meshIndex < _targetMeshes.Count; meshIndex++)
                {
                    SkinnedMeshRenderer mesh = _targetMeshes[meshIndex];
                    if (mesh == null || mesh.sharedMesh == null) continue;

                    for (int i = 0; i < mesh.sharedMesh.blendShapeCount; i++)
                    {
                        if (unique.Add(mesh.sharedMesh.GetBlendShapeName(i)))
                            count++;
                    }
                }
            }

            return HasAnyMesh() ? count : -1;
        }

        #endregion

        #region Validation Logic

        private void RefreshMeshBlendshapes()
        {
            _meshBlendshapeNames.Clear();
            _meshNameSet.Clear();

            void AddMesh(SkinnedMeshRenderer mesh)
            {
                if (mesh == null || mesh.sharedMesh == null) return;
                Mesh m = mesh.sharedMesh;
                for (int i = 0; i < m.blendShapeCount; i++)
                {
                    string name = m.GetBlendShapeName(i);
                    if (_meshNameSet.Add(name)) _meshBlendshapeNames.Add(name);
                    string withoutPrefix = name.Contains(".") ? name.Substring(name.LastIndexOf('.') + 1) : null;
                    if (withoutPrefix != null && !_meshNameSet.Contains(withoutPrefix)) _meshNameSet.Add(withoutPrefix);
                }
            }

            if (_targetMeshes != null)
            {
                for (int i = 0; i < _targetMeshes.Count; i++)
                    AddMesh(_targetMeshes[i]);
            }
        }

        private void TryAutoBindComponent()
        {
            if (_component != null) return;

            var found = FindAnyObjectByType<ConvaiLipSyncComponent>();
            if (found == null) return;

            _component = found;
            SyncFromComponent();
        }

        private LipSyncProfileId GetComponentSelectedProfile()
        {
            if (_component == null) return _selectedProfile;
            return _component.LockedProfile;
        }

        private void RefreshValidation()
        {
            _validationEntries.Clear();

            IReadOnlyList<string> sourceBlendshapes = ResolveSourceBlendshapesForValidation();

            for (int i = 0; i < sourceBlendshapes.Count; i++)
            {
                string sourceBlendshape = sourceBlendshapes[i];
                var entry = new MappingValidationEntry { Index = i, SourceBlendshape = sourceBlendshape };

                if (_mapping != null)
                {
                    IReadOnlyList<string> targetNames = _mapping.GetTargetNames(sourceBlendshape);
                    bool isEnabled = _mapping.IsEnabled(sourceBlendshape);

                    entry.MappedTargetNames = targetNames?.ToList() ?? new List<string>();
                    entry.IsEnabled = isEnabled;

                    if (_mapping.TryGetEntry(sourceBlendshape,
                            out ConvaiLipSyncMapAsset.BlendshapeMappingSnapshot mapEntry))
                    {
                        entry.Multiplier = mapEntry.Multiplier;
                        entry.Offset = mapEntry.Offset;
                    }

                    if (!isEnabled)
                    {
                        entry.Status = ValidationStatus.Disabled;
                        entry.StatusMessage = "Mapping is disabled";
                    }
                    else if (entry.MappedTargetNames.Count == 0)
                    {
                        entry.Status = ValidationStatus.NoMapping;
                        entry.StatusMessage = "No target names defined in mapping";
                    }
                    else
                    {
                        bool allFound = true;
                        var missingNames = new List<string>();

                        foreach (string targetName in entry.MappedTargetNames)
                        {
                            if (!_meshNameSet.Contains(targetName))
                            {
                                allFound = false;
                                missingNames.Add(targetName);
                            }
                        }

                        if (allFound)
                        {
                            entry.Status = entry.MappedTargetNames.Count > 1
                                ? ValidationStatus.MultipleTargets
                                : ValidationStatus.Valid;
                            entry.StatusMessage = entry.MappedTargetNames.Count > 1
                                ? $"Maps to {entry.MappedTargetNames.Count} targets"
                                : "Mapping is valid";
                        }
                        else
                        {
                            entry.Status = ValidationStatus.TargetBlendshapeMissing;
                            entry.StatusMessage = $"Missing target blendshape(s): {string.Join(", ", missingNames)}";
                        }
                    }
                }
                else
                {
                    if (_meshNameSet.Contains(sourceBlendshape))
                    {
                        entry.Status = ValidationStatus.Valid;
                        entry.MappedTargetNames.Add(sourceBlendshape);
                        entry.StatusMessage = "Direct name match (no mapping)";
                    }
                    else
                    {
                        entry.Status = ValidationStatus.NoMapping;
                        entry.StatusMessage = "No mapping and no direct blendshape match on target meshes";
                    }
                }

                _validationEntries.Add(entry);
            }
        }

        private IReadOnlyList<string> ResolveSourceBlendshapesForValidation()
        {
            if (_mapping != null)
            {
                IReadOnlyList<string> mappedNames = _mapping.GetSourceBlendshapeNames();
                if (mappedNames != null && mappedNames.Count > 0) return mappedNames;
            }

            return LipSyncBuiltInProfileLibrary.GetSourceBlendshapeNamesOrEmpty(_selectedProfile);
        }

        #endregion
    }
}
#endif
