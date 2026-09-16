using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Convai.Domain.Logging;
using Convai.Runtime.Logging;
using UnityEditor;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using CompilationPipeline = UnityEditor.Compilation.CompilationPipeline;
using PackageManagerClient = UnityEditor.PackageManager.Client;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageStatusCode = UnityEditor.PackageManager.StatusCode;

namespace Convai.Editor
{
    /// <summary>Read-only snapshot consumed by every AI coding setup surface.</summary>
    internal readonly struct ConvaiAICodingSetupState
    {
        internal ConvaiAICodingSetupState(
            bool unityReady,
            string unityVersion,
            bool assistantReady,
            string assistantVersion,
            bool skillReady,
            bool toolsReady,
            int toolCount,
            string toolIssue,
            bool repairRunning,
            string repairMessage,
            MessageType repairMessageType)
        {
            UnityReady = unityReady;
            UnityVersion = unityVersion;
            AssistantReady = assistantReady;
            AssistantVersion = assistantVersion;
            SkillReady = skillReady;
            ToolsReady = toolsReady;
            ToolCount = toolCount;
            ToolIssue = toolIssue;
            RepairRunning = repairRunning;
            RepairMessage = repairMessage;
            RepairMessageType = repairMessageType;
        }

        internal bool UnityReady { get; }
        internal string UnityVersion { get; }
        internal bool AssistantReady { get; }
        internal string AssistantVersion { get; }
        internal bool SkillReady { get; }
        internal bool ToolsReady { get; }
        internal int ToolCount { get; }
        internal string ToolIssue { get; }
        internal bool RepairRunning { get; }
        internal string RepairMessage { get; }
        internal MessageType RepairMessageType { get; }
    }

    internal sealed class ConvaiAICodingSetupWindow : EditorWindow
    {
        private const string AssistantPackageName = "com.unity.ai.assistant";
        private const string McpSettingsPath = "Project/AI/Unity MCP Server";
        private const string RegistryTypeName = "Unity.AI.MCP.Editor.ToolRegistry.McpToolRegistry";
        private const string BridgeTypeName = "Unity.AI.MCP.Editor.UnityMCPBridge";
        private const string RepairPendingKey = "Convai.AICodingSetup.RepairPending";
        private const string RepairMessageKey = "Convai.AICodingSetup.RepairMessage";
        private const string RepairMessageTypeKey = "Convai.AICodingSetup.RepairMessageType";
        private const string RepairStartedTicksKey = "Convai.AICodingSetup.RepairStartedTicks";
        private const string RepairGoalKey = "Convai.AICodingSetup.RepairGoal";
        private const double RepairTimeoutSeconds = 60d;
        internal static int ExpectedToolCount => ExpectedToolNames.Length;
        internal const string AssistantPackageIdentifier = "com.unity.ai.assistant@2.14.0-pre.1";
        internal static readonly string[] ExpectedToolNames = LoadExpectedToolNames();
        private static readonly Version MinimumAssistantVersion = new(2, 13, 0);
        private static readonly Version MaximumAssistantVersion = new(3, 0, 0);
        private static AddRequest _assistantInstallRequest;
        private static double _nextRegistryRefreshAt;

        private enum RepairGoal
        {
            FullSetup,
            PackagedSkill,
            ToolRegistration
        }

        [MenuItem("Convai/AI Coding Setup", priority = 12)]
        private static void OpenWindow() =>
            ConfigurationWindow.ConvaiConfigurationWindowEditor.OpenAICodingWindow();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Convai AI Coding Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Unity owns MCP client configuration. Convai adds SDK tools, a packaged skill, and optional project instruction blocks.",
                MessageType.Info);

            ConvaiAICodingSetupState state = GetSetupState();

