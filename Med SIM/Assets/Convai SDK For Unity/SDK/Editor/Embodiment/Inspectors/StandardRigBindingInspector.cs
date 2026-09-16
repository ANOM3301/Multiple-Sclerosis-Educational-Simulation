using Convai.Domain.Embodiment.Semantics;
using Convai.Editor.Inspectors;
using Convai.Runtime.Animation;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Embodiment.Inspectors
{
    /// <summary>
    ///     Custom inspector for <see cref="StandardRigBinding" />. Adds a live resolution
    ///     preview showing which bones and blendshapes the binding currently maps to.
    /// </summary>
    [CustomEditor(typeof(StandardRigBinding))]
    public sealed class StandardRigBindingInspector : UnityEditor.Editor
    {
        private bool _showBones = true;
        private bool _showBlendshapes;

        public override void OnInspectorGUI()
        {
            ConvaiBrandedInspectorChrome.DrawHeader("Standard Rig Binding", "Semantic Rig Resolution");
            DrawDefaultInspector();

            StandardRigBinding binding = (StandardRigBinding)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Detection", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Convention", binding.DetectedConvention.ToString());
                EditorGUILayout.LabelField(
                    "Detection Confidence",
                    binding.DetectionConfidence.ToString("0.00"));
            }

            if (GUILayout.Button("Rebuild Resolution Tables"))
            {
                Undo.RecordObject(binding, "Rebuild Rig Binding");
                binding.Rebuild();
                EditorUtility.SetDirty(binding);
            }

            EditorGUILayout.Space();

            _showBones = EditorGUILayout.Foldout(_showBones, "Bone Resolution", true);
            if (_showBones) DrawBoneTable(binding);

            _showBlendshapes = EditorGUILayout.Foldout(_showBlendshapes, "Blendshape Resolution", true);
            if (_showBlendshapes) DrawBlendshapeTable(binding);
        }

        private static void DrawBoneTable(StandardRigBinding binding)
        {
            foreach (StandardBone bone in System.Enum.GetValues(typeof(StandardBone)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(bone.ToString(), GUILayout.Width(140f));
                    bool resolved = binding.TryGetBone(bone, out Transform bt);
                    EditorGUILayout.LabelField(resolved ? bt.name : "<unresolved>");
                }
            }
        }

        private static void DrawBlendshapeTable(StandardRigBinding binding)
        {
            foreach (StandardBlendshape shape in System.Enum.GetValues(typeof(StandardBlendshape)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(shape.ToString(), GUILayout.Width(160f));
                    bool resolved = binding.TryGetBlendshape(shape, out SkinnedMeshRenderer mesh, out int idx);
                    string text = resolved ? $"{mesh.name}  [{idx}]" : "<unresolved>";
                    EditorGUILayout.LabelField(text);
                }
            }
        }
    }
}
