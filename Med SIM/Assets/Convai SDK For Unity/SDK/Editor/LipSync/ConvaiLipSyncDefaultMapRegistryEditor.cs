#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Convai.Domain.Models.LipSync;
using Convai.Editor.Inspectors;
using Convai.Editor.UI;
using Convai.Modules.LipSync.Profiles;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Convai.Modules.LipSync.Editor
{
    [CustomEditor(typeof(ConvaiLipSyncDefaultMapRegistry))]
    internal sealed class ConvaiLipSyncDefaultMapRegistryEditor : UnityEditor.Editor
    {
        private static readonly Color Accent = ConvaiInspectorThemeTokens.Accent;
        private static readonly Color HeaderBackground = ConvaiInspectorThemeTokens.HeaderBackground;
        private Texture2D _convaiIcon;
        private ReorderableList _entriesList;

        private SerializedProperty _entriesProp;
        private GUIStyle _headerStyle;
        private GUIStyle _issueBadgeStyle;
        private GUIStyle _statLabelStyle;
        private GUIStyle _statValueStyle;
        private bool _stylesInitialized;

        private void OnEnable()
        {
            _entriesProp = serializedObject.FindProperty("_entries");
            _convaiIcon = ConvaiBrandedIconProvider.GetConvaiIcon();
            InitializeList();
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();
            serializedObject.Update();

            var registry = (ConvaiLipSyncDefaultMapRegistry)target;
            IReadOnlyList<ConvaiLipSyncProfile> catalogProfiles = LipSyncProfileCatalog.GetProfiles();
            int totalEntries = _entriesProp != null ? _entriesProp.arraySize : 0;
            int coveredProfiles = CountCoveredCatalogProfiles(catalogProfiles);
            int validationIssueCount = registry.ValidationIssues.Count;
            DrawRegistryHeader(totalEntries, catalogProfiles.Count, coveredProfiles, validationIssueCount);
            DrawToolbar();
            EditorGUILayout.Space(4f);

            if (_entriesList != null) _entriesList.DoLayoutList();

            bool applied = serializedObject.ApplyModifiedProperties();
            if (applied) EditorUtility.SetDirty(target);

            DrawValidationIssues();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14, normal = { textColor = new Color(0.435f, 0.812f, 0.592f) }
            };

            _statValueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 15
            };
            _statValueStyle.normal.textColor = Accent;

            _statLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            _statLabelStyle.normal.textColor = new Color(0.72f, 0.72f, 0.76f);

            _issueBadgeStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight };
            _stylesInitialized = true;
        }

        private void InitializeList()
        {
            if (_entriesProp == null) return;

            _entriesList = new ReorderableList(serializedObject, _entriesProp, true, true, true, true)
            {
                elementHeight = (EditorGUIUtility.singleLineHeight * 2f) + 10f
            };

            _entriesList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Default Map Entries");
            };

            _entriesList.drawElementCallback = (rect, index, _, _) =>
            {
                SerializedProperty entry = _entriesProp.GetArrayElementAtIndex(index);
                SerializedProperty profileIdProp = entry.FindPropertyRelative("_profileId");
                SerializedProperty mapProp = entry.FindPropertyRelative("_defaultMap");

                var map = mapProp.objectReferenceValue as ConvaiLipSyncMapAsset;
                LipSyncProfileId mapProfile = map != null ? map.TargetProfileId : default;
                string fallbackProfile = LipSyncProfileId.Normalize(profileIdProp.stringValue);
                bool hasAuthoritativeProfile = map != null && mapProfile.IsValid;
                LipSyncProfileId resolvedProfile = hasAuthoritativeProfile
                    ? mapProfile
                    : new LipSyncProfileId(fallbackProfile);
                string displayName = ResolveDisplayProfileName(resolvedProfile);

                Rect mapRect = new(rect.x, rect.y + 2f, rect.width - 42f, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(mapRect, mapProp, GUIContent.none);

                Rect openRect = new(rect.xMax - 42f, rect.y + 2f, 40f, EditorGUIUtility.singleLineHeight);
                using (new EditorGUI.DisabledScope(map == null))
                {
                    if (GUI.Button(openRect, "Open"))
                        Selection.activeObject = map;
                }

                Rect profileRect = new(rect.x, rect.y + EditorGUIUtility.singleLineHeight + 6f, rect.width,
                    EditorGUIUtility.singleLineHeight);
                if (hasAuthoritativeProfile)
                    EditorGUI.LabelField(profileRect, $"Profile: {displayName}");
                else
                {
                    Rect fallbackFieldRect = new(rect.x, profileRect.y, rect.width * 0.52f, profileRect.height);
                    Rect previewRect = new(rect.x + (rect.width * 0.54f), profileRect.y, rect.width * 0.46f,
                        profileRect.height);
                    EditorGUI.PropertyField(
                        fallbackFieldRect,
                        profileIdProp,
                        new GUIContent("Fallback ID"));
                    EditorGUI.LabelField(previewRect, $"Profile: {displayName}");
                }
            };
        }

        private void DrawRegistryHeader(int totalEntries, int catalogProfileCount, int coveredProfiles,
            int validationIssueCount)
        {
            const float headerHeight = 58f;
            const float iconSize = 22f;
            const float iconTextSpacing = 6f;
            const float metricCellWidth = 72f;
            const float metricCellGap = 4f;
            const float metricValueHeight = 16f;
            const float metricLabelHeight = 12f;
            const float metricValueLabelGap = 0f;
            float metricGroupHeight = metricValueHeight + metricValueLabelGap + metricLabelHeight;
            const float leftRegionWidth = 360f;

            Rect headerRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(
                new Rect(headerRect.x - 18f, headerRect.y - 4f, headerRect.width + 36f, headerHeight + 4f),
                HeaderBackground);

            EditorGUILayout.BeginVertical(GUILayout.Height(headerHeight));
            Rect rowRect = GUILayoutUtility.GetRect(0f, headerHeight, GUILayout.ExpandWidth(true),
                GUILayout.Height(headerHeight));
            string issueText = validationIssueCount > 0 ? $"{validationIssueCount} Issues" : string.Empty;
            float rightWidth = validationIssueCount > 0
                ? Mathf.Max(120f, _issueBadgeStyle.CalcSize(new GUIContent(issueText)).x + 8f)
                : 0f;
            var leftRegionRect = new Rect(rowRect.x, rowRect.y, Mathf.Min(leftRegionWidth, rowRect.width),
                rowRect.height);
            Rect rightRegionRect = validationIssueCount > 0
                ? new Rect(rowRect.xMax - rightWidth, rowRect.y, rightWidth, rowRect.height)
                : new Rect(rowRect.xMax, rowRect.y, 0f, rowRect.height);
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
            GUI.Label(titleRect, "Lip Sync Default Map Registry", _headerStyle);

            if (validationIssueCount > 0)
            {
                _issueBadgeStyle.normal.textColor = new Color(1f, 0.655f, 0.149f);
                GUI.Label(rightRegionRect, issueText, _issueBadgeStyle);
            }

            float coverage = catalogProfileCount > 0
                ? coveredProfiles / (float)catalogProfileCount * 100f
                : 0f;

            float totalMetricsWidth = (metricCellWidth * 4f) + (metricCellGap * 3f);
            float idealMetricsStartX = rowRect.center.x - (totalMetricsWidth * 0.5f);
            float minMetricsStartX = leftRegionRect.xMax + 8f;
            float maxMetricsStartX = validationIssueCount > 0
                ? rightRegionRect.xMin - totalMetricsWidth - 8f
                : rowRect.xMax - totalMetricsWidth - 8f;
            float metricsStartX = Mathf.Clamp(idealMetricsStartX, minMetricsStartX,
                Mathf.Max(minMetricsStartX, maxMetricsStartX));
            float metricsStartY =
                centerRegionRect.y + Mathf.Max(0f, (centerRegionRect.height - metricGroupHeight) * 0.5f);

            DrawMetricCell(
                new Rect(metricsStartX + ((metricCellWidth + metricCellGap) * 0f), metricsStartY, metricCellWidth,
                    metricGroupHeight), "Total", totalEntries.ToString(), metricValueHeight, metricValueLabelGap,
                metricLabelHeight);
            DrawMetricCell(
                new Rect(metricsStartX + ((metricCellWidth + metricCellGap) * 1f), metricsStartY, metricCellWidth,
                    metricGroupHeight), "Profiles", catalogProfileCount.ToString(), metricValueHeight,
                metricValueLabelGap, metricLabelHeight);
            DrawMetricCell(
                new Rect(metricsStartX + ((metricCellWidth + metricCellGap) * 2f), metricsStartY, metricCellWidth,
                    metricGroupHeight), "Mapped", coveredProfiles.ToString(), metricValueHeight, metricValueLabelGap,
                metricLabelHeight);
            DrawMetricCell(
                new Rect(metricsStartX + ((metricCellWidth + metricCellGap) * 3f), metricsStartY, metricCellWidth,
                    metricGroupHeight), "Coverage", $"{coverage:0.#}%", metricValueHeight, metricValueLabelGap,
                metricLabelHeight);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            GUILayout.Space(6f);
        }

        private void DrawMetricCell(Rect cellRect, string label, string value, float valueHeight, float valueLabelGap,
            float labelHeight)
        {
            var valueRect = new Rect(cellRect.x, cellRect.y, cellRect.width, valueHeight);
            var labelRect = new Rect(cellRect.x, valueRect.yMax + valueLabelGap, cellRect.width, labelHeight);
            GUI.Label(valueRect, value, _statValueStyle);
            GUI.Label(labelRect, label, _statLabelStyle);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Missing Profiles", GUILayout.Height(22f))) AddMissingProfilesFromCatalog();

            if (GUILayout.Button("Remove Empty", GUILayout.Height(22f))) RemoveEmptyEntries();

            if (GUILayout.Button("Sort by Profile", GUILayout.Height(22f))) SortEntriesByResolvedProfile();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeaderMetric(string label, string value)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(90f));
            GUILayout.Label(value, _statValueStyle);
            GUILayout.Label(label, _statLabelStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawValidationIssues()
        {
            var registry = (ConvaiLipSyncDefaultMapRegistry)target;
            IReadOnlyList<string> issues = registry.ValidationIssues;
            if (issues == null || issues.Count == 0) return;

            EditorGUILayout.HelpBox("Validation issues detected. Resolve these before shipping.", MessageType.Warning);
            for (int i = 0; i < issues.Count; i++)
                EditorGUILayout.LabelField($"- {issues[i]}", EditorStyles.wordWrappedMiniLabel);
        }

        private int CountCoveredCatalogProfiles(IReadOnlyList<ConvaiLipSyncProfile> catalogProfiles)
        {
            if (catalogProfiles == null || catalogProfiles.Count == 0) return 0;

            var registry = (ConvaiLipSyncDefaultMapRegistry)target;
            int covered = 0;
            for (int i = 0; i < catalogProfiles.Count; i++)
            {
                ConvaiLipSyncProfile profile = catalogProfiles[i];
                if (profile == null) continue;

                if (registry.GetForProfile(profile.ProfileId) != null) covered++;
            }

            return covered;
        }

        private static string ResolveDisplayProfileName(LipSyncProfileId profileId)
        {
            if (LipSyncProfileCatalog.TryGetProfile(profileId, out ConvaiLipSyncProfile profile))
                return profile.DisplayName;

            return profileId.IsValid ? profileId.Value : "(Unresolved)";
        }

        private void AddMissingProfilesFromCatalog()
        {
            IReadOnlyList<ConvaiLipSyncProfile> profiles = LipSyncProfileCatalog.GetProfiles();
            if (profiles == null || profiles.Count == 0 || _entriesProp == null) return;

            HashSet<string> existingProfiles = new(StringComparer.Ordinal);
            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                SerializedProperty entry = _entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty mapProp = entry.FindPropertyRelative("_defaultMap");
                SerializedProperty profileIdProp = entry.FindPropertyRelative("_profileId");
                var map = mapProp.objectReferenceValue as ConvaiLipSyncMapAsset;
                LipSyncProfileId resolved = map != null && map.TargetProfileId.IsValid
                    ? map.TargetProfileId
                    : new LipSyncProfileId(profileIdProp.stringValue);
                if (resolved.IsValid) existingProfiles.Add(resolved.Value);
            }

            int added = 0;
            for (int i = 0; i < profiles.Count; i++)
            {
                ConvaiLipSyncProfile profile = profiles[i];
                if (profile == null || !profile.ProfileId.IsValid ||
                    existingProfiles.Contains(profile.ProfileId.Value)) continue;

                int index = _entriesProp.arraySize;
                _entriesProp.InsertArrayElementAtIndex(index);
                SerializedProperty entry = _entriesProp.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_profileId").stringValue = profile.ProfileId.Value;
                entry.FindPropertyRelative("_defaultMap").objectReferenceValue = null;
                added++;
            }

            if (added > 0)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
        }

        private void RemoveEmptyEntries()
        {
            if (_entriesProp == null) return;

            bool removed = false;
            for (int i = _entriesProp.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty entry = _entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty mapProp = entry.FindPropertyRelative("_defaultMap");
                SerializedProperty profileIdProp = entry.FindPropertyRelative("_profileId");
                if (mapProp.objectReferenceValue != null) continue;

                if (!string.IsNullOrWhiteSpace(profileIdProp.stringValue)) continue;

                _entriesProp.DeleteArrayElementAtIndex(i);
                removed = true;
            }

            if (removed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
        }

        private void SortEntriesByResolvedProfile()
        {
            if (_entriesProp == null || _entriesProp.arraySize <= 1) return;

            List<EntrySnapshot> snapshots = new(_entriesProp.arraySize);
            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                SerializedProperty entry = _entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty profileIdProp = entry.FindPropertyRelative("_profileId");
                SerializedProperty mapProp = entry.FindPropertyRelative("_defaultMap");
                var map = mapProp.objectReferenceValue as ConvaiLipSyncMapAsset;
                string resolvedProfile = map != null && map.TargetProfileId.IsValid
                    ? map.TargetProfileId.Value
                    : LipSyncProfileId.Normalize(profileIdProp.stringValue);
                snapshots.Add(new EntrySnapshot(resolvedProfile, profileIdProp.stringValue, map));
            }

            snapshots.Sort((left, right) =>
            {
                int profileCompare = string.CompareOrdinal(left.ResolvedProfile, right.ResolvedProfile);
                if (profileCompare != 0) return profileCompare;

                string leftName = left.Map != null ? left.Map.name : string.Empty;
                string rightName = right.Map != null ? right.Map.name : string.Empty;
                return string.CompareOrdinal(leftName, rightName);
            });

            for (int i = 0; i < snapshots.Count; i++)
            {
                SerializedProperty entry = _entriesProp.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_profileId").stringValue = snapshots[i].ProfileId;
                entry.FindPropertyRelative("_defaultMap").objectReferenceValue = snapshots[i].Map;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private readonly struct EntrySnapshot
        {
            public EntrySnapshot(string resolvedProfile, string profileId, ConvaiLipSyncMapAsset map)
            {
                ResolvedProfile = resolvedProfile ?? string.Empty;
                ProfileId = profileId ?? string.Empty;
                Map = map;
            }

            public string ResolvedProfile { get; }
            public string ProfileId { get; }
            public ConvaiLipSyncMapAsset Map { get; }
        }
    }
}
#endif