            DrawStatus(
                "Unity 6000+",
                state.UnityReady,
                state.UnityVersion,
                null,
                "Install or open this project with Unity 6000 or newer.");
            DrawStatus(
                "Unity AI Assistant",
                state.AssistantReady,
                state.AssistantVersion ?? "not installed",
                CanRepairAssistant(state.AssistantVersion) ? BeginAssistantRepair : null,
                $"Installs supported package {AssistantPackageIdentifier}.");
            DrawStatus(
                "Packaged Convai skill",
                state.SkillReady,
                "AIAssistantSkills/convai-unity-sdk/SKILL.md",
                state.SkillReady ? null : BeginSkillRepair,
                "Refreshes package assets and recompiles Editor assemblies.");
            DrawStatus(
                "Convai tools",
                state.ToolsReady,
                state.AssistantReady
                    ? state.ToolsReady
                        ? $"{state.ToolCount}/{ExpectedToolCount} registered"
                        : $"{state.ToolCount}/{ExpectedToolCount}; {state.ToolIssue}"
                    : $"{state.ToolCount}/{ExpectedToolCount}; install Unity AI Assistant first",
                CanRepairToolRegistration(state.AssistantVersion, state.ToolsReady)
                    ? BeginToolRegistrationRepair
                    : null,
                state.AssistantReady
                    ? "Refreshes the Unity MCP registry, recompiles Editor assemblies, and reconnects the bridge."
                    : "Fix Unity AI Assistant first.");

