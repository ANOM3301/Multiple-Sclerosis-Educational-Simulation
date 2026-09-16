using Convai.Domain.Embodiment.Readings;
using Convai.Editor.Inspectors;
using Convai.Runtime.Embodiment;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Embodiment.Debugging
{
    /// <summary>
    ///     Play-mode editor window showing live embodiment state for a selected character:
    ///     dialogue phase, emotion scores, attention target, and gaze intent.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Zero overhead when closed. While open and in play mode it repaints at
    ///         ~10 Hz via <see cref="OnInspectorUpdate" />. Assign a character root in the
    ///         field or rely on auto-selection from the scene hierarchy.
    ///     </para>
    ///     <para>
    ///         Menu entry is compiled only when <c>CONVAI_INTERNAL_EMBODIMENT_LIVE_INSPECTOR</c>
    ///         is defined (developer builds). Call <see cref="Open" /> from tooling in this
    ///         assembly when needed.
    ///     </para>
    /// </remarks>
    public sealed class EmbodimentLiveInspectorWindow : EditorWindow
    {
        private const float RepaintInterval = 0.1f;
        private const int TopEmotionCount = 5;

#if CONVAI_INTERNAL_EMBODIMENT_LIVE_INSPECTOR
        [MenuItem("Convai/Developer/Embodiment Live Inspector", priority = 211)]
#endif
        internal static void Open()
        {
            EmbodimentLiveInspectorWindow window =
                GetWindow<EmbodimentLiveInspectorWindow>(false, "Embodiment Live Inspector", true);
            window.minSize = new Vector2(320f, 420f);
        }

        private GameObject _characterRoot;
        private bool _autoSelect = true;
        private Vector2 _scroll;
        private float _lastRepaint;
        private bool _showDialogue = true;
        private bool _showEmotion = true;
        private bool _showAttention = true;
        private bool _showGaze = true;

        private void OnGUI()
        {
            ConvaiBrandedInspectorChrome.DrawHeader("Embodiment Live Inspector", "Runtime Character State");

            DrawControls();

            if (!UnityEngine.Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect live character state.", MessageType.Info);
                return;
            }

            EmbodimentContext context = ResolveContext();
            if (context == null)
            {
                EditorGUILayout.HelpBox(
                    "No EmbodimentContext found. Assign a character root above or select one in the scene.",
                    MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawDialogueSection(context);
            DrawEmotionSection(context);
            DrawAttentionSection(context);
            DrawGazeSection(context);
            EditorGUILayout.EndScrollView();
        }

        private void DrawControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _autoSelect = EditorGUILayout.ToggleLeft(
                    new GUIContent("Auto-Select", "Track the currently selected hierarchy object's character."),
                    _autoSelect, GUILayout.Width(100f));

                using (new EditorGUI.DisabledScope(_autoSelect))
                {
                    _characterRoot = (GameObject)EditorGUILayout.ObjectField(
                        _characterRoot, typeof(GameObject), true);
                }
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawDialogueSection(EmbodimentContext context)
        {
            _showDialogue = EditorGUILayout.Foldout(_showDialogue, "Dialogue Phase", true, EditorStyles.foldoutHeader);
            if (!_showDialogue) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var flow = context.ConversationFlowSource;
                if (flow == null)
                {
                    EditorGUILayout.LabelField("No ConversationFlowSource registered.");
                    return;
                }

                DialogueStateReading reading = flow.Current;
                EditorGUILayout.LabelField("Primary State", reading.Primary.ToString());
                EditorGUILayout.LabelField("Blend To", reading.BlendTo.ToString());
                float blend = reading.BlendWeight;
                Rect blendRect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(blendRect, blend, $"Blend {blend:F2}");

                EditorGUILayout.LabelField("Time In State", $"{reading.TimeInState:F1}s");
                EditorGUILayout.LabelField("Energy Level", $"{reading.EnergyLevel:F2}");

                var dialoguePhase = context.DialoguePhase;
                if (dialoguePhase != null)
                {
                    EditorGUILayout.LabelField("Speech Active", dialoguePhase.IsSpeechActive.ToString());
                    EditorGUILayout.LabelField("Speech Blend", $"{dialoguePhase.SpeechBlendFactor:F2}");
                }
            }
        }

        private void DrawEmotionSection(EmbodimentContext context)
        {
            _showEmotion = EditorGUILayout.Foldout(_showEmotion, "Emotion Scores", true, EditorStyles.foldoutHeader);
            if (!_showEmotion) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var emotion = context.EmotionStateSource;
                if (emotion == null)
                {
                    EditorGUILayout.LabelField("No EmotionStateSource registered.");
                    return;
                }

                EmotionReading reading = emotion.Current;
                var scores = reading.AllScores;
                if (scores == null || scores.Count == 0)
                {
                    EditorGUILayout.LabelField("No scores.");
                    return;
                }

                float[] sortedValues = new float[scores.Count];
                string[] sortedLabels = new string[scores.Count];
                int idx = 0;
                foreach (var kv in scores)
                {
                    sortedLabels[idx] = kv.Key;
                    sortedValues[idx] = kv.Value;
                    idx++;
                }

                for (int i = 0; i < sortedValues.Length - 1; i++)
                {
                    for (int j = i + 1; j < sortedValues.Length; j++)
                    {
                        if (sortedValues[j] > sortedValues[i])
                        {
                            (sortedValues[i], sortedValues[j]) = (sortedValues[j], sortedValues[i]);
                            (sortedLabels[i], sortedLabels[j]) = (sortedLabels[j], sortedLabels[i]);
                        }
                    }
                }

                int count = Mathf.Min(TopEmotionCount, sortedValues.Length);
                for (int i = 0; i < count; i++)
                {
                    Rect barRect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(barRect, sortedValues[i], $"{sortedLabels[i]}  {sortedValues[i]:F2}");
                }
            }
        }

        private void DrawAttentionSection(EmbodimentContext context)
        {
            _showAttention = EditorGUILayout.Foldout(_showAttention, "Attention", true, EditorStyles.foldoutHeader);
            if (!_showAttention) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var attention = context.AttentionSource;
                if (attention == null)
                {
                    EditorGUILayout.LabelField("No AttentionSource registered.");
                    return;
                }

                AttentionReading reading = attention.Current;
                EditorGUILayout.LabelField("Valid", reading.IsValid.ToString());
                if (reading.IsValid)
                {
                    string targetName = reading.Target != null ? reading.Target.name : "<null>";
                    EditorGUILayout.LabelField("Target", targetName);
                    EditorGUILayout.LabelField("Smoothed Point", reading.SmoothedPoint.ToString("F2"));

                    Rect commitRect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(commitRect, reading.Commitment, $"Commitment {reading.Commitment:F2}");
                }
            }
        }

        private void DrawGazeSection(EmbodimentContext context)
        {
            _showGaze = EditorGUILayout.Foldout(_showGaze, "Gaze Intent", true, EditorStyles.foldoutHeader);
            if (!_showGaze) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var gaze = context.GazeIntentProvider;
                if (gaze == null)
                {
                    EditorGUILayout.LabelField("No GazeIntentProvider registered.");
                    return;
                }

                GazeIntent intent = gaze.Current;
                EditorGUILayout.LabelField("Target Point", intent.WorldTargetPoint.ToString("F2"));

                Rect weightRect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(weightRect, intent.OverallWeight, $"Overall Weight {intent.OverallWeight:F2}");

                Rect eyeRect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(eyeRect, intent.EyeShare, $"Eye Share {intent.EyeShare:F2}");
            }
        }

        private EmbodimentContext ResolveContext()
        {
            GameObject root = _autoSelect ? Selection.activeGameObject : _characterRoot;
            if (root == null) return null;

            EmbodimentContext ctx = root.GetComponentInParent<EmbodimentContext>(true);
            if (ctx != null) return ctx;

            ctx = root.GetComponentInChildren<EmbodimentContext>(true);
            return ctx;
        }

        private void OnInspectorUpdate()
        {
            if (!UnityEngine.Application.isPlaying) return;

            float now = (float)EditorApplication.timeSinceStartup;
            if (now - _lastRepaint < RepaintInterval) return;

            _lastRepaint = now;
            Repaint();
        }

        private void OnSelectionChange()
        {
            if (_autoSelect) Repaint();
        }
    }
}
