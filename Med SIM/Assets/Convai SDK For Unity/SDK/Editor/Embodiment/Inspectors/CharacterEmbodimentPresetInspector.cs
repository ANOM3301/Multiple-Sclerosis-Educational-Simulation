using System.Collections.Generic;
using Convai.Editor.Inspectors;
using Convai.Modules.Embodiment.Presets;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Embodiment.Inspectors
{
    /// <summary>
    ///     Custom inspector for <see cref="CharacterEmbodimentPreset" />. Adds per-slot
    ///     edit-time validation: duplicate IDs, unknown IDs, null profiles, and wrong profile
    ///     types are flagged inline before the character enters play mode.
    /// </summary>
    [CustomEditor(typeof(CharacterEmbodimentPreset))]
    internal sealed class CharacterEmbodimentPresetInspector : ConvaiEmbodimentProfileEditorBase<CharacterEmbodimentPreset>
    {
        internal const string SectionIdentity = "Identity";
        internal const string SectionSlots = "ProfileSlots";
        internal const string SectionDiagnostics = "SlotDiagnostics";

        private bool _showIdentity;
        private bool _showSlots;
        private bool _showDiagnostics;

        protected override string HeaderTitle => "Character Embodiment Preset";
        protected override string HeaderSubtitle => "Module Profile Map";
        protected override string HeaderStatus => GetHeaderStatus();
        protected override Color HeaderStatusColor => GetHeaderStatusColor();

        protected override void OnEnable()
        {
            base.OnEnable();
            _showIdentity = LoadSectionState(SectionIdentity, true);
            _showSlots = LoadSectionState(SectionSlots, true);
            _showDiagnostics = LoadSectionState(SectionDiagnostics, true);
        }

        protected override void OnDisable()
        {
            SaveSectionState(SectionIdentity, _showIdentity);
            SaveSectionState(SectionSlots, _showSlots);
            SaveSectionState(SectionDiagnostics, _showDiagnostics);
            base.OnDisable();
        }

        protected override void DrawProfileInspector()
        {
            _showIdentity = DrawProfileSection(SectionIdentity, "IDENTITY", _showIdentity,
                ConvaiInspectorIconIds.Content);
            if (_showIdentity)
            {
                DrawSectionBody(() =>
                {
                    DrawProperty("presetId", "Preset ID");
                    DrawProperty("description");
                });
            }

            _showSlots = DrawProfileSection(SectionSlots, "PROFILE SLOTS", _showSlots,
                ConvaiInspectorIconIds.Routing);
            if (_showSlots)
                DrawSectionBody(() => DrawProperty("profileSlots"));

            _showDiagnostics = DrawProfileSection(SectionDiagnostics, "SLOT DIAGNOSTICS", _showDiagnostics,
                ConvaiInspectorIconIds.Validation, Warning);
            if (_showDiagnostics)
                DrawSectionBody(() => DrawSlotDiagnostics(Profile));
        }

        private void DrawSlotDiagnostics(CharacterEmbodimentPreset preset)
        {
            IReadOnlyList<EmbodimentProfileSlot> slots = preset.ProfileSlots;
            List<EmbodimentPresetSlotDiagnostic> diagnostics = EmbodimentPresetSlotDiagnostics.Analyze(slots);
            bool hasIssues = diagnostics.Count > 0;

            if (slots == null || slots.Count == 0)
            {
                DrawWarningBox("No Slots", diagnostics[0].Message);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    EmbodimentProfileSlot slot = slots[i];
                    if (slot == null) continue;

                    bool emptyId = string.IsNullOrWhiteSpace(slot.ModuleId);
                    bool nullProfile = slot.Profile == null;
                    bool slotHasError = HasDiagnostic(diagnostics, i, EmbodimentPresetSlotSeverity.Error);
                    bool slotHasWarning = HasDiagnostic(diagnostics, i, EmbodimentPresetSlotSeverity.Warning);

                    bool slotOk = !emptyId && !slotHasError && !slotHasWarning;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIContent statusIcon = slotOk
                            ? EditorGUIUtility.IconContent("TestPassed")
                            : EditorGUIUtility.IconContent("console.warnicon.sml");

                        GUILayout.Label(statusIcon, GUILayout.Width(20f), GUILayout.Height(18f));

                        string label = emptyId ? $"[{i}] <empty id>" : slot.ModuleId;
                        EditorGUILayout.LabelField(label, GUILayout.MinWidth(160f));

                        if (slot.Profile != null)
                            EditorGUILayout.LabelField(slot.Profile.name, EditorStyles.miniLabel);
                        else
                            EditorGUILayout.LabelField("null", EditorStyles.miniLabel);
                    }

                    DrawDiagnosticsForSlot(diagnostics, i);
                }

                if (!hasIssues)
                    DrawInfoBox("All Slots Validated", "Every built-in slot has a recognized module ID and compatible profile type.");
            }
        }

        private static bool HasDiagnostic(IReadOnlyList<EmbodimentPresetSlotDiagnostic> diagnostics, int slotIndex,
            EmbodimentPresetSlotSeverity severity)
        {
            for (int i = 0; i < diagnostics.Count; i++)
                if (diagnostics[i].SlotIndex == slotIndex && diagnostics[i].Severity == severity)
                    return true;
            return false;
        }

        private static void DrawDiagnosticsForSlot(IReadOnlyList<EmbodimentPresetSlotDiagnostic> diagnostics, int slotIndex)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                EmbodimentPresetSlotDiagnostic diagnostic = diagnostics[i];
                if (diagnostic.SlotIndex != slotIndex) continue;

                MessageType type = diagnostic.Severity == EmbodimentPresetSlotSeverity.Error
                    ? MessageType.Error
                    : MessageType.Warning;
                EditorGUILayout.HelpBox(diagnostic.Message, type);
            }
        }

        private string GetHeaderStatus()
        {
            List<EmbodimentPresetSlotDiagnostic> diagnostics = EmbodimentPresetSlotDiagnostics.Analyze(Profile.ProfileSlots);
            if (EmbodimentPresetSlotDiagnostics.HasErrors(diagnostics)) return "Error";
            return diagnostics.Count > 0 ? "Warning" : "Ready";
        }

        private Color GetHeaderStatusColor()
        {
            List<EmbodimentPresetSlotDiagnostic> diagnostics = EmbodimentPresetSlotDiagnostics.Analyze(Profile.ProfileSlots);
            if (EmbodimentPresetSlotDiagnostics.HasErrors(diagnostics)) return Error;
            return diagnostics.Count > 0 ? Warning : AccentEmphasis;
        }
    }
}
