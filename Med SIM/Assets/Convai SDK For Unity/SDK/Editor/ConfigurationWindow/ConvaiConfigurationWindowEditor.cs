using System;
using System.Collections.Generic;
using System.Linq;
using Convai.Editor.ConfigurationWindow.Components;
using Convai.Editor.ConfigurationWindow.Components.Sections;
using Convai.Editor.ConfigurationWindow.Components.Sections.LongTermMemory;
using Convai.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Convai.Editor.ConfigurationWindow
{
    /// <summary>
    ///     Main editor window for the Convai SDK configuration.
    ///     Provides a comprehensive UI for managing SDK settings, account, logging, and features.
    /// </summary>
    /// <remarks>
    ///     Access via the Convai menu in Unity's menu bar.
    ///     The window uses UI Toolkit for rendering and supports multiple configuration sections.
    /// </remarks>
    public class ConvaiConfigurationWindowEditor : EditorWindow
    {
        private const string WindowTitle = "Convai Editor";
        private static readonly Vector2 MinimumWindowSize = new(980, 620);
        private ConvaiConfigurationWindow _configurationWindow;
        private string _pendingSection = ConvaiWelcomeSection.SECTION_NAME;

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            _configurationWindow = new ConvaiConfigurationWindow();
            rootVisualElement.Add(_configurationWindow);
            rootVisualElement.styleSheets.Clear();
            if (ConvaiEditorSettings.Instance.UnityStyleSheet != null)
                rootVisualElement.styleSheets.Add(ConvaiEditorSettings.Instance.UnityStyleSheet);
            if (ConvaiEditorSettings.Instance.ConvaiConfigurationWindowStyleSheet != null)
                rootVisualElement.styleSheets.Add(ConvaiEditorSettings.Instance.ConvaiConfigurationWindowStyleSheet);
            rootVisualElement.Q<VisualElement>("convai-logo").style.backgroundImage =
                new StyleBackground(ConvaiEditorSettings.Instance.ConvaiLogoTextureWhite);
            TryOpenPendingSection();
        }

        /// <summary>Opens the configuration window to the Welcome section.</summary>
        [MenuItem("Convai/Welcome", priority = 1)]
        public static void OpenWelcomeWindow() => OpenSection(ConvaiWelcomeSection.SECTION_NAME);

        /// <summary>Opens the configuration window to the Account section.</summary>
        [MenuItem("Convai/Account", priority = 2)]
        public static void OpenAccountWindow() => OpenSection(ConvaiAccountSection.SECTION_NAME);

        /// <summary>Opens the configuration window to the SDK Settings section.</summary>
        [MenuItem("Convai/Settings", priority = 3)]
        public static void OpenSettingsWindow() => OpenSection(ConvaiSettingsSection.SECTION_NAME);

        /// <summary>Opens the configuration window to the AI Coding section.</summary>
        public static void OpenAICodingWindow() => OpenSection(ConvaiAICodingSection.SECTION_NAME);

        /// <summary>Opens the configuration window to the Long Term Memory section.</summary>
        [MenuItem("Convai/Long Term Memory", priority = 4)]
        public static void OpenLongTermMemoryWindow() => OpenSection(ConvaiLongTermMemorySection.SECTION_NAME);

#if CONVAI_ENABLE_UPDATES_SECTION
        /// <summary>Opens the configuration window to the Updates section.</summary>
        [MenuItem("Convai/Updates", priority = 20)]
        public static void OpenUpdateWindow() => OpenSection(ConvaiUpdatesSection.SECTION_NAME);
#endif

        /// <summary>Opens the configuration window to the Contact section.</summary>
        [MenuItem("Convai/Contact Us", priority = 21)]
        public static void OpenContactUsWindow() => OpenSection(ConvaiContactSection.SECTION_NAME);

        private static void OpenSection(string sectionName)
        {
            IReadOnlyList<ConfigurationSectionDescriptor> enabledSections =
                ConfigurationSectionRegistry.GetEnabledSections();
            ConfigurationSectionDescriptor descriptor = enabledSections.FirstOrDefault(section =>
                string.Equals(section.SectionId, sectionName, StringComparison.Ordinal));

            if (descriptor == null && enabledSections.Count > 0) descriptor = enabledSections[0];

            if (descriptor == null) return;

            bool hasApiKey = ConvaiSettings.Instance != null && ConvaiSettings.Instance.HasApiKey;
            if (descriptor.RequiresApiKey && !hasApiKey)
            {
                EditorUtility.DisplayDialog(
                    "API Key Required",
                    "Please set up your API Key in the Settings section (Convai > Settings) to access this section.",
                    "OK");
                descriptor = enabledSections.FirstOrDefault(section =>
                                 string.Equals(section.SectionId, ConvaiWelcomeSection.SECTION_NAME,
                                     StringComparison.Ordinal)) ??
                             descriptor;
            }

            var window = GetWindow<ConvaiConfigurationWindowEditor>(WindowTitle);
            window.minSize = MinimumWindowSize;
            window._pendingSection = descriptor.SectionId;
            window.Show();
            window.Focus();
            window.TryOpenPendingSection();
        }

        private void TryOpenPendingSection()
        {
            if (_configurationWindow == null) return;

            if (string.IsNullOrEmpty(_pendingSection)) _pendingSection = ConvaiWelcomeSection.SECTION_NAME;

            _configurationWindow.OpenSection(_pendingSection);
        }
    }
}
