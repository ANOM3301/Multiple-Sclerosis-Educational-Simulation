using System.Collections.Generic;
using Convai.Domain.Logging;
using Convai.Editor.Utilities;
using Convai.Runtime;
using Convai.Runtime.Adapters.Networking;
using Convai.Runtime.Components;
using Convai.Runtime.Logging;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor
{
    /// <summary>
    ///     Editor wizard for setting up required Convai SDK components in a scene.
    ///     Provides menu items to add missing components and validate scene setup.
    /// </summary>
    public static class ConvaiSetupWizard
    {
        private const string MenuPath = "GameObject/Convai/";

        /// <summary>
        ///     Adds all required Convai SDK components to the scene if they are missing.
        /// </summary>
        [MenuItem(MenuPath + "Setup Required Components", false, 10)]
        public static void SetupRequiredComponents()
        {
            ConvaiSceneSetupApi.BootstrapResult bootstrap = ConvaiSceneSetupApi.BootstrapScene();
            bool addedAny = bootstrap.AddedManager || bootstrap.AddedRoomManager;

            if (addedAny)
            {
                EditorUtility.DisplayDialog(
                    "Convai Setup Complete",
                    "Required Convai SDK components have been added to the scene.\n\n" +
                    "Next steps:\n" +
                    "1. Configure your API key:\n" +
                    "   Edit > Project Settings > Convai SDK\n\n" +
                    "2. Add ConvaiCharacter to your Characters:\n" +
                    "   Select Character > Add Component > Convai Character\n\n" +
                    "3. Add ConvaiPlayer to your player:\n" +
                    "   Select Player > Add Component > Convai Player",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Convai Setup",
                    "All required components are already in the scene!\n\n" +
                    "Your scene has:\n" +
                    "✓ ConvaiManager\n" +
                    "✓ ConvaiRoomManager",
                    "OK");
            }
        }

        /// <summary>
        ///     Validates the current scene setup and reports any issues.
        /// </summary>
        [MenuItem(MenuPath + "Validate Scene Setup", false, 11)]
        public static void ValidateSceneSetup()
        {
            ConvaiSceneSetupApi.ValidationReport report = ConvaiSceneSetupApi.ValidateCurrentScene();
            ConvaiCharacter[] characters = Object.FindObjectsByType<ConvaiCharacter>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            if (report.IsSuccess && report.Warnings.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Validation Passed ✓",
                    "Scene setup is correct!\n\n" +
                    $"Found {characters.Length} ConvaiCharacter(s) in scene.",
                    "OK");
                ConvaiLogger.Debug("[Convai Validation] Scene setup is correct.", LogCategory.Editor);
            }
            else
            {
                string message = "";

                if (report.Errors.Count > 0)
                    message += "❌ ERRORS (must fix):\n• " + string.Join("\n• ", report.Errors) + "\n\n";

                if (report.Warnings.Count > 0)
                    message += "⚠️ WARNINGS:\n• " + string.Join("\n• ", report.Warnings) + "\n\n";

                if (report.NextSteps.Count > 0)
                    message += "How to fix:\n• " + string.Join("\n• ", report.NextSteps);

                EditorUtility.DisplayDialog(
                    report.Errors.Count > 0 ? "Validation Failed" : "Validation Warnings",
                    message,
                    "OK");

                if (report.Errors.Count > 0)
                    ConvaiLogger.Error("[Convai Validation] " + message, LogCategory.Editor);
                else
                    ConvaiLogger.Warning("[Convai Validation] " + message, LogCategory.Editor);
            }
        }

        /// <summary>
        ///     Opens the Convai SDK documentation in a browser.
        /// </summary>
        [MenuItem(MenuPath + "Open Documentation", false, 100)]
        public static void OpenDocumentation() =>
            UnityEngine.Application.OpenURL(ConvaiEditorLinks.DocsUnityQuickstartUrl);

        /// <summary>
        ///     Opens the Convai SDK settings in Project Settings.
        /// </summary>
        [MenuItem(MenuPath + "Open SDK Settings", false, 101)]
        public static void OpenSDKSettings() => SettingsService.OpenProjectSettings("Project/Convai SDK");
    }
}