            if (!string.IsNullOrWhiteSpace(state.RepairMessage))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(state.RepairMessage, state.RepairMessageType);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Open Unity MCP Server Settings", GUILayout.Height(28f)))
                OpenUnityMcpSettings();

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Managed project instructions", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Writes only after button click. Existing non-Convai content stays intact.", EditorStyles.wordWrappedMiniLabel);

            foreach (ConvaiAgentClient client in Enum.GetValues(typeof(ConvaiAgentClient)))
                DrawClient(client);
        }

        private static void DrawStatus(
            string label,
            bool ready,
            string detail,
            Action fixAction,
            string fixTooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(ready ? "✓" : "!", GUILayout.Width(18f));
                GUILayout.Label(label, GUILayout.Width(170f));
                GUILayout.Label(detail ?? string.Empty, EditorStyles.miniLabel);
                if (ready)
                {
                    GUILayout.Space(62f);
                    return;
                }

                using (new EditorGUI.DisabledScope(fixAction == null || IsRepairRunning))
                {
                    var content = new GUIContent("Fix", fixTooltip);
                    if (GUILayout.Button(content, GUILayout.Width(58f))) fixAction?.Invoke();
                }
            }
        }

        internal static bool IsRepairRunning =>
            _assistantInstallRequest != null || SessionState.GetBool(RepairPendingKey, false);

        internal static ConvaiAICodingSetupState GetSetupState()
        {
            bool unityReady = IsUnityCompatible();
            string assistantVersion = FindAssistantVersion();
            bool assistantReady = IsAssistantCompatible(assistantVersion);
            bool skillReady = HasPackagedSkill();
            bool toolsReady = HasExpectedRegisteredConvaiTools(out int toolCount, out string toolIssue);
            return new ConvaiAICodingSetupState(
                unityReady,
                UnityEngine.Application.unityVersion,
                assistantReady,
                assistantVersion,
                skillReady,
                toolsReady,
                toolCount,
                toolIssue,
                IsRepairRunning,
                SessionState.GetString(RepairMessageKey, string.Empty),
                GetRepairMessageType());
        }

        internal static void OpenUnityMcpSettings() => SettingsService.OpenProjectSettings(McpSettingsPath);

        internal static bool CanRepairAssistant(string version) => !IsAssistantCompatible(version);

        internal static bool CanRepairToolRegistration(string assistantVersion, bool toolsReady) =>
            IsAssistantCompatible(assistantVersion) && !toolsReady;

        internal static void BeginAssistantRepair()
        {
            if (!CanStartRepair()) return;
            try
            {
                SetRepairStatus(
                    $"Installing {AssistantPackageIdentifier}. Unity may recompile and reload assemblies.",
                    MessageType.Info,
                    true,
                    true);
                SessionState.SetInt(RepairGoalKey, (int)RepairGoal.FullSetup);
                _assistantInstallRequest = PackageManagerClient.Add(AssistantPackageIdentifier);
                EditorApplication.update -= PollAssistantInstall;
                EditorApplication.update += PollAssistantInstall;
                RepaintOpenWindows();
            }
            catch (Exception exception)
            {
                FailRepair($"Could not start Unity AI Assistant installation: {exception.Message}");
            }
        }

        private static void PollAssistantInstall()
        {
            if (_assistantInstallRequest == null)
            {
                EditorApplication.update -= PollAssistantInstall;
                return;
            }

            if (!_assistantInstallRequest.IsCompleted)
            {
                RepaintOpenWindows();
                return;
            }

            AddRequest completed = _assistantInstallRequest;
            _assistantInstallRequest = null;
            EditorApplication.update -= PollAssistantInstall;
            if (completed.Status == PackageStatusCode.Success)
            {
                string installedVersion = completed.Result?.version ?? "unknown version";
                ConvaiLogger.Info(
                    $"Unity AI Assistant {installedVersion} installed. Refreshing Convai MCP tools.",
                    LogCategory.Editor);
                BeginRegistrationRepairInternal(
                    $"Unity AI Assistant {installedVersion} installed. Compiling and registering Convai tools.",
                    RepairGoal.FullSetup);
                return;
            }

            string error = completed.Error?.message ?? "Unity Package Manager returned an unknown error.";
            FailRepair($"Unity AI Assistant installation failed: {error}");
        }

        internal static void BeginSkillRepair()
        {
            if (!CanStartRepair()) return;
            BeginRegistrationRepairInternal(
                "Refreshing package assets and locating the packaged Convai skill.",
                RepairGoal.PackagedSkill);
        }

        internal static void BeginToolRegistrationRepair()
        {
            if (!CanStartRepair()) return;
            BeginRegistrationRepairInternal(
                "Refreshing package assets and registering Convai MCP tools.",
                RepairGoal.ToolRegistration);
        }

        private static void BeginRegistrationRepairInternal(string message, RepairGoal goal)
        {
            try
            {
                SessionState.SetInt(RepairGoalKey, (int)goal);
                SetRepairStatus(message, MessageType.Info, true, true);
                TryRefreshToolRegistry(out _);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                CompilationPipeline.RequestScriptCompilation();
                ScheduleRepairCompletionPoll();
            }
            catch (Exception exception)
            {
                FailRepair($"Could not refresh Convai MCP tools: {exception.Message}");
            }
        }

        private static bool CanStartRepair()
        {
            if (IsRepairRunning) return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SetRepairStatus(
                    "Exit Play Mode before repairing AI coding setup. Play Mode was not changed automatically.",
                    MessageType.Warning,
                    false,
                    false);
                return false;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                SetRepairStatus(
                    "Unity is compiling or updating packages. Wait for it to finish, then click Fix again.",
                    MessageType.Warning,
                    false,
                    false);
                return false;
            }

            return true;
        }

        private static void ScheduleRepairCompletionPoll()
        {
            _nextRegistryRefreshAt = 0d;
            EditorApplication.update -= PollRepairCompletion;
            EditorApplication.update += PollRepairCompletion;
            RepaintOpenWindows();
        }

        private static void PollRepairCompletion()
        {
            if (!SessionState.GetBool(RepairPendingKey, false))
            {
                EditorApplication.update -= PollRepairCompletion;
                return;
            }

            if (_assistantInstallRequest != null || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                RepaintOpenWindows();
                return;
            }

            string assistantVersion = FindAssistantVersion();
            var goal = (RepairGoal)SessionState.GetInt(RepairGoalKey, (int)RepairGoal.FullSetup);
            if (goal == RepairGoal.PackagedSkill)
            {
                if (HasPackagedSkill())
                    CompleteRepair("Packaged Convai skill ready.");
                else if (HasRepairTimedOut())
                    FailRepair("The packaged Convai skill is still missing. Reimport or update the Convai SDK package, then retry.");
                return;
            }

            if (!IsAssistantCompatible(assistantVersion))
            {
                if (HasRepairTimedOut())
                    FailRepair("Unity AI Assistant is still unavailable after the package refresh. Check Package Manager and Editor logs, then retry.");
                return;
            }

            if (goal == RepairGoal.FullSetup && !HasPackagedSkill())
            {
                if (HasRepairTimedOut())
                    FailRepair("The packaged Convai skill is still missing. Reimport or update the Convai SDK package, then retry.");
                return;
            }

            bool toolsReady = HasExpectedRegisteredConvaiTools(out int toolCount, out string toolIssue);
            string registryError = string.Empty;
            if (!toolsReady && EditorApplication.timeSinceStartup >= _nextRegistryRefreshAt)
            {
                TryRefreshToolRegistry(out registryError);
                _nextRegistryRefreshAt = EditorApplication.timeSinceStartup + 1d;
                toolsReady = HasExpectedRegisteredConvaiTools(out toolCount, out toolIssue);
            }

            if (toolsReady)
            {
                if (!TryRestartUnityMcpBridge(out bool restarted, out string bridgeError))
                {
                    FailRepair($"Convai tools registered, but Unity MCP bridge reconnect failed: {bridgeError}");
                    return;
                }

                string reconnect = restarted ? " Unity MCP bridge reconnected." : string.Empty;
                CompleteRepair($"AI coding setup ready. {toolCount}/{ExpectedToolCount} Convai tools registered.{reconnect}");
                return;
            }

            if (HasRepairTimedOut())
            {
                string suffix = string.IsNullOrWhiteSpace(registryError) ? string.Empty : $" {registryError}";
                FailRepair($"Unity AI Assistant loaded, but the Convai tool catalog is unhealthy: {toolIssue}.{suffix}");
            }
        }

        internal static bool TryRefreshToolRegistry(out string error)
        {
            Type registryType = FindUnityMcpType(RegistryTypeName);
            if (registryType == null)
            {
                error = "Unity MCP tool registry is not loaded.";
                return false;
            }

            MethodInfo refresh = registryType.GetMethod(
                "RefreshTools",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (refresh == null)
            {
                error = "Unity MCP tool registry has no compatible RefreshTools method.";
                return false;
            }

            try
            {
                refresh.Invoke(null, null);
                error = string.Empty;
                return true;
            }
            catch (TargetInvocationException exception)
            {
                error = exception.InnerException?.Message ?? exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal static bool HasExpectedRegisteredConvaiTools(out int toolCount, out string issue)
        {
            if (!TryGetRegisteredConvaiToolNames(out string[] names, out issue))
            {
                toolCount = 0;
                return false;
            }

            toolCount = names.Length;
            return HasExpectedToolNames(names, out issue);
        }

        internal static bool HasExpectedToolNames(IEnumerable<string> toolNames, out string issue)
        {
            var expected = new HashSet<string>(ExpectedToolNames, StringComparer.Ordinal);
            var actual = new HashSet<string>(
                (toolNames ?? Enumerable.Empty<string>()).Where(name =>
                    !string.IsNullOrWhiteSpace(name) && name.StartsWith("Convai_", StringComparison.Ordinal)),
                StringComparer.Ordinal);
            string[] missing = expected.Except(actual).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            string[] unexpected = actual.Except(expected).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (missing.Length == 0 && unexpected.Length == 0)
            {
                issue = string.Empty;
                return true;
            }

            var parts = new List<string>(2);
            if (missing.Length > 0) parts.Add($"missing {string.Join(", ", missing)}");
            if (unexpected.Length > 0) parts.Add($"unexpected {string.Join(", ", unexpected)}");
            issue = string.Join("; ", parts);
            return false;
        }

        private static string[] LoadExpectedToolNames()
        {
            Type catalogType = Type.GetType(
                "Convai.Editor.AI.ConvaiMcpToolCatalog, Convai.Editor.AI",
                throwOnError: false);
            FieldInfo allField = catalogType?.GetField(
                "All",
                BindingFlags.Static | BindingFlags.NonPublic);
            return allField?.GetValue(null) is IEnumerable<string> toolIds
                ? toolIds.Select(toolId => toolId.Replace('.', '_')).ToArray()
                : Array.Empty<string>();
        }

        internal static bool TryRestartUnityMcpBridge(out bool restarted, out string error)
        {
            restarted = false;
            Type bridgeType = FindUnityMcpType(BridgeTypeName);
            if (bridgeType == null)
            {
                error = "Unity MCP bridge is not loaded.";
                return false;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            PropertyInfo isRunningProperty = bridgeType.GetProperty("IsRunning", flags);
            MethodInfo stop = bridgeType.GetMethod("Stop", flags, null, Type.EmptyTypes, null);
            MethodInfo start = bridgeType.GetMethod("Start", flags, null, Type.EmptyTypes, null);
            if (isRunningProperty == null || stop == null || start == null)
            {
                error = "Unity MCP bridge has no compatible lifecycle API.";
                return false;
            }

            try
            {
                if (!(isRunningProperty.GetValue(null) is bool isRunning) || !isRunning)
                {
                    error = string.Empty;
                    return true;
                }

                stop.Invoke(null, null);
                start.Invoke(null, null);
                restarted = true;
                error = string.Empty;
                return true;
            }
            catch (TargetInvocationException exception)
            {
                error = exception.InnerException?.Message ?? exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool HasRepairTimedOut()
        {
            string value = SessionState.GetString(RepairStartedTicksKey, string.Empty);
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)) return false;
            return DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) >=
                   TimeSpan.FromSeconds(RepairTimeoutSeconds);
        }

        private static void CompleteRepair(string message)
        {
            EditorApplication.update -= PollRepairCompletion;
            SetRepairStatus(message, MessageType.Info, false, false);
            ConvaiLogger.Info(message, LogCategory.Editor);
        }

        private static void FailRepair(string message)
        {
            _assistantInstallRequest = null;
            EditorApplication.update -= PollAssistantInstall;
            EditorApplication.update -= PollRepairCompletion;
            SetRepairStatus(message, MessageType.Error, false, false);
            ConvaiLogger.Error(message, LogCategory.Editor);
        }

        private static void SetRepairStatus(
            string message,
            MessageType messageType,
            bool pending,
            bool resetStartedAt)
        {
            SessionState.SetString(RepairMessageKey, message ?? string.Empty);
            SessionState.SetInt(RepairMessageTypeKey, (int)messageType);
            SessionState.SetBool(RepairPendingKey, pending);
            if (resetStartedAt)
                SessionState.SetString(
                    RepairStartedTicksKey,
                    DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            else if (!pending)
                SessionState.SetString(RepairStartedTicksKey, string.Empty);
            RepaintOpenWindows();
        }

        private static MessageType GetRepairMessageType()
        {
            int value = SessionState.GetInt(RepairMessageTypeKey, (int)MessageType.Info);
            return Enum.IsDefined(typeof(MessageType), value) ? (MessageType)value : MessageType.Info;
        }

        private static void RepaintOpenWindows()
        {
            foreach (ConvaiAICodingSetupWindow window in Resources.FindObjectsOfTypeAll<ConvaiAICodingSetupWindow>())
                window.Repaint();
        }

        private void DrawClient(ConvaiAgentClient client)
        {
            string projectRoot = GetProjectRoot();
            bool installed;
            try { installed = ConvaiAgentInstructionsInstaller.ContainsManagedBlock(projectRoot, client); }
            catch (InvalidOperationException) { installed = false; }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(GetClientLabel(client), GUILayout.Width(130f));
                GUILayout.Label(ConvaiAgentInstructionsInstaller.GetRelativePath(client), EditorStyles.miniLabel);
                string action = installed ? "Update" : "Install";
                if (GUILayout.Button(action, GUILayout.Width(70f)))
                    RunInstructionAction(() => ConvaiAgentInstructionsInstaller.Upsert(projectRoot, client));
                using (new EditorGUI.DisabledScope(!installed))
                    if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                        RunInstructionAction(() => ConvaiAgentInstructionsInstaller.Remove(projectRoot, client));
            }
        }

        private void RunInstructionAction(Action action)
        {
            try
            {
                action();
                AssetDatabase.Refresh();
                Repaint();
            }
            catch (Exception exception)
            {
                ConvaiLogger.Error($"AI Coding Setup failed: {exception.Message}", LogCategory.Editor);
                EditorUtility.DisplayDialog("Convai AI Coding Setup", exception.Message, "OK");
            }
        }

        internal static string GetClientLabel(ConvaiAgentClient client) => client switch
        {
            ConvaiAgentClient.ClaudeCode => "Claude Code",
            ConvaiAgentClient.Copilot => "VS Code Copilot",
            _ => client.ToString()
        };

        private static bool IsUnityCompatible() =>
            Version.TryParse(UnityEngine.Application.unityVersion.Split('f', 'b', 'a', 'p')[0], out Version version) &&
            version.Major >= 6000;

        private static string FindAssistantVersion() => PackageInfo.GetAllRegisteredPackages()
            .FirstOrDefault(package => package.name == AssistantPackageName)?.version;

        internal static bool IsAssistantCompatible(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            int separator = value.IndexOf('-');
            string numeric = separator >= 0 ? value.Substring(0, separator) : value;
            string prerelease = separator >= 0 ? value.Substring(separator + 1) : string.Empty;
            if (!Version.TryParse(numeric, out Version version)) return false;
            if (version < MinimumAssistantVersion || version > MaximumAssistantVersion) return false;
            if (version == MaximumAssistantVersion) return prerelease.Length > 0;
            if (version > MinimumAssistantVersion) return true;
            if (prerelease.Length == 0) return true;
            const string prefix = "pre.";
            return prerelease.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                   int.TryParse(prerelease.Substring(prefix.Length), out int prereleaseNumber) &&
                   prereleaseNumber >= 2;
        }

        private static bool HasPackagedSkill()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(ConvaiAICodingSetupWindow).Assembly);
            return package != null && System.IO.File.Exists(System.IO.Path.Combine(
                package.resolvedPath,
                "AIAssistantSkills/convai-unity-sdk/SKILL.md"));
        }

        private static bool TryGetRegisteredConvaiToolNames(out string[] names, out string error)
        {
            names = Array.Empty<string>();
            Type registryType = FindUnityMcpType(RegistryTypeName);
            if (registryType == null)
            {
                error = "Unity MCP tool registry is not loaded";
                return false;
            }

            MethodInfo getTools = registryType.GetMethod(
                "GetAvailableTools",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(bool) },
                null);
            if (getTools == null)
            {
                error = "Unity MCP tool registry has no compatible GetAvailableTools method";
                return false;
            }

            try
            {
                object value = getTools.Invoke(null, new object[] { true });
                if (!(value is IEnumerable tools))
                {
                    error = "Unity MCP tool registry returned an invalid tool collection";
                    return false;
                }

                var registered = new List<string>();
                foreach (object tool in tools)
                {
                    if (tool == null) continue;
                    PropertyInfo nameProperty = tool.GetType().GetProperty(
                        "name",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (nameProperty?.GetValue(tool) is string name &&
                        name.StartsWith("Convai_", StringComparison.Ordinal))
                        registered.Add(name);
                }

                names = registered.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
                error = string.Empty;
                return true;
            }
            catch (TargetInvocationException exception)
            {
                error = exception.InnerException?.Message ?? exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static Type FindUnityMcpType(string fullName) =>
            Type.GetType($"{fullName}, Unity.AI.MCP.Editor") ??
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);

        internal static string GetProjectRoot() =>
            System.IO.Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? UnityEngine.Application.dataPath;

        [InitializeOnLoadMethod]
        private static void ScheduleOnboardingPrompt()
        {
            if (UnityEngine.Application.isBatchMode) return;
            EditorApplication.delayCall += ShowOnboardingPromptOnce;
            if (SessionState.GetBool(RepairPendingKey, false))
                EditorApplication.delayCall += ScheduleRepairCompletionPoll;
        }

        private static void ShowOnboardingPromptOnce()
        {
            string key = "Convai.AICodingSetup.Shown." + Hash128.Compute(UnityEngine.Application.dataPath);
            if (EditorPrefs.GetBool(key, false)) return;
            string assistantVersion = FindAssistantVersion();
            if (!IsUnityCompatible() || !IsAssistantCompatible(assistantVersion) || !HasPackagedSkill()) return;
            EditorPrefs.SetBool(key, true);
            if (EditorUtility.DisplayDialog(
                    "Convai AI Coding Setup",
                    "Unity AI Assistant detected. Open Convai AI Coding Setup to configure external agents and inspect MCP readiness? No files are written automatically.",
                    "Open Setup",
                    "Later"))
                OpenWindow();
        }
    }
}
