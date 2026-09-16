using System;
using System.Collections.Generic;
using Convai.Editor.ConfigurationWindow.Services;
using Convai.Editor.Utilities;
using Convai.Runtime;
using UnityEditor;

namespace Convai.Editor.Settings.Services
{
    /// <summary>One project-level health item, optionally with an automated fix.</summary>
    public sealed class ProjectHealthItem
    {
        public ProjectHealthItem(SetupHealthCheckResult result, string fixLabel = null, Action fix = null)
        {
            Result = result;
            FixLabel = fixLabel;
            Fix = fix;
        }

        public SetupHealthCheckResult Result { get; }

        /// <summary>Label for the fix button; null when no automated fix exists.</summary>
        public string FixLabel { get; }

        /// <summary>Automated fix action; null when the item is informational.</summary>
        public Action Fix { get; }
    }

    /// <summary>
    ///     Project-configuration health checks for the settings Setup Health section.
    ///     Complements the scene-level checks in <see cref="SetupHealthService" />.
    /// </summary>
    public static class ProjectSetupHealthService
    {
        private const string SettingsAssetPath = "Assets/Resources/ConvaiSettings.asset";
        private const string DefaultMicUsageDescription = "Microphone access is required for voice conversations.";

        /// <summary>Builds the project-level health report.</summary>
        public static IReadOnlyList<ProjectHealthItem> BuildProjectReport()
        {
            var items = new List<ProjectHealthItem>
            {
                CheckSettingsAsset(),
                new ProjectHealthItem(SetupHealthService.CheckApiKey()),
                CheckIosMicrophoneUsageDescription(),
                CheckAndroidMicrophonePermission()
            };

            items.AddRange(CheckDefineDrift());
            AddPlatformCaveats(items);
            return items;
        }

        private static ProjectHealthItem CheckSettingsAsset()
        {
            bool exists = AssetDatabase.LoadAssetAtPath<ConvaiSettings>(SettingsAssetPath) != null;
            if (exists)
            {
                return new ProjectHealthItem(new SetupHealthCheckResult(
                    "settings-asset", "Settings Asset", SetupHealthStatus.Healthy,
                    $"Present at {SettingsAssetPath}."));
            }

            return new ProjectHealthItem(
                new SetupHealthCheckResult(
                    "settings-asset", "Settings Asset", SetupHealthStatus.Blocked,
                    $"Missing at {SettingsAssetPath}. Runtime code cannot load project defaults."),
                "Create",
                ConvaiSettings.EnsureSettingsAssetExists);
        }

        private static ProjectHealthItem CheckIosMicrophoneUsageDescription()
        {
            if (!string.IsNullOrWhiteSpace(PlayerSettings.iOS.microphoneUsageDescription))
            {
                return new ProjectHealthItem(new SetupHealthCheckResult(
                    "ios-mic-usage", "iOS Microphone Usage Description", SetupHealthStatus.Healthy,
                    "Set in Player Settings."));
            }

            return new ProjectHealthItem(
                new SetupHealthCheckResult(
                    "ios-mic-usage", "iOS Microphone Usage Description", SetupHealthStatus.Warning,
                    "Empty. iOS builds crash on microphone access without a usage description."),
                "Set Default",
                () => PlayerSettings.iOS.microphoneUsageDescription = DefaultMicUsageDescription);
        }

        private static ProjectHealthItem CheckAndroidMicrophonePermission()
        {
            // Unity injects RECORD_AUDIO when Microphone APIs are referenced, but custom
            // manifests can strip it - there is no reliable editor-side check, so inform only.
            return new ProjectHealthItem(new SetupHealthCheckResult(
                "android-mic-permission", "Android Microphone Permission", SetupHealthStatus.Healthy,
                $"RECORD_AUDIO must be present in the merged manifest. See {ConvaiEditorLinks.DocsHomeUrl} if you use a custom manifest."));
        }

        private static IEnumerable<ProjectHealthItem> CheckDefineDrift()
        {
            foreach ((string symbol, string label, _) in ScriptingDefineToggleService.FeatureDefines)
            {
                IReadOnlyList<BuildTargetGroup> drifting = ScriptingDefineToggleService.GetDriftingGroups(symbol);
                if (drifting.Count == 0) continue;

                string symbolCopy = symbol;
                yield return new ProjectHealthItem(
                    new SetupHealthCheckResult(
                        $"define-drift-{symbol}", $"Define Drift: {label}", SetupHealthStatus.Warning,
                        $"{symbol} differs from the active build target on: {string.Join(", ", drifting)}."),
                    "Sync All",
                    () => ScriptingDefineToggleService.SyncToAllGroups(symbolCopy));
            }
        }

        private static void AddPlatformCaveats(List<ProjectHealthItem> items)
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                items.Add(new ProjectHealthItem(new SetupHealthCheckResult(
                    "webgl-caveats", "WebGL Platform", SetupHealthStatus.Warning,
                    "WebGL uses the browser transport path; microphone enumeration and some native features differ from desktop.")));
            }
        }
    }
}
