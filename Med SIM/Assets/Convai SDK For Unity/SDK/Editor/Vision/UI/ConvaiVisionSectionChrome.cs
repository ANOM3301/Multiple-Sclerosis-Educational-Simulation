#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Convai.Modules.Vision.Editor.UI
{
    /// <summary>
    ///     Specification for a collapsible section header in Vision module editors.
    /// </summary>
    internal readonly struct ConvaiVisionSectionHeaderSpec
    {
        public ConvaiVisionSectionHeaderSpec(
            string editorTypeId,
            string sectionId,
            string title,
            string icon,
            Color? headerColor = null,
            int iconFontSize = ConvaiVisionEditorThemeTokens.SectionIconFontSize)
        {
            EditorTypeId = editorTypeId;
            SectionId = sectionId;
            Title = title;
            Icon = icon;
            HeaderColor = headerColor ?? ConvaiVisionEditorThemeTokens.Accent;
            IconFontSize = iconFontSize;
        }

        public string EditorTypeId { get; }
        public string SectionId { get; }
        public string Title { get; }
        public string Icon { get; }
        public Color HeaderColor { get; }
        public int IconFontSize { get; }
    }

    /// <summary>
    ///     Provides standardized collapsible section UI for Vision module editors.
    /// </summary>
    internal static class ConvaiVisionSectionChrome
    {
        /// <summary>
        ///     Draws a collapsible section header with icon, chevron, and title.
        ///     The entire row is clickable for expand/collapse.
        /// </summary>
        /// <returns>New expansion state</returns>
        public static bool DrawHeader(in ConvaiVisionSectionHeaderSpec spec, bool expanded)
        {
            ConvaiVisionEditorStyleCache.EnsureInitialized();

            GUIStyle headerLabelStyle = ConvaiVisionEditorStyleCache.SectionHeaderLabelStyle;
            GUIStyle iconStyle = ConvaiVisionEditorStyleCache.SectionIconStyle;
            GUIStyle chevronStyle = ConvaiVisionEditorStyleCache.SectionChevronStyle;

            SetTextColor(headerLabelStyle, spec.HeaderColor);
            SetTextColor(iconStyle, spec.HeaderColor);
            SetTextColor(chevronStyle, spec.HeaderColor);
            iconStyle.fontSize = spec.IconFontSize;

            EditorGUILayout.BeginHorizontal(GUILayout.Height(ConvaiVisionEditorThemeTokens.SectionHeaderRowHeight));

            GUI.color = spec.HeaderColor;
            GUILayout.Label(spec.Icon, iconStyle);
            GUI.color = Color.white;

            GUILayout.Space(ConvaiVisionEditorThemeTokens.SectionIconSpacing);
            GUILayout.Label(expanded ? "\u25BC" : "\u25B6", chevronStyle);
            GUILayout.Space(ConvaiVisionEditorThemeTokens.SectionChevronTextSpacing);

            Rect titleRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                headerLabelStyle,
                GUILayout.Height(ConvaiVisionEditorThemeTokens.SectionHeaderRowHeight),
                GUILayout.ExpandWidth(true));
            EditorGUI.LabelField(titleRect, spec.Title, headerLabelStyle);

            EditorGUILayout.EndHorizontal();

            Rect headerRect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(headerRect, MouseCursor.Link);

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && headerRect.Contains(currentEvent.mousePosition))
            {
                expanded = !expanded;
                currentEvent.Use();
            }

            if (expanded)
            {
                Rect lineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
                lineRect.x += 4;
                lineRect.width -= 8;
                EditorGUI.DrawRect(lineRect, ConvaiVisionEditorThemeTokens.DividerColor(spec.HeaderColor));
            }

            return expanded;
        }

        /// <summary>
        ///     Begins a section body with background and padding.
        /// </summary>
        public static void BeginBody(Color? backgroundOverride = null)
        {
            Color background = backgroundOverride ?? ConvaiVisionEditorThemeTokens.SectionBackground;

            Rect bodyRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(
                new Rect(
                    bodyRect.x,
                    bodyRect.y,
                    bodyRect.width,
                    bodyRect.height + ConvaiVisionEditorThemeTokens.SectionBodyBottomFill),
                background);

            GUILayout.Space(ConvaiVisionEditorThemeTokens.SectionBodyTopPadding);
            EditorGUI.indentLevel++;
        }

        /// <summary>
        ///     Ends a section body and restores layout state.
        /// </summary>
        public static void EndBody()
        {
            EditorGUI.indentLevel--;
            GUILayout.Space(ConvaiVisionEditorThemeTokens.SectionBodyBottomPadding);
            EditorGUILayout.EndVertical();
            GUILayout.Space(ConvaiVisionEditorThemeTokens.SectionOuterSpacing);
        }

        private static void SetTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.onNormal.textColor = color;
            style.focused.textColor = color;
            style.onFocused.textColor = color;
            style.hover.textColor = color;
            style.onHover.textColor = color;
            style.active.textColor = color;
            style.onActive.textColor = color;
        }
    }
}
#endif
