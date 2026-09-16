#if UNITY_EDITOR
using Convai.Editor.UI;
using UnityEngine;

namespace Convai.Modules.Vision.Editor.UI
{
    /// <summary>
    ///     Provides branding and icon assets for Vision module editors.
    /// </summary>
    internal static class ConvaiVisionIconProvider
    {
        /// <summary>
        ///     Gets the Convai branding icon, with fallback to Unity's default inspector icon.
        /// </summary>
        public static Texture2D GetConvaiIcon() => ConvaiBrandedIconProvider.GetConvaiIcon();
    }
}
#endif
