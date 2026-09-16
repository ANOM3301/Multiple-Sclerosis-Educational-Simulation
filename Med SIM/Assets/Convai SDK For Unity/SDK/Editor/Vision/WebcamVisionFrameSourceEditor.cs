#if UNITY_EDITOR
using Convai.Modules.Vision.Editor.UI;
using Convai.Runtime.Vision.Sources;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Vision
{
    /// <summary>
    ///     Custom inspector for WebcamVisionFrameSource.
    ///     Captures frames from a physical webcam and streams them via the vision pipeline.
    ///     Sections: Webcam Device · Capture Settings · Live Status.
    /// </summary>
    [CustomEditor(typeof(WebcamVisionFrameSource))]
    public class WebcamVisionFrameSourceEditor : ConvaiVisionBaseEditor
    {
        // Section IDs & icons
        private const string SectionDeviceId = "WebcamDevice";
        private const string SectionCaptureId = "CaptureSettings";
        private const string SectionStatusId = "LiveStatus";

        private const string IconDevice = "\u25CE"; // ◎ – bullseye, evokes a camera lens
        private const string IconCapture = "\u2699"; // ⚙
        private const string IconStatusIdle = "\u25CF";
        private const string IconStatusLive = "\u25C9";

        private SerializedProperty _deviceNameProp;
        private SerializedProperty _fpsProp;
        private SerializedProperty _heightProp;
        private bool _showCapture = true;

        private bool _showDevice = true;
        private bool _showStatus = true;

        // State
        private WebcamVisionFrameSource _source;
        private SerializedProperty _sourceIdProp;
        private SerializedProperty _widthProp;

        protected override string EditorStateHostId => "WebcamSourceEditor";

        // Lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            _source = (WebcamVisionFrameSource)target;
            _deviceNameProp = serializedObject.FindProperty("_webcamDeviceName");
            _widthProp = serializedObject.FindProperty("_requestedWidth");
            _heightProp = serializedObject.FindProperty("_requestedHeight");
            _fpsProp = serializedObject.FindProperty("_requestedFps");
            _sourceIdProp = serializedObject.FindProperty("_sourceId");

            _showDevice = ConvaiVisionSectionStateStore.Get(EditorStateHostId, SectionDeviceId, true);
            _showCapture = ConvaiVisionSectionStateStore.Get(EditorStateHostId, SectionCaptureId, true);
            _showStatus = ConvaiVisionSectionStateStore.Get(EditorStateHostId, SectionStatusId, true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ConvaiVisionSectionStateStore.Set(EditorStateHostId, SectionDeviceId, _showDevice);
            ConvaiVisionSectionStateStore.Set(EditorStateHostId, SectionCaptureId, _showCapture);
            ConvaiVisionSectionStateStore.Set(EditorStateHostId, SectionStatusId, _showStatus);
        }


        public override void OnInspectorGUI()
        {
            EnsureStyles();
            serializedObject.Update();

            (Color dot, string text) = GetHeaderStatus();
            DrawHeader("Webcam Vision Frame Source", dot, text);
            EditorGUILayout.Space(6f);

            DrawValidation();
            DrawDeviceSection();
            DrawCaptureSection();

            if (UnityEngine.Application.isPlaying)
                DrawLiveStatus();
            else
                DrawStatusPlaceholder();

            serializedObject.ApplyModifiedProperties();

            if (UnityEngine.Application.isPlaying && _source.IsCapturing)
                Repaint();
        }


        private (Color dot, string text) GetHeaderStatus()
        {
            if (!UnityEngine.Application.isPlaying) return (StatusEditor, "Editor");
            return _source.IsCapturing ? (ConvaiGreen, "Capturing") : (StatusIdle, "Idle");
        }


        private void DrawValidation()
        {
#if UNITY_WEBGL
            // AsyncGPUReadback is unavailable on WebGL — the rest of the inspector is not useful.
            EditorGUILayout.HelpBox(
                "WebGL is not supported. WebcamVisionFrameSource requires AsyncGPUReadback. " +
                "Use CameraVisionFrameSource to stream the game canvas instead.",
                MessageType.Error);
#else
#if UNITY_ANDROID
            EditorGUILayout.HelpBox(
                "Android: the CAMERA permission will be requested at runtime. " +
                "Ensure it is declared in your AndroidManifest.xml.",
                MessageType.Info);
#elif UNITY_IOS
            EditorGUILayout.HelpBox(
                "iOS: NSCameraUsageDescription must be set in Player Settings \u2192 Other Settings.",
                MessageType.Info);
#endif

            if (UnityEngine.Application.isPlaying && WebCamTexture.devices.Length == 0)
                DrawWarningBox("No Webcam Detected",
                    "No webcam devices found. Check that a camera is connected and that permissions are granted.");
#endif
        }


        private void DrawDeviceSection()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            string title = devices.Length > 0 ? $"WEBCAM DEVICE  \u2014  {devices.Length} found" : "WEBCAM DEVICE";

            _showDevice = DrawSection(SectionDeviceId, title, _showDevice, IconDevice);
            if (!_showDevice) return;

            DrawSectionBody(() =>
            {
                // TextField: avoids any [Header] decorator the runtime field may carry.
                _deviceNameProp.stringValue = EditorGUILayout.TextField(
                    new GUIContent("Device Name",
                        "The name of the webcam to open, exactly as the OS reports it. " +
                        "Leave empty to use the system default camera."),
                    _deviceNameProp.stringValue);

                if (string.IsNullOrEmpty(_deviceNameProp.stringValue))
                {
                    GUI.color = new Color(0.6f, 0.6f, 0.63f);
                    EditorGUILayout.LabelField("Empty = system default camera", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }

                if (devices.Length > 0)
                {
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField("Available Devices", EditorStyles.boldLabel);

                    for (int i = 0; i < devices.Length; i++)
                    {
                        WebCamDevice d = devices[i];
                        bool isSelected = _deviceNameProp.stringValue == d.name;
                        string facing = d.isFrontFacing ? "Front" : "Back";

                        EditorGUILayout.BeginHorizontal();
                        GUI.color = isSelected ? ConvaiGreenLight : Color.white;
                        GUILayout.Label(
                            isSelected ? $"\u2713 {d.name}  ({facing})" : $"  {d.name}  ({facing})",
                            EditorStyles.miniLabel);
                        GUI.color = Color.white;
                        GUILayout.FlexibleSpace();

                        if (isSelected)
                        {
                            if (GUILayout.Button("Clear", MiniButtonStyle, GUILayout.Width(55f)))
                            {
                                _deviceNameProp.stringValue = string.Empty;
                                serializedObject.ApplyModifiedProperties();
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("Select", MiniButtonStyle, GUILayout.Width(55f)))
                            {
                                _deviceNameProp.stringValue = d.name;
                                serializedObject.ApplyModifiedProperties();
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.Space(4f);
                    GUI.color = new Color(0.6f, 0.6f, 0.63f);
                    EditorGUILayout.LabelField(
                        "No devices detected. Connect a webcam or check device permissions.",
                        EditorStyles.wordWrappedMiniLabel);
                    GUI.color = Color.white;
                }
            });
        }


        private void DrawCaptureSection()
        {
            _showCapture = DrawSection(SectionCaptureId, "CAPTURE SETTINGS", _showCapture, IconCapture);
            if (!_showCapture) return;

            DrawSectionBody(() =>
            {
                // Resolution on one row — more compact than two separate fields.
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("Requested Resolution",
                        "Hint to the device driver. " +
                        "The webcam may return a different resolution based on what it supports."),
                    GUILayout.Width(EditorGUIUtility.labelWidth));
                _widthProp.intValue = EditorGUILayout.IntField(
                    GUIContent.none, _widthProp.intValue, GUILayout.Width(58f));
                GUI.color = new Color(0.65f, 0.65f, 0.65f);
                GUILayout.Label("\u00D7", GUILayout.Width(14f));
                GUI.color = Color.white;
                _heightProp.intValue = EditorGUILayout.IntField(
                    GUIContent.none, _heightProp.intValue, GUILayout.Width(58f));
                EditorGUILayout.EndHorizontal();

                _fpsProp.intValue = EditorGUILayout.IntField(
                    new GUIContent("Requested FPS",
                        "Hint to the device driver. Actual frame rate may differ based on hardware."),
                    _fpsProp.intValue);

                EditorGUILayout.Space(4f);

                // TextField: avoids the [Header("Debug")] decorator that PropertyField would render.
                // Renamed from "Source ID" to "Identifier" — clearer for external developers.
                _sourceIdProp.stringValue = EditorGUILayout.TextField(
                    new GUIContent("Identifier",
                        "Optional tag to distinguish this source when multiple WebcamVisionFrameSource " +
                        "components exist in the same scene. Leave empty if only one webcam source is used."),
                    _sourceIdProp.stringValue);

                // Show actual device-negotiated dimensions in play mode.
                // Only visible at runtime once the webcam has initialised.
                if (UnityEngine.Application.isPlaying && _source.CurrentRenderTexture != null)
                {
                    EditorGUILayout.Space(4f);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.LabelField(
                            new GUIContent("Actual Resolution",
                                "The resolution the device opened at. May differ from what was requested."),
                            new GUIContent(
                                $"{_source.FrameDimensions.Width}\u00D7{_source.FrameDimensions.Height}" +
                                $"  @  {_source.TargetFrameRate:F0} fps"));
                    }
                }
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

            Color bg = _source.IsCapturing ? new Color(0.32f, 0.72f, 0.53f, 0.08f) : SectionBg;
            DrawSectionBody(() =>
            {
                var statusProvider = _source as IVisionFrameSourceStatusProvider;
                string label = _source.IsCapturing ? "\u25B6  CAPTURING" : "\u25CF  IDLE";
                DrawStatusRow(label, _source.IsCapturing ? ConvaiGreen : StatusIdle);

                EditorGUILayout.Space(6f);

                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("Resolution",
                    $"{_source.FrameDimensions.Width}\u00D7{_source.FrameDimensions.Height}",
                    DefaultValueColor);
                DrawLiveCell("Target FPS", $"{_source.TargetFrameRate:F0}", DefaultValueColor);
                DrawLiveCell("Frames", $"{_source.FrameCount}", DefaultValueColor);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4f);

                EditorGUILayout.BeginHorizontal();
                DrawLiveCell("Frame Ready", _source.IsFrameReady ? "Yes" : "No",
                    _source.IsFrameReady ? ConvaiGreenLight : DefaultValueColor, _source.IsFrameReady);
                string id = string.IsNullOrEmpty(_source.SourceId) ? "(none)" : _source.SourceId;
                DrawLiveCell("Identifier", id, DefaultValueColor);
                EditorGUILayout.EndHorizontal();

                if (statusProvider != null)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.BeginHorizontal();
                    DrawLiveCell("State", statusProvider.State.ToString(), DefaultValueColor);
                    DrawLiveCell("Error", statusProvider.ErrorKind.ToString(), DefaultValueColor);
                    EditorGUILayout.EndHorizontal();
                }

                if (!string.IsNullOrWhiteSpace(statusProvider?.StatusMessage))
                {
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.HelpBox(statusProvider.StatusMessage, MessageType.Info);
                }
                else if (!string.IsNullOrEmpty(_source.LastErrorCode))
                {
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.HelpBox(
                        $"{_source.LastErrorMessage}  [{_source.LastErrorCode}]",
                        MessageType.Error);
                }
            }, bg);
        }
    }
}
#endif
