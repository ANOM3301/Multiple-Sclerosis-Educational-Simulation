#if UNITY_EDITOR
using UnityEditor;

namespace Convai.Modules.Vision.Editor.UI
{
    /// <summary>
    ///     Persists collapsible section state for Vision module editors.
    ///     State keys follow the pattern: Convai.Vision.{EditorType}.{SectionId}.Expanded
    /// </summary>
    internal static class ConvaiVisionSectionStateStore
    {
        private const string Prefix = "Convai.Vision";
        private const string Suffix = "Expanded";

        /// <summary>
        ///     Gets the expansion state for a section.
        /// </summary>
        /// <param name="hostId">Editor type identifier (e.g., "PublisherEditor")</param>
        /// <param name="sectionId">Section identifier (e.g., "Configuration")</param>
        /// <param name="defaultValue">Default expansion state if no saved value exists</param>
        public static bool Get(string hostId, string sectionId, bool defaultValue)
        {
            string key = BuildKey(hostId, sectionId);
            return EditorPrefs.GetBool(key, defaultValue);
        }

        public static void Set(string hostId, string sectionId, bool value)
        {
            string key = BuildKey(hostId, sectionId);
            EditorPrefs.SetBool(key, value);
        }

        /// <summary>
        ///     Builds a unique EditorPrefs key for a section.
        /// </summary>
        internal static string BuildKey(string hostId, string sectionId) =>
            $"{Prefix}.{Normalize(hostId)}.{Normalize(sectionId)}.{Suffix}";

        private static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Unknown";
            return raw.Trim().Replace(" ", string.Empty);
        }
    }
}
#endif
