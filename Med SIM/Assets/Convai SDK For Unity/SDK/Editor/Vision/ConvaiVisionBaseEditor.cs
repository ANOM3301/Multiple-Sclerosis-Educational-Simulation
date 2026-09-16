#if UNITY_EDITOR
using System;
using Convai.Modules.Vision.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Vision
{
    /// <summary>
    ///     Shared base for all Vision module custom editors.
    ///     Provides common drawing primitives, shared styles, and branding assets so individual
    ///     editors contain only their own domain logic.
    /// </summary>
    public abstract class ConvaiVisionBaseEditor : UnityEditor.Editor
    {
        protected const int SectionIconFontSize = ConvaiVisionEditorThemeTokens.SectionIconFontSize;

        // Fixed cell width for live-status grid columns
        protected const int LiveCellWidth = 110;

        // Theme shortcuts
        protected static readonly Color ConvaiGreen = ConvaiVisionEditorThemeTokens.Accent;
        protected static readonly Color ConvaiGreenLight = ConvaiVisionEditorThemeTokens.AccentEmphasis;
        protected static readonly Color ConvaiInfo = ConvaiVisionEditorThemeTokens.Info;
        protected static readonly Color HeaderBg = ConvaiVisionEditorThemeTokens.HeaderBackground;
        protected static readonly Color SectionBg = ConvaiVisionEditorThemeTokens.SectionBackground;

        // Semantic status colors
        protected static readonly Color StatusIdle = new(0.50f, 0.50f, 0.50f);
        protected static readonly Color StatusEditor = new(0.72f, 0.72f, 0.76f);
        protected static readonly Color DefaultValueColor = new(0.85f, 0.85f, 0.85f);
        private bool _stylesReady;
        protected Texture2D CircleTexture;

        protected Texture2D ConvaiIcon;

        // Shared styles
        protected GUIStyle HeaderStyle;
        protected GUIStyle LiveCellLabelStyle;
        protected GUIStyle LiveCellValueStyle;
        protected GUIStyle MiniButtonStyle;
        protected GUIStyle StatusLabelStyle;

        /// <summary>Unique key used to namespace EditorPrefs for this editor's section states.</summary>
        protected abstract string EditorStateHostId { get; }

        // Lifecycle
        protected virtual void OnEnable()
        {
            ConvaiIcon = ConvaiVisionIconProvider.GetConvaiIcon();
            if (CircleTexture == null) CircleTexture = BuildCircleTexture(32);
        }

        protected virtual void OnDisable()
        {
            _stylesReady = false;
            if (CircleTexture != null)
            {
                DestroyImmediate(CircleTexture);
                CircleTexture = null;
            }
        }

        private static Texture2D BuildCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float rSq = center * center;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                px[(y * size) + x] = new Color(1f, 1f, 1f, (dx * dx) + (dy * dy) <= rSq ? 1f : 0f);
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        protected void EnsureStyles()
        {
            if (_stylesReady) return;

            HeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15, alignment = TextAnchor.MiddleLeft, normal = { textColor = ConvaiGreenLight }
            };

            StatusLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft
            };

            MiniButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10, padding = new RectOffset(8, 8, 3, 3)
            };

            LiveCellLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9, normal = { textColor = new Color(0.55f, 0.55f, 0.58f) }
            };

            LiveCellValueStyle = new GUIStyle(EditorStyles.label) { fontSize = 11 };

            _stylesReady = true;
        }

        // Header

        /// <summary>
        ///     Draws the branded top header with icon, title, and a coloured status badge.
        /// </summary>
        protected void DrawHeader(string title, Color dotColor, string statusText)
        {
            const float h = 48f, iconSz = 24f, gap = 8f, statusW = 150f, titleH = 22f;

            Rect outer = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(
                new Rect(outer.x - 18f, outer.y - 4f, outer.width + 36f, h + 4f),
                HeaderBg);

            EditorGUILayout.BeginVertical(GUILayout.Height(h));
            Rect row = GUILayoutUtility.GetRect(0f, h, GUILayout.ExpandWidth(true), GUILayout.Height(h));

            // Convai icon
            var iconRect = new Rect(row.x, row.y + ((h - iconSz) * 0.5f), iconSz, iconSz);
            if (ConvaiIcon != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(iconRect, ConvaiIcon, ScaleMode.ScaleToFit, true);

            // Title
            GUI.Label(
                new Rect(iconRect.xMax + gap, row.y + ((h - titleH) * 0.5f) - 1f,
                    Mathf.Max(0f, row.width - iconSz - gap - statusW), titleH),
                title, HeaderStyle);

            // Status badge – right-aligned
            DrawStatusBadge(new Rect(row.xMax - statusW, row.y, statusW, h), dotColor, statusText);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            GUILayout.Space(2f);
        }

        private void DrawStatusBadge(Rect region, Color dotColor, string text)
        {
            if (StatusLabelStyle == null) return;
            StatusLabelStyle.normal.textColor = dotColor;

            const float dotSz = 7f;
            var content = new GUIContent(text);
            Vector2 sz = StatusLabelStyle.CalcSize(content);
            float totalW = dotSz + 5f + sz.x;
            float x = region.xMax - totalW - 6f;
            float cy = region.y + (region.height * 0.5f);

            DrawCircle(new Rect(x, cy - (dotSz * 0.5f), dotSz, dotSz), dotColor);
            GUI.Label(new Rect(x + dotSz + 5f, cy - (sz.y * 0.5f), sz.x, sz.y), content, StatusLabelStyle);
        }

        protected void DrawCircle(Rect rect, Color color)
        {
            if (CircleTexture == null) return;
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, CircleTexture, ScaleMode.ScaleToFit);
            GUI.color = prev;
        }

        // Section chrome helpers

        protected bool DrawSection(string sectionId, string title, bool expanded, string icon,
            Color? color = null, int? fontSize = null)
        {
            var spec = new ConvaiVisionSectionHeaderSpec(
                EditorStateHostId, sectionId, title, icon,
                color ?? ConvaiGreen, fontSize ?? SectionIconFontSize);
            return ConvaiVisionSectionChrome.DrawHeader(in spec, expanded);
        }

        protected void DrawSectionBody(Action draw, Color? bg = null)
        {
            ConvaiVisionSectionChrome.BeginBody(bg ?? SectionBg);
            draw?.Invoke();
            ConvaiVisionSectionChrome.EndBody();
        }

        // Message boxes

        protected void DrawWarningBox(string title, string message,
            string btnLabel = null, Action btnAction = null, float btnW = 110f)
            => DrawMsgBox(new Color(1f, 0.65f, 0.15f, 0.18f), "\u26A0", title, message, btnLabel, btnAction, btnW);

        protected void DrawInfoBox(string title, string message)
            => DrawMsgBox(new Color(0.129f, 0.588f, 0.953f, 0.18f), "\u2139", title, message);

        protected void DrawErrorBox(string title, string message)
            => DrawMsgBox(new Color(0.94f, 0.33f, 0.31f, 0.18f), "\u2715", title, message);

        private void DrawMsgBox(Color bg, string icon, string title, string body,
            string btnLabel = null, Action btnAction = null, float btnW = 110f)
        {
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = bg;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(icon, GUILayout.Width(18f));
            EditorGUILayout.BeginVertical();
            GUILayout.Label(title, EditorStyles.boldLabel);
            GUILayout.Label(body, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(btnLabel) && btnAction != null)
            {
                GUILayout.Space(2f);
                if (GUILayout.Button(btnLabel, MiniButtonStyle, GUILayout.Width(btnW)))
                    btnAction();
            }

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = prev;
        }

        // Live status helpers

        /// <summary>Draws a labelled value cell inside the live-status grid.</summary>
        protected void DrawLiveCell(string label, string value, Color valueColor, bool bold = false)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LiveCellWidth));
            GUILayout.Label(label.ToUpper(), LiveCellLabelStyle);
            LiveCellValueStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            LiveCellValueStyle.normal.textColor = valueColor;
            GUILayout.Label(value, LiveCellValueStyle);
            EditorGUILayout.EndVertical();
        }

        /// <summary>Draws the "enter play mode" placeholder inside the Live Status section.</summary>
        protected void DrawOfflinePlaceholder()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.color = new Color(0.55f, 0.55f, 0.58f);
            GUILayout.Label("\u25CF  Enter Play Mode to view live telemetry", EditorStyles.miniLabel);
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Draws the status label row (e.g. "▶ PUBLISHING") with an optional right-side badge.</summary>
        protected void DrawStatusRow(string statusText, Color statusColor,
            string badgeText = null, Color? badgeColor = null)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.color = statusColor;
            GUILayout.Label(statusText, LiveCellValueStyle, GUILayout.ExpandWidth(false));
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(badgeText))
            {
                GUI.color = badgeColor ?? ConvaiInfo;
                GUILayout.Label($"[{badgeText}]", EditorStyles.miniBoldLabel);
                GUI.color = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
