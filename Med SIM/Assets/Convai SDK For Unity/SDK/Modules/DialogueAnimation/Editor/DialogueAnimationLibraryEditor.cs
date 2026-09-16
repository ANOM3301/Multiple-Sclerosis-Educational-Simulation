using Convai.Modules.DialogueAnimation.Core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Editor
{
    /// <summary>
    ///     Custom inspector for <see cref="DialogueAnimationLibrary" /> that renders the
    ///     idle and talk pools as reorderable lists and surfaces lint warnings (missing
    ///     clip, non-looping animation, duplicate clip).
    /// </summary>
    [CustomEditor(typeof(DialogueAnimationLibrary))]
    public sealed class DialogueAnimationLibraryEditor : UnityEditor.Editor
    {
        private SerializedProperty _idles;
        private SerializedProperty _talks;
        private SerializedProperty _defaultCrossFade;

        private ReorderableList _idleList;
        private ReorderableList _talkList;

        private void OnEnable()
        {
            _idles = serializedObject.FindProperty("_idles");
            _talks = serializedObject.FindProperty("_talks");
            _defaultCrossFade = serializedObject.FindProperty("_defaultCrossFadeDuration");

            _idleList = BuildList(_idles, "Idle Pool");
            _talkList = BuildList(_talks, "Talk Pool");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_defaultCrossFade);
            EditorGUILayout.Space(6f);

            _idleList.DoLayoutList();
            EditorGUILayout.Space(4f);
            _talkList.DoLayoutList();

            DrawLintWarnings((DialogueAnimationLibrary)target);

            serializedObject.ApplyModifiedProperties();
        }

        private ReorderableList BuildList(SerializedProperty arrayProp, string header)
        {
            var list = new ReorderableList(serializedObject, arrayProp,
                draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, header, EditorStyles.boldLabel),

                elementHeightCallback = index =>
                {
                    SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
                    return EditorGUI.GetPropertyHeight(element, includeChildren: true) + 4f;
                },

                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
                    rect.y += 2f;
                    rect.height -= 4f;
                    EditorGUI.PropertyField(rect, element, new GUIContent($"Clip {index}"), true);
                }
            };
            return list;
        }

        private static void DrawLintWarnings(DialogueAnimationLibrary library)
        {
            if (library == null) return;

            if (!library.HasAnyValidIdle())
                EditorGUILayout.HelpBox(
                    "No valid idle clip is assigned. The controller needs at least one idle clip " +
                    "for the idle overlay layer (and optionally the base foundation clip).",
                    MessageType.Warning);

            if (!library.HasAnyValidTalk())
                EditorGUILayout.HelpBox(
                    "No valid talk clip is assigned. The character will hold the current idle " +
                    "during speech turns.", MessageType.Info);

            WarnIfClipsNotLooping(library.IdleEntries, "idle");
            WarnIfClipsNotLooping(library.TalkEntries, "talk");
        }

        private static void WarnIfClipsNotLooping(
            System.Collections.Generic.IReadOnlyList<DialogueClipEntry> pool,
            string poolLabel)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                DialogueClipEntry entry = pool[i];
                if (entry.Clip == null) continue;
                if (entry.Clip.isLooping) continue;

                EditorGUILayout.HelpBox(
                    $"{poolLabel} clip '{entry.Clip.name}' is not marked as looping. The " +
                    $"controller holds each variant for several seconds; non-looping clips will " +
                    $"snap to their last frame. Enable 'Loop Time' on the clip import " +
                    $"settings or pick a looping variant.",
                    MessageType.Warning);
            }
        }
    }
}
