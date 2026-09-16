using System.Collections.Generic;
using Convai.Runtime.Actions;
using Convai.Runtime.Components;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Inspectors
{
    [CustomEditor(typeof(ConvaiActionConfigSource))]
    public sealed class ConvaiActionConfigSourceEditor : UnityEditor.Editor
    {
        private IReadOnlyList<ConvaiActionConfigDiagnostic> _diagnostics;

        private void OnEnable() => RefreshDiagnostics();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Validate Action Config"))
                RefreshDiagnostics();

            DrawRenderedPreview();
            DrawDiagnostics();
        }

        private void RefreshDiagnostics()
        {
            _diagnostics = ConvaiActionConfigValidator.Validate(target as ConvaiActionConfigSource);
        }

        private void DrawDiagnostics()
        {
            if (_diagnostics == null)
                _diagnostics = ConvaiActionConfigValidator.Validate(target as ConvaiActionConfigSource);

            if (_diagnostics.Count == 0)
            {
                EditorGUILayout.HelpBox("Action config validation passed.", MessageType.Info);
                return;
            }

            int errorCount = 0;
            int warningCount = 0;
            for (int i = 0; i < _diagnostics.Count; i++)
            {
                switch (_diagnostics[i].Severity)
                {
                    case ConvaiActionConfigDiagnosticSeverity.Error:
                        errorCount++;
                        break;
                    case ConvaiActionConfigDiagnosticSeverity.Warning:
                        warningCount++;
                        break;
                }
            }

            EditorGUILayout.HelpBox(
                $"Action config validation found {errorCount} error(s) and {warningCount} warning(s).",
                errorCount > 0 ? MessageType.Error : MessageType.Warning);

            for (int i = 0; i < _diagnostics.Count; i++)
            {
                ConvaiActionConfigDiagnostic diagnostic = _diagnostics[i];
                MessageType messageType = diagnostic.Severity switch
                {
                    ConvaiActionConfigDiagnosticSeverity.Error => MessageType.Error,
                    ConvaiActionConfigDiagnosticSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };

                EditorGUILayout.HelpBox(diagnostic.ToString(), messageType);
            }
        }

        private void DrawRenderedPreview()
        {
            if (target is not ConvaiActionConfigSource source ||
                source.Definitions == null ||
                source.Definitions.Count == 0)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Backend Preview", EditorStyles.boldLabel);
            for (int i = 0; i < source.Definitions.Count; i++)
            {
                ConvaiActionDefinition definition = source.Definitions[i];
                string preview = definition?.ToActionConfigString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(preview))
                    preview = "<invalid action template>";

                EditorGUILayout.HelpBox(preview, MessageType.None);
                EditorGUILayout.LabelField("Failure Policy Override", definition?.FailurePolicyOverride.ToString() ?? "Unknown");
            }
        }
    }
}
