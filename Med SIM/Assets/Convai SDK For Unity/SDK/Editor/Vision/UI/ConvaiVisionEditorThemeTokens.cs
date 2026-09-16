#if UNITY_EDITOR
using UnityEngine;

namespace Convai.Modules.Vision.Editor.UI
{
    /// <summary>
    ///     Theme tokens for Vision module editor UI.
    ///     Follows the design system established by LipSync module.
    /// </summary>
    internal static class ConvaiVisionEditorThemeTokens
    {
        // Layout Constants
        public const int SectionHeaderRowHeight = 22;
        public const int SectionIconFontSize = 16;
        public const int SectionIconCellWidth = 16;
        public const int SectionChevronCellWidth = 12;
        public const int SectionIconSpacing = 1;
        public const int SectionChevronTextSpacing = 2;

        public const int SectionBodyTopPadding = 4;
        public const int SectionBodyBottomPadding = 6;
        public const int SectionBodyBottomFill = 4;
        public const int SectionOuterSpacing = 4;

        // Color Tokens - Semantic colors for Vision module
        public static readonly Color Accent = new(0.322f, 0.718f, 0.533f); // Convai Green
        public static readonly Color AccentEmphasis = new(0.435f, 0.812f, 0.592f); // Lighter Green
        public static readonly Color Warning = new(1f, 0.655f, 0.149f); // Orange
        public static readonly Color Error = new(0.937f, 0.325f, 0.314f); // Red
        public static readonly Color Info = new(0.129f, 0.588f, 0.953f); // Blue

        // Background Colors
        public static readonly Color HeaderBackground = new(0.18f, 0.18f, 0.18f, 0.9f);
        public static readonly Color SectionBackground = new(0.22f, 0.22f, 0.22f, 0.5f);
        public static readonly Color AlternateRowBackground = new(0.2f, 0.2f, 0.2f, 0.3f);
        public static readonly Color TableHeaderBackground = new(0.15f, 0.15f, 0.15f, 0.8f);

        /// <summary>
        ///     Creates a semi-transparent divider color from a base color.
        /// </summary>
        public static Color DividerColor(Color baseColor) => new(baseColor.r, baseColor.g, baseColor.b, 0.3f);
    }
}
#endif
