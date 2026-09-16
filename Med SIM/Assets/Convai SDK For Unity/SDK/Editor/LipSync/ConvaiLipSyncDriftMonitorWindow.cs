using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Convai.Modules.LipSync;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.LipSync
{
    /// <summary>
    ///     Live visualizer for audio-vs-lipsync alignment. Plots the drift error (measured audio
    ///     playhead minus visual clock) over time with lifecycle event markers, shows the cumulative
    ///     correction the drift loop is applying (its slope exposes any real underlying drift source),
    ///     and provides a live A/V offset slider for calibrating perceived sync by eye.
    /// </summary>
    internal sealed class ConvaiLipSyncDriftMonitorWindow : EditorWindow
    {
        private const float ChartHeight = 220f;
        private const float DeadbandMs = 20f;

        private static readonly Color BgColor = new(0.12f, 0.12f, 0.12f);
        private static readonly Color GridColor = new(1f, 1f, 1f, 0.08f);
        private static readonly Color DeadbandColor = new(0.2f, 0.8f, 0.3f, 0.10f);
        private static readonly Color ErrorLineColor = new(0.35f, 0.75f, 1f);
        private static readonly Color CorrectionLineColor = new(1f, 0.75f, 0.25f, 0.9f);
        private static readonly Color EventColor = new(1f, 0.4f, 0.4f, 0.65f);
        private static readonly Color ZeroLineColor = new(1f, 1f, 1f, 0.35f);

        private readonly List<string> _characterIds = new();
        private readonly List<LipSyncDriftSample> _samples = new();
        private readonly List<LipSyncDriftEvent> _events = new();

        private string _selectedCharacterId;
        private float _windowSeconds = 10f;
        private bool _useOffsetOverride;
        private float _offsetOverrideMs = -30f;
        private bool _logSummaryToConsole;
        private double _nextSummaryTime;
        private double _nextRepaintTime;
        private Vector2 _eventScroll;

        [MenuItem("Convai/LipSync Drift Monitor")]
        public static void Open()
        {
            var window = GetWindow<ConvaiLipSyncDriftMonitorWindow>("LipSync Drift");
            window.minSize = new Vector2(520f, 420f);
        }

        private void OnEnable() => EditorApplication.update += OnEditorUpdate;

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            // Leave playback untouched when the window closes: drop the live override and stop sampling.
            LipSyncDriftMonitor.TimeOffsetOverrideSeconds = null;
            LipSyncDriftMonitor.Enabled = false;
        }

        private void OnEditorUpdate()
        {
            if (!LipSyncDriftMonitor.Enabled || !EditorApplication.isPlaying) return;

            if (EditorApplication.timeSinceStartup >= _nextRepaintTime)
            {
                _nextRepaintTime = EditorApplication.timeSinceStartup + 0.05d;
                Repaint();
            }

            if (_logSummaryToConsole && EditorApplication.timeSinceStartup >= _nextSummaryTime)
            {
                _nextSummaryTime = EditorApplication.timeSinceStartup + 1d;
                LogSummary();
            }
        }

        private void OnGUI()
        {
            // Refresh data caches only during Layout so the control structure (and data the
            // controls are laid out for) is identical between the Layout and Repaint passes;
            // refreshing mid-frame causes "Invalid GUILayout state" errors.
            if (Event.current.type == EventType.Layout)
            {
                RefreshSelection();
                if (!string.IsNullOrEmpty(_selectedCharacterId))
                {
                    LipSyncDriftMonitor.CopySamples(_selectedCharacterId, _samples);
                    LipSyncDriftMonitor.CopyEvents(_selectedCharacterId, _events);
                }
                else
                {
                    _samples.Clear();
                    _events.Clear();
                }
            }

            DrawToolbar();

            if (string.IsNullOrEmpty(_selectedCharacterId))
            {
                EditorGUILayout.HelpBox(
                    LipSyncDriftMonitor.Enabled
                        ? "Waiting for lip sync activity... Enter Play mode and talk to a character."
                        : "Enable monitoring, enter Play mode, and talk to a character.",
                    MessageType.Info);
                return;
            }

            DrawStats();
            DrawOffsetControls();

            Rect chartRect = GUILayoutUtility.GetRect(position.width - 12f, ChartHeight);
            DrawChart(chartRect);

            DrawEventLog();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            bool enabled = GUILayout.Toggle(LipSyncDriftMonitor.Enabled, "Monitor", EditorStyles.toolbarButton,
                GUILayout.Width(70f));
            if (enabled != LipSyncDriftMonitor.Enabled)
            {
                LipSyncDriftMonitor.Enabled = enabled;
                if (!enabled) LipSyncDriftMonitor.TimeOffsetOverrideSeconds = null;
            }

            // Always draw the popup (placeholder when empty) so the toolbar control count is
            // stable regardless of when characters appear.
            string[] options = _characterIds.Count > 0 ? _characterIds.ToArray() : new[] { "(no characters)" };
            int index = Mathf.Max(0, _characterIds.IndexOf(_selectedCharacterId));
            int newIndex = EditorGUILayout.Popup(index, options, EditorStyles.toolbarPopup, GUILayout.Width(180f));
            if (newIndex >= 0 && newIndex < _characterIds.Count) _selectedCharacterId = _characterIds[newIndex];

            GUILayout.Space(8f);
            GUILayout.Label("Window", GUILayout.Width(50f));
            _windowSeconds = GUILayout.HorizontalSlider(_windowSeconds, 3f, 30f, GUILayout.Width(90f));
            GUILayout.Label($"{_windowSeconds:F0}s", GUILayout.Width(28f));

            GUILayout.FlexibleSpace();

            _logSummaryToConsole = GUILayout.Toggle(_logSummaryToConsole, "Console summary",
                EditorStyles.toolbarButton, GUILayout.Width(110f));

            if (GUILayout.Button("Export CSV", EditorStyles.toolbarButton, GUILayout.Width(80f))) ExportCsv();

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                LipSyncDriftMonitor.Clear();

            EditorGUILayout.EndHorizontal();
        }

        private void RefreshSelection()
        {
            LipSyncDriftMonitor.GetCharacterIds(_characterIds);
            if (_characterIds.Count == 0)
            {
                _selectedCharacterId = null;
                return;
            }

            if (string.IsNullOrEmpty(_selectedCharacterId) || !_characterIds.Contains(_selectedCharacterId))
                _selectedCharacterId = _characterIds[0];
        }

        private void DrawStats()
        {
            LipSyncDriftSample latest = _samples.Count > 0 ? _samples[^1] : default;
            ComputeWindowStats(out float meanAbsMs, out float maxAbsMs, out float correctionRateMsPerMin);

            EditorGUILayout.BeginHorizontal();

            Color errorColor = Mathf.Abs(latest.ErrorMs) <= DeadbandMs
                ? new Color(0.4f, 0.9f, 0.45f)
                : Mathf.Abs(latest.ErrorMs) <= 50f
                    ? new Color(0.95f, 0.85f, 0.3f)
                    : new Color(1f, 0.4f, 0.4f);
            GUIStyle big = new(EditorStyles.boldLabel) { fontSize = 22, normal = { textColor = errorColor } };
            GUILayout.Label(latest.AudioActive ? $"{latest.ErrorMs:+0;-0;0} ms" : "— ms", big, GUILayout.Width(110f));

            EditorGUILayout.BeginVertical();
            GUILayout.Label(
                $"mean |err| {meanAbsMs:F1} ms   max |err| {maxAbsMs:F1} ms   correction {correctionRateMsPerMin:+0.0;-0.0;0.0} ms/min",
                EditorStyles.miniLabel);
            GUILayout.Label(
                $"state {latest.State}   audio {(latest.AudioActive ? "playing" : "idle")}   target {latest.AudioTargetSeconds:F2}s   clock {latest.VisualClockSeconds:F2}s   buffered {latest.BufferedSeconds:F2}s   headroom {latest.HeadroomSeconds:F2}s",
                EditorStyles.miniLabel);
            GUILayout.Label(
                "err > 0: mouth behind audio.  A steady correction slope = real drift source (clock skew); spikes = stalls/gaps.",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawOffsetControls()
        {
            EditorGUILayout.BeginHorizontal();

            bool useOverride = EditorGUILayout.ToggleLeft("Live A/V offset override", _useOffsetOverride,
                GUILayout.Width(170f));
            using (new EditorGUI.DisabledScope(!useOverride))
            {
                float newValue = EditorGUILayout.Slider(_offsetOverrideMs, -150f, 150f);
                GUILayout.Label("ms", GUILayout.Width(24f));
                if (useOverride && !Mathf.Approximately(newValue, _offsetOverrideMs)) _offsetOverrideMs = newValue;
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Label(
                "Negative = mouth leads audio (default -30 ms). Tune by eye, then report the best value so it can be baked in.",
                EditorStyles.miniLabel);

            if (useOverride != _useOffsetOverride) _useOffsetOverride = useOverride;
            LipSyncDriftMonitor.TimeOffsetOverrideSeconds =
                _useOffsetOverride ? _offsetOverrideMs / 1000f : null;
        }

        private void DrawChart(Rect rect)
        {
            EditorGUI.DrawRect(rect, BgColor);
            if (_samples.Count < 2) return;

            float now = _samples[^1].TimeSeconds;
            float t0 = now - _windowSeconds;

            // Y range: at least ±50 ms, expanded to fit the data.
            float maxAbs = 50f;
            for (int i = 0; i < _samples.Count; i++)
            {
                if (_samples[i].TimeSeconds < t0 || !_samples[i].AudioActive) continue;
                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(_samples[i].ErrorMs));
            }

            maxAbs *= 1.1f;

            float YFor(float ms) => rect.yMax - ((ms + maxAbs) / (2f * maxAbs) * rect.height);
            float XFor(float t) => rect.xMin + (Mathf.Clamp01((t - t0) / _windowSeconds) * rect.width);

            // Deadband + zero line + horizontal grid.
            var deadbandRect = new Rect(rect.xMin, YFor(DeadbandMs), rect.width, YFor(-DeadbandMs) - YFor(DeadbandMs));
            EditorGUI.DrawRect(deadbandRect, DeadbandColor);
            EditorGUI.DrawRect(new Rect(rect.xMin, YFor(0f), rect.width, 1f), ZeroLineColor);
            for (float ms = 50f; ms < maxAbs; ms += 50f)
            {
                EditorGUI.DrawRect(new Rect(rect.xMin, YFor(ms), rect.width, 1f), GridColor);
                EditorGUI.DrawRect(new Rect(rect.xMin, YFor(-ms), rect.width, 1f), GridColor);
            }

            GUI.Label(new Rect(rect.xMin + 4f, rect.yMin + 2f, 120f, 16f), $"+{maxAbs:F0} ms", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.xMin + 4f, rect.yMax - 18f, 120f, 16f), $"-{maxAbs:F0} ms", EditorStyles.miniLabel);

            // Event markers.
            foreach (LipSyncDriftEvent evt in _events)
            {
                if (evt.TimeSeconds < t0 || evt.TimeSeconds > now) continue;
                float x = XFor(evt.TimeSeconds);
                EditorGUI.DrawRect(new Rect(x, rect.yMin, 1f, rect.height), EventColor);
            }

            // Error polyline (gaps where audio is inactive) + cumulative-correction trace.
            DrawSeries(rect, t0, now, XFor, YFor, useCorrection: false, ErrorLineColor);
            DrawSeries(rect, t0, now, XFor, YFor, useCorrection: true, CorrectionLineColor);

            GUI.Label(new Rect(rect.xMax - 220f, rect.yMin + 2f, 216f, 16f),
                "blue: drift error   orange: cumulative correction", EditorStyles.miniLabel);
        }

        private void DrawSeries(Rect rect, float t0, float now, System.Func<float, float> xFor,
            System.Func<float, float> yFor, bool useCorrection, Color color)
        {
            var points = new List<Vector3>(_samples.Count);
            float correctionBase = 0f;
            bool baseSet = false;

            Handles.BeginGUI();
            Handles.color = color;
            for (int i = 0; i < _samples.Count; i++)
            {
                LipSyncDriftSample s = _samples[i];
                if (s.TimeSeconds < t0) continue;

                if (!s.AudioActive)
                {
                    FlushPolyline(points);
                    baseSet = false;
                    continue;
                }

                float value;
                if (useCorrection)
                {
                    // Plot correction relative to the window start so the slope stays readable.
                    if (!baseSet)
                    {
                        correctionBase = s.CumulativeCorrectionMs;
                        baseSet = true;
                    }

                    value = s.CumulativeCorrectionMs - correctionBase;
                }
                else
                {
                    value = s.ErrorMs;
                }

                points.Add(new Vector3(xFor(s.TimeSeconds), Mathf.Clamp(yFor(value), rect.yMin, rect.yMax), 0f));
            }

            FlushPolyline(points);
            Handles.EndGUI();
        }

        private static void FlushPolyline(List<Vector3> points)
        {
            if (points.Count >= 2) Handles.DrawAAPolyLine(2f, points.ToArray());
            points.Clear();
        }

        private void DrawEventLog()
        {
            GUILayout.Label("Events", EditorStyles.boldLabel);
            _eventScroll = EditorGUILayout.BeginScrollView(_eventScroll, GUILayout.MinHeight(70f));
            for (int i = _events.Count - 1; i >= 0 && i >= _events.Count - 40; i--)
                GUILayout.Label($"[{_events[i].TimeSeconds:F2}s] {_events[i].Label}", EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void ComputeWindowStats(out float meanAbsMs, out float maxAbsMs, out float correctionRateMsPerMin)
        {
            meanAbsMs = 0f;
            maxAbsMs = 0f;
            correctionRateMsPerMin = 0f;
            if (_samples.Count < 2) return;

            float now = _samples[^1].TimeSeconds;
            float t0 = now - _windowSeconds;
            int count = 0;
            LipSyncDriftSample? first = null;
            LipSyncDriftSample? last = null;

            foreach (LipSyncDriftSample s in _samples)
            {
                if (s.TimeSeconds < t0 || !s.AudioActive) continue;
                meanAbsMs += Mathf.Abs(s.ErrorMs);
                maxAbsMs = Mathf.Max(maxAbsMs, Mathf.Abs(s.ErrorMs));
                count++;
                first ??= s;
                last = s;
            }

            if (count > 0) meanAbsMs /= count;
            if (first.HasValue && last.HasValue && last.Value.TimeSeconds > first.Value.TimeSeconds + 0.5f)
            {
                float span = last.Value.TimeSeconds - first.Value.TimeSeconds;
                correctionRateMsPerMin =
                    (last.Value.CumulativeCorrectionMs - first.Value.CumulativeCorrectionMs) / span * 60f;
            }
        }

        private void LogSummary()
        {
            if (string.IsNullOrEmpty(_selectedCharacterId) || _samples.Count == 0) return;

            ComputeWindowStats(out float meanAbsMs, out float maxAbsMs, out float correctionRateMsPerMin);
            LipSyncDriftSample latest = _samples[^1];
            Debug.Log(
                $"[LipSyncDrift] char='{_selectedCharacterId}' err={latest.ErrorMs:F1}ms mean|err|={meanAbsMs:F1}ms " +
                $"max|err|={maxAbsMs:F1}ms corrRate={correctionRateMsPerMin:F1}ms/min state={latest.State} " +
                $"target={latest.AudioTargetSeconds:F2}s clock={latest.VisualClockSeconds:F2}s " +
                $"buffered={latest.BufferedSeconds:F2}s headroom={latest.HeadroomSeconds:F2}s");
        }

        private void ExportCsv()
        {
            if (string.IsNullOrEmpty(_selectedCharacterId) || _samples.Count == 0)
            {
                EditorUtility.DisplayDialog("LipSync Drift", "No samples to export yet.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel("Export drift samples", "",
                $"lipsync-drift-{_selectedCharacterId}", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder();
            sb.AppendLine("time_s,error_ms,audio_target_s,visual_clock_s,cumulative_correction_ms,buffered_s,headroom_s,state,audio_active");
            foreach (LipSyncDriftSample s in _samples)
            {
                sb.AppendLine(string.Join(",",
                    s.TimeSeconds.ToString("F4", CultureInfo.InvariantCulture),
                    s.ErrorMs.ToString("F2", CultureInfo.InvariantCulture),
                    s.AudioTargetSeconds.ToString("F4", CultureInfo.InvariantCulture),
                    s.VisualClockSeconds.ToString("F4", CultureInfo.InvariantCulture),
                    s.CumulativeCorrectionMs.ToString("F2", CultureInfo.InvariantCulture),
                    s.BufferedSeconds.ToString("F3", CultureInfo.InvariantCulture),
                    s.HeadroomSeconds.ToString("F3", CultureInfo.InvariantCulture),
                    s.State.ToString(),
                    s.AudioActive ? "1" : "0"));
            }

            sb.AppendLine();
            sb.AppendLine("event_time_s,label");
            foreach (LipSyncDriftEvent e in _events)
                sb.AppendLine($"{e.TimeSeconds.ToString("F4", CultureInfo.InvariantCulture)},\"{e.Label}\"");

            File.WriteAllText(path, sb.ToString());
            EditorUtility.RevealInFinder(path);
        }
    }
}
