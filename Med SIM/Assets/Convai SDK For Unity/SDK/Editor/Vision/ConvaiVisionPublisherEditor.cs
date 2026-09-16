#if UNITY_EDITOR
using Convai.Modules.Vision;
using Convai.Modules.Vision.Editor.UI;
using Convai.Runtime.Vision.Publishing;
using Convai.Runtime.Vision.Sources;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Vision
{
    /// <summary>
    ///     Custom inspector for ConvaiVisionPublisher.
    ///     Sections: Frame Source · Publish Policy · Live Status.
    /// </summary>
    [CustomEditor(typeof(ConvaiVisionPublisher))]
    public class ConvaiVisionPublisherEditor : ConvaiVisionBaseEditor
    {
        // Section IDs & icons
        private const string SectionSourceId = "FrameSource";
        private const string SectionPolicyId = "PublishPolicy";
        private const string SectionStatusId = "LiveStatus";

        private const string IconSource = "\u2699"; // ⚙  – frame source / config
        private const string IconPolicy = "\u25B6"; // ▶  – publish / transport
        private const string IconStatusIdle = "\u25CF"; // ●
        private const string IconStatusLive = "\u25C9"; // ◉
        private SerializedProperty _bitrateProp;
        private SerializedProperty _fpsProp;

        private SerializedProperty _frameSourceProp;

        // State
        private ConvaiVisionPublisher _publisher;
        private SerializedProperty _publishPolicyProp;
        private bool _showPolicy = true;

        private bool _showSource = true;
        private bool _showStatus = true;
        private SerializedProperty _videoTrackNameProp;

        protected override string EditorStateHostId => "PublisherEditor";

        // Lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            _publisher = (ConvaiVisionPublisher)target;
            _frameSourceProp = serializedObject.FindProperty("_frameSourceComponent");
            _publishPolicyProp = serializedObject.FindProperty("_publishPolicy");
            _videoTrackNameProp = serializedObject.FindProperty("videoTrackName");
            _fpsProp = serializedObject.FindProperty("publishFrameRateOverride");
            _bitrateProp = serializedObject.FindProperty("publishBitrateOverride");

            _showSource = ConvaiVisionSectionStateStore.Get(EditorStateHostId, SectionSourceId, true);
            _showPolicy = ConvaiVisionSectionStateStore.Get(EditorStateHostId, SectionPolicyId, true);
            _showStatus = ConvaiVisionSectionStateStore.Get(EditorStateHostId, SectionStatusId, true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ConvaiVisionSectionStateStore.Set(EditorStateHostId, SectionSourceId, _showSource);
            ConvaiVisionSectionStateStore.Set(EditorStateHostId, SectionPolicyId, _showPolicy);
            ConvaiVisionSectionStateStore.Set(EditorStateHostId, SectionStatusId, _showStatus);
        }


        public override void OnInspectorGUI()
        {
            EnsureStyles();
            serializedObject.Update();

            (Color dot, string text) = GetHeaderStatus();
            DrawHeader("Convai Vision Publisher", dot, text);
            EditorGUILayout.Space(6f);

            DrawValidation();
            DrawFrameSourceSection();
            DrawPublishPolicySection();

            if (UnityEngine.Application.isPlaying)
                DrawLiveStatus();
            else
                DrawStatusPlaceholder();

            serializedObject.ApplyModifiedProperties();

            if (UnityEngine.Application.isPlaying &&
                (_publisher.IsPublishing || (_publisher.FrameSource?.IsCapturing ?? false)))
                Repaint();
        }


        private (Color dot, string text) GetHeaderStatus()
        {
            if (!UnityEngine.Application.isPlaying) return (StatusEditor, "Editor");
            return _publisher.IsPublishing ? (ConvaiGreen, "Publishing") : (StatusIdle, "Idle");
        }


        private void DrawValidation()
        {
            if (UnityEngine.Application.platform == RuntimePlatform.WebGLPlayer)
            {
                EditorGUILayout.HelpBox(
                    "WebGL: the Unity canvas is streamed automatically. The Frame Source field is ignored.",
                    MessageType.Info);
                return;
            }

            // _publisher.FrameSource is a runtime backing field — null until Play.
            // Read the serialized property instead so the check works in editor mode.
            if (_frameSourceProp.objectReferenceValue == null)
            {
                DrawWarningBox(
                    "Frame Source Required",
                    "Assign a CameraVisionFrameSource or WebcamVisionFrameSource component so the publisher knows what to stream.",
                    "Auto-Find", AutoFindSource, 90f);
            }

            List<MonoBehaviour> localSources = FindLocalFrameSources();
            if (localSources.Count > 1)
            {
                string assignedLabel = _frameSourceProp.objectReferenceValue is MonoBehaviour assignedBehaviour
                    ? DescribeFrameSource(assignedBehaviour)
                    : "none";
                EditorGUILayout.HelpBox(
                    $"Multiple local frame sources detected ({localSources.Count}). Assigned source: {assignedLabel}. Keep this explicit to avoid ambiguity.",
                    MessageType.Info);
            }
        }


        private void DrawFrameSourceSection()
        {
            _showSource = DrawSection(SectionSourceId, "FRAME SOURCE", _showSource, IconSource);
            if (!_showSource) return;

            DrawSectionBody(() =>
            {
                // ObjectField: avoids the [Header("Frame Source")] decorator that
                // PropertyField would render, duplicating the section title.
                _frameSourceProp.objectReferenceValue = EditorGUILayout.ObjectField(
                    new GUIContent("Source",
                        "The component that provides video frames. " +
                        "Attach either a CameraVisionFrameSource (game camera) or a " +
                        "WebcamVisionFrameSource (physical webcam) to this field."),
                    _frameSourceProp.objectReferenceValue,
                    typeof(MonoBehaviour), true);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Auto-Find in Hierarchy", MiniButtonStyle, GUILayout.Width(155f)))
                    AutoFindSource();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4f);

                // TextField: avoids the [Header("Video Settings")] decorator.
                _videoTrackNameProp.stringValue = EditorGUILayout.TextField(
                    new GUIContent("Track Name",
                        "Name of the published video track as it appears in the LiveKit room. " +
                        "Only needs to change if you publish multiple tracks simultaneously."),
                    _videoTrackNameProp.stringValue);

                Object assigned = _frameSourceProp.objectReferenceValue;
                if (assigned != null)
                {
                    EditorGUILayout.Space(2f);
                    if (assigned is MonoBehaviour assignedBehaviour)
                    {
                        EditorGUILayout.HelpBox($"\u2713  Connected: {DescribeFrameSource(assignedBehaviour)}",
                            MessageType.None);
                    }
                }
            });
        }


        private void DrawPublishPolicySection()
        {
            _showPolicy = DrawSection(SectionPolicyId, "PUBLISH POLICY", _showPolicy, IconPolicy);
            if (!_showPolicy) return;

            DrawSectionBody(() =>
            {
                // EnumPopup: avoids the [Header("Publish Policy")] decorator.
                _publishPolicyProp.enumValueIndex = (int)(VisionPublishPolicy)EditorGUILayout.EnumPopup(
                    new GUIContent("Mode",
                        "Controls when and how video is transported to the LiveKit room."),
                    (VisionPublishPolicy)_publishPolicyProp.enumValueIndex);

                var policy = (VisionPublishPolicy)_publishPolicyProp.enumValueIndex;
                EditorGUILayout.Space(2f);
                EditorGUILayout.HelpBox(GetPolicyDescription(policy), MessageType.Info);

                EditorGUILayout.Space(6f);

                // Transport caps
                // These are OPTIONAL upper limits applied on top of whichever policy is active.
                // They do not replace the policy – the policy still decides the base behaviour.
                // Setting a cap to 0 means "no cap; let the policy decide."
                // They are shown for every policy including Manual, because caps still apply
                // when EnablePublishing(true) is called from code in Manual mode.
                EditorGUILayout.LabelField("Transport Caps  (0 = no cap, policy decides)",
                    EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(2f);

                _fpsProp.intValue = EditorGUILayout.IntSlider(
                    new GUIContent("Max Publish FPS",
                        "Hard upper limit on the published frame rate. " +
                        "The policy may already set a lower target — this only adds a ceiling. " +
                        "Set to 0 to leave it uncapped."),
                    _fpsProp.intValue, 0, 30);

                _bitrateProp.intValue = EditorGUILayout.IntField(
                    new GUIContent("Max Bitrate (bps)",
                        "Hard upper limit on the published bitrate in bits per second. " +
                        "The policy may already set a lower target — this only adds a ceiling. " +
                        "Set to 0 to leave it uncapped."),
                    _bitrateProp.intValue);
            });
        }

        // Live Status section

        private void DrawStatusPlaceholder()
        {
            _showStatus = DrawSection(SectionStatusId, "LIVE STATUS", _showStatus, IconStatusIdle);
            if (!_showStatus) return;
            DrawSectionBody(DrawOfflinePlaceholder);
        }

        private void DrawLiveStatus()
        {
            _showStatus = DrawSection(SectionStatusId, "LIVE STATUS", _showStatus, IconStatusLive);
            if (!_showStatus) return;

            Color bg = _publisher.IsPublishing ? new Color(0.32f, 0.72f, 0.53f, 0.08f) : SectionBg;
            DrawSectionBody(() =>
            {
                string statusLabel = _publisher.IsPublishing ? "\u25B6  PUBLISHING" : "\u25CF  IDLE";
                Color statusColor = _publisher.IsPublishing ? ConvaiGreen : StatusIdle;
                string badge = _publisher.PublishPolicy.ToString();
                DrawStatusRow(statusLabel, statusColor, badge);

                EditorGUILayout.Space(6f);

                if (_publisher.FrameSource is { } src)
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawLiveCell("Source",
                        src is MonoBehaviour behaviour ? behaviour.name : src.GetType().Name,
                        DefaultValueColor);
                    DrawLiveCell("Source Id",
                        string.IsNullOrWhiteSpace(src.SourceId) ? "(none)" : src.SourceId,
                        DefaultValueColor);
                    DrawLiveCell("Capturing", src.IsCapturing ? "Yes" : "No",
                        src.IsCapturing ? ConvaiGreenLight : DefaultValueColor, src.IsCapturing);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(4f);

                    EditorGUILayout.BeginHorizontal();
                    DrawLiveCell("Frame Ready", src.IsFrameReady ? "Yes" : "No",
                        src.IsFrameReady ? ConvaiGreenLight : DefaultValueColor, src.IsFrameReady);
                    DrawLiveCell("Resolution",
                        $"{src.FrameDimensions.Width}\u00D7{src.FrameDimensions.Height}", DefaultValueColor);
                    DrawLiveCell("Target FPS", $"{src.TargetFrameRate:F0}", DefaultValueColor);
                    DrawLiveCell("Frames", $"{src.FrameCount}", DefaultValueColor);
                    EditorGUILayout.EndHorizontal();

                    if (src is MonoBehaviour sourceBehaviour)
                    {
                        EditorGUILayout.Space(6f);
                        EditorGUILayout.HelpBox($"Live Source: {DescribeFrameSource(sourceBehaviour)}",
                            MessageType.None);
                    }
                }
                else
                {
                    GUI.color = new Color(0.6f, 0.6f, 0.6f);
                    EditorGUILayout.LabelField("No frame source connected.", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }
            }, bg);
        }

        // Helpers

        private void AutoFindSource()
        {
            MonoBehaviour found = _publisher.GetComponentInChildren<IVisionFrameSource>(true) as MonoBehaviour
                                  ?? _publisher.GetComponentInParent<IVisionFrameSource>() as MonoBehaviour;
            if (found != null)
            {
                _frameSourceProp.objectReferenceValue = found;
                serializedObject.ApplyModifiedProperties();
            }
        }

        private List<MonoBehaviour> FindLocalFrameSources()
        {
            var sources = new List<MonoBehaviour>();
            MonoBehaviour[] behaviours = _publisher.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IVisionFrameSource)
                    sources.Add(behaviours[i]);
            }

            return sources;
        }

        private static string DescribeFrameSource(MonoBehaviour behaviour)
        {
            if (behaviour is not IVisionFrameSource source)
                return behaviour.name;

            string sourceId = string.IsNullOrWhiteSpace(source.SourceId) ? "unlabeled" : source.SourceId;
            return $"{GetHierarchyPath(behaviour.transform)} [{sourceId}] ({behaviour.GetType().Name})";
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "(unknown)";

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }

        private static string GetPolicyDescription(VisionPublishPolicy policy) => policy switch
        {
            VisionPublishPolicy.AutoCompatible =>
                "Publishes continuously using a balanced transport budget. " +
                "Automatically stays compatible with the backend. Best default for most projects.",
            VisionPublishPolicy.HighResponsiveness =>
                "Publishes continuously with higher visual fidelity and lower latency. " +
                "Uses more bandwidth and GPU — suitable for high-quality demo scenarios.",
            VisionPublishPolicy.LowOverhead =>
                "Publishes continuously with reduced CPU, GPU, and network usage. " +
                "Best for performance-constrained devices or background vision tasks.",
            VisionPublishPolicy.Manual =>
                "Does NOT publish automatically. " +
                "Call EnablePublishing(true) from code when you want streaming to start. " +
                "Useful when you need full control over when the video track is active.",
            _ => "Unknown policy."
        };
    }
}
#endif
