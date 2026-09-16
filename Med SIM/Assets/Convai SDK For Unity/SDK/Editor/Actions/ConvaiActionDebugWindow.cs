using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Convai.Domain.Logging;
using Convai.Runtime.Actions;
using Convai.Runtime.Components;
using Convai.Runtime.Logging;
using Convai.Shared.Actions;
using Convai.Shared.Types;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Convai.Editor.Actions
{
    /// <summary>
    ///     Live inspector for the action pipeline: rendered backend config, validator diagnostics,
    ///     local command injection, and a runtime dispatch event feed. Project-specific templates and
    ///     injection shortcuts come from <see cref="ConvaiActionDebugPresetRegistry" /> providers.
    /// </summary>
    public sealed partial class ConvaiActionDebugWindow : EditorWindow
    {
        private const int MaxEvents = 80;

        private readonly List<ActionDebugEvent> _events = new();
        private Vector2 _scroll;
        private ConvaiCharacter _character;
        private ConvaiActionConfigSource _source;
        private ConvaiActionDispatcher _dispatcher;
        private ConvaiCharacter _subscribedCharacter;
        private ConvaiActionDispatcher _subscribedDispatcher;
        private bool _autoScroll = true;
        private string _manualActionName = string.Empty;
        private string _manualTarget = string.Empty;

        [MenuItem("Convai/Developer/Action Debug Window", false, 112)]
        public static void ShowWindow()
        {
            ConvaiActionDebugWindow window = GetWindow<ConvaiActionDebugWindow>();
            window.titleContent = new GUIContent("Action Debug");
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            AutoResolve();
            Subscribe();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Unsubscribe();
        }

        private void OnEditorUpdate()
        {
            if (_character == null || _source == null || _dispatcher == null)
                AutoResolve();

            Subscribe();
            CapturePendingRuntimeUpdateIds();
        }

        private void OnGUI()
        {
            DrawToolbar();
            _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);
            EditorGUILayout.Space(8f);
            DrawConfigPreview();
            EditorGUILayout.Space(8f);
            DrawRuntimeSessionState();
            EditorGUILayout.Space(8f);
            DrawRuntimePatchComposer();
            EditorGUILayout.Space(8f);
            DrawInjectionControls();
            EditorGUILayout.Space(8f);
            DrawPresetProviders();
            EditorGUILayout.Space(8f);
            DrawEventFeed();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                    AutoResolve(force: true);

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                {
                    _events.Clear();
                    ClearRuntimeDiagnosticState();
                }

                GUILayout.FlexibleSpace();
                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto scroll", EditorStyles.toolbarButton);
            }

            _character = (ConvaiCharacter)EditorGUILayout.ObjectField("Character", _character, typeof(ConvaiCharacter), true);
            _source = (ConvaiActionConfigSource)EditorGUILayout.ObjectField("Config Source", _source, typeof(ConvaiActionConfigSource), true);
            _dispatcher = (ConvaiActionDispatcher)EditorGUILayout.ObjectField("Dispatcher", _dispatcher, typeof(ConvaiActionDispatcher), true);

            IReadOnlyList<IConvaiActionDebugPresetProvider> providers = ConvaiActionDebugPresetRegistry.Providers;
            for (int i = 0; i < providers.Count; i++)
            {
                string status = providers[i]?.DescribeCharacterState(_character);
                if (!string.IsNullOrEmpty(status))
                    EditorGUILayout.LabelField(providers[i].DisplayName, status);
            }
        }

        private void DrawConfigPreview()
        {
            EditorGUILayout.LabelField("Rendered backend config", EditorStyles.boldLabel);
            if (_source == null)
            {
                EditorGUILayout.HelpBox("No ConvaiActionConfigSource found.", MessageType.Warning);
                return;
            }

            IReadOnlyList<ConvaiActionConfigDiagnostic> diagnostics = ConvaiActionConfigValidator.Validate(_source);
            for (int i = 0; i < diagnostics.Count; i++)
            {
                ConvaiActionConfigDiagnostic diagnostic = diagnostics[i];
                MessageType messageType = diagnostic.Severity == ConvaiActionConfigDiagnosticSeverity.Error
                    ? MessageType.Error
                    : diagnostic.Severity == ConvaiActionConfigDiagnosticSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox($"{diagnostic.Context}: {diagnostic.Message}", messageType);
            }

            IReadOnlyList<ConvaiActionDefinition> definitions = _source.Definitions;
            if (definitions == null || definitions.Count == 0)
            {
                EditorGUILayout.HelpBox("No action templates authored.", MessageType.Info);
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                ConvaiActionDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.ActionName))
                    continue;

                EditorGUILayout.SelectableLabel(definition.ToActionConfigString(), GUILayout.Height(18f));
                EditorGUILayout.LabelField("Effective failure policy", FormatEffectiveFailurePolicy(definition));
            }
        }

        private void DrawInjectionControls()
        {
            EditorGUILayout.LabelField("Local injection", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_dispatcher == null))
            {
                _manualActionName = EditorGUILayout.TextField("Action name", _manualActionName);
                _manualTarget = EditorGUILayout.TextField("Target / parameters", _manualTarget);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_manualActionName)))
                    {
                        if (GUILayout.Button("Inject"))
                            Inject(_manualActionName, _manualTarget);

                        if (GUILayout.Button("Inject → first authored object"))
                            InjectFirstObject(_manualActionName);
                    }
                }

                IReadOnlyList<ConvaiActionDefinition> definitions = _source?.Definitions;
                if (definitions != null && definitions.Count > 0)
                {
                    EditorGUILayout.LabelField("Authored actions", EditorStyles.miniBoldLabel);
                    DrawWrappedButtonGrid(
                        definitions,
                        buttonsPerRow: 4,
                        definition => string.IsNullOrWhiteSpace(definition?.ActionName) ? null : definition.ActionName,
                        definition => InjectFirstObject(definition.ActionName));
                }
            }
        }

        private void DrawPresetProviders()
        {
            IReadOnlyList<IConvaiActionDebugPresetProvider> providers = ConvaiActionDebugPresetRegistry.Providers;
            if (providers.Count == 0)
                return;

            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            for (int providerIndex = 0; providerIndex < providers.Count; providerIndex++)
            {
                IConvaiActionDebugPresetProvider provider = providers[providerIndex];
                if (provider == null)
                    continue;

                EditorGUILayout.LabelField(provider.DisplayName ?? "Presets", EditorStyles.miniBoldLabel);

                bool canApplyTemplates = _source != null && !UnityEngine.Application.isPlaying;
                using (new EditorGUI.DisabledScope(!canApplyTemplates))
                {
                    if (GUILayout.Button($"Apply {provider.DisplayName} templates"))
                        ApplyTemplates(provider);
                }

                IReadOnlyList<ConvaiActionDebugInjectionPreset> presets = provider.GetInjectionPresets();
                if (presets == null || presets.Count == 0)
                    continue;

                using (new EditorGUI.DisabledScope(_dispatcher == null))
                {
                    DrawWrappedButtonGrid(
                        presets,
                        buttonsPerRow: 3,
                        preset => string.IsNullOrWhiteSpace(preset?.ActionName) ? null : preset.Label ?? preset.ActionName,
                        preset => Inject(preset.ActionName, preset.Target));
                }
            }

            if (UnityEngine.Application.isPlaying)
                EditorGUILayout.HelpBox("Template authoring is disabled during Play Mode.", MessageType.Info);
        }

        /// <summary>
        ///     Draws one button per item, wrapping to a new row every <paramref name="buttonsPerRow" />
        ///     buttons. Items whose <paramref name="label" /> resolves to null/whitespace are skipped.
        ///     Uses explicit Begin/EndHorizontal throughout so row breaks never fight a RAII scope.
        /// </summary>
        private static void DrawWrappedButtonGrid<T>(
            IReadOnlyList<T> items,
            int buttonsPerRow,
            Func<T, string> label,
            Action<T> onClick)
        {
            EditorGUILayout.BeginHorizontal();
            int drawn = 0;
            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                string text = label(item);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (drawn > 0 && drawn % buttonsPerRow == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }

                if (GUILayout.Button(text))
                    onClick(item);

                drawn++;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEventFeed()
        {
            EditorGUILayout.LabelField("Runtime feed", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(
                _scroll,
                GUILayout.MinHeight(180f),
                GUILayout.MaxHeight(300f));
            for (int i = 0; i < _events.Count; i++)
            {
                ActionDebugEvent entry = _events[i];
                EditorGUILayout.LabelField($"{entry.Time:HH:mm:ss.fff} {entry.Kind}", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(entry.Payload, GUILayout.MinHeight(44f));
            }

            if (_autoScroll && Event.current.type == EventType.Repaint)
                _scroll.y = float.MaxValue;

            EditorGUILayout.EndScrollView();
        }

        private void AutoResolve(bool force = false)
        {
            if (force)
            {
                _character = null;
                _source = null;
                _dispatcher = null;
            }

            if (_character == null)
                _character = FindAnyObjectByType<ConvaiCharacter>();

            if (_source == null)
                _source = _character != null
                    ? _character.GetComponent<ConvaiActionConfigSource>()
                    : FindAnyObjectByType<ConvaiActionConfigSource>();

            if (_dispatcher == null)
                _dispatcher = _character != null
                    ? _character.GetComponent<ConvaiActionDispatcher>()
                    : FindAnyObjectByType<ConvaiActionDispatcher>();
        }

        private void Subscribe()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                UnsubscribeRuntimeDiagnostics();
                return;
            }

            SubscribeRuntimeDiagnostics();

            if (_subscribedCharacter != _character)
            {
                if (_subscribedCharacter != null)
                    _subscribedCharacter.OnActionsReceived -= HandleActionsReceived;

                _subscribedCharacter = _character;
                HandleRuntimeCharacterSelectionChanged();
                if (_subscribedCharacter != null)
                    _subscribedCharacter.OnActionsReceived += HandleActionsReceived;
            }

            if (_subscribedDispatcher != _dispatcher)
            {
                UnsubscribeDispatcher();
                _subscribedDispatcher = _dispatcher;
                if (_subscribedDispatcher != null)
                {
                    _subscribedDispatcher.OnStepStarted.AddListener(HandleStepStarted);
                    _subscribedDispatcher.OnStepCompleted.AddListener(HandleStepCompleted);
                    _subscribedDispatcher.OnBatchAborted.AddListener(HandleBatchAborted);
                }
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedCharacter != null)
                _subscribedCharacter.OnActionsReceived -= HandleActionsReceived;

            _subscribedCharacter = null;
            UnsubscribeDispatcher();
            UnsubscribeRuntimeDiagnostics();
        }

        private void UnsubscribeDispatcher()
        {
            if (_subscribedDispatcher == null)
                return;

            _subscribedDispatcher.OnStepStarted.RemoveListener(HandleStepStarted);
            _subscribedDispatcher.OnStepCompleted.RemoveListener(HandleStepCompleted);
            _subscribedDispatcher.OnBatchAborted.RemoveListener(HandleBatchAborted);
            _subscribedDispatcher = null;
        }

        private void InjectFirstObject(string actionName)
        {
            IReadOnlyList<ConvaiActionObjectDefinition> objects = _source?.Objects;
            string target = objects != null && objects.Count > 0 ? objects[0]?.Name : null;
            Inject(actionName, target);
        }

        private void Inject(string actionName, string target)
        {
            if (_dispatcher == null)
                return;

            ConvaiActionCommand command = new(actionName, target);
            ResolveInjectionContext(out ConvaiActionConfig actionConfig, out IReadOnlyList<ConvaiActionDefinition> definitions);
            if (definitions != null && definitions.Count > 0)
            {
                command = ConvaiActionResponseParser.Enrich(
                    command,
                    actionConfig,
                    definitions);
            }

            _dispatcher.EnqueueActions(new[] { command });
            AddEvent("Injected", FormatBatchJson(new[] { command }));
        }

        private void ResolveInjectionContext(
            out ConvaiActionConfig actionConfig,
            out IReadOnlyList<ConvaiActionDefinition> definitions)
        {
            actionConfig = null;
            definitions = null;

            if (_character != null)
            {
                actionConfig = _character.ActionConfig;
                definitions = _character.ActionDefinitions;
                if (actionConfig != null && definitions != null && definitions.Count > 0)
                    return;
            }

            if (_source == null)
                return;

            actionConfig = _source.BuildActionConfig();
            definitions = _source.Definitions;
        }

        private void ApplyTemplates(IConvaiActionDebugPresetProvider provider)
        {
            if (UnityEngine.Application.isPlaying)
            {
                ConvaiLogger.Warning("Applying templates is edit-mode only.", LogCategory.Editor);
                return;
            }

            if (_source == null)
            {
                ConvaiLogger.Warning("No ConvaiActionConfigSource found.", LogCategory.Editor);
                return;
            }

            IReadOnlyList<ConvaiActionDefinition> templates = provider?.BuildTemplates();
            if (templates == null || templates.Count == 0)
            {
                ConvaiLogger.Warning($"Provider '{provider?.DisplayName}' supplied no templates.",
                    LogCategory.Editor);
                return;
            }

            Undo.RecordObject(_source, $"Apply {provider.DisplayName} Action Templates");
            IReadOnlyList<ConvaiActionDefinition> existing = _source.Definitions;

            var definitions = new List<ConvaiActionDefinition>(templates.Count);
            for (int i = 0; i < templates.Count; i++)
            {
                ConvaiActionDefinition template = templates[i];
                if (template == null || string.IsNullOrWhiteSpace(template.ActionName))
                    continue;

                definitions.Add(MergeWithExisting(template, FindDefinition(existing, template.ActionName)));
            }

            CopyUnknownDefinitions(existing, definitions);
            _source.ReplaceDefinitions(definitions);
            EditorUtility.SetDirty(_source);
            if (_source.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(_source.gameObject.scene);

            AutoResolve(force: true);
            AddEvent("Templates applied", FormatTemplatePreview(definitions));
            ConvaiLogger.Info($"Applied '{provider.DisplayName}' action templates.",
                LogCategory.Editor);
        }

        private static ConvaiActionDefinition MergeWithExisting(
            ConvaiActionDefinition template,
            ConvaiActionDefinition previous)
        {
            ConvaiActionDefinition merged = template.Clone();
            if (previous == null)
                return merged;

            // Preserve scene wiring and authored dispatch tuning across template re-application.
            merged.Executor = previous.Executor != null ? previous.Executor : merged.Executor;
            merged.TimeoutSeconds = previous.TimeoutSeconds > 0f ? previous.TimeoutSeconds : merged.TimeoutSeconds;
            merged.FailurePolicyOverride = previous.FailurePolicyOverride != ConvaiActionFailurePolicyOverride.UseDispatcherDefault
                ? previous.FailurePolicyOverride
                : merged.FailurePolicyOverride;
            merged.WaitForBotSpeech = previous.WaitForBotSpeech || merged.WaitForBotSpeech;
            merged.DelayAfterBotSpeechSeconds = previous.DelayAfterBotSpeechSeconds > 0f
                ? previous.DelayAfterBotSpeechSeconds
                : merged.DelayAfterBotSpeechSeconds;
            return merged;
        }

        private static void CopyUnknownDefinitions(
            IReadOnlyList<ConvaiActionDefinition> existing,
            List<ConvaiActionDefinition> definitions)
        {
            if (existing == null)
                return;

            for (int i = 0; i < existing.Count; i++)
            {
                ConvaiActionDefinition definition = existing[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.ActionName))
                    continue;

                if (FindDefinition(definitions, definition.ActionName) != null)
                    continue;

                definitions.Add(definition.Clone());
            }
        }

        private static ConvaiActionDefinition FindDefinition(
            IReadOnlyList<ConvaiActionDefinition> definitions,
            string actionName)
        {
            if (definitions == null || string.IsNullOrWhiteSpace(actionName))
                return null;

            for (int i = 0; i < definitions.Count; i++)
            {
                ConvaiActionDefinition definition = definitions[i];
                if (string.Equals(
                        definition?.ActionName,
                        actionName,
                        StringComparison.OrdinalIgnoreCase))
                    return definition;
            }

            return null;
        }

        private void HandleActionsReceived(IReadOnlyList<ConvaiActionCommand> actions) =>
            AddEvent("Received", FormatBatchJson(actions));

        private void HandleStepStarted(ConvaiActionInvocation invocation) =>
            AddEvent("Step started", FormatInvocation(invocation));

        private void HandleStepCompleted(ConvaiActionStepReport report) =>
            AddEvent("Step completed", FormatReport(report));

        private void HandleBatchAborted() =>
            AddEvent("Batch aborted", "{}");

        private void AddEvent(string kind, string payload)
        {
            _events.Add(new ActionDebugEvent(DateTime.Now, kind, payload));
            while (_events.Count > MaxEvents)
                _events.RemoveAt(0);
            Repaint();
        }

        private static string FormatInvocation(ConvaiActionInvocation invocation)
        {
            if (invocation == null)
                return "{}";

            var builder = new StringBuilder();
            builder.Append('{');
            AppendJsonProperty(builder, "action", invocation.Definition?.ActionName ?? invocation.Command?.Name, first: true);
            AppendJsonProperty(builder, "target", invocation.ResolvedTarget?.Name ?? invocation.Command?.Target);
            builder.Append(",\"command\":");
            AppendActionJson(builder, invocation.Command);
            builder.Append('}');
            return builder.ToString();
        }

        private static string FormatReport(ConvaiActionStepReport report)
        {
            if (report == null)
                return "{}";

            var builder = new StringBuilder();
            builder.Append('{');
            AppendJsonProperty(builder, "status", report.Result.Status.ToString(), first: true);
            AppendJsonProperty(builder, "message", report.Message);
            AppendJsonProperty(builder, "failure", report.FailureMessage);
            builder.Append(",\"batchAborted\":").Append(report.BatchAborted ? "true" : "false");
            builder.Append(",\"invocation\":").Append(FormatInvocation(report.Invocation));
            builder.Append('}');
            return builder.ToString();
        }

        private string FormatEffectiveFailurePolicy(ConvaiActionDefinition definition)
        {
            if (definition == null)
                return "Unknown";

            return definition.FailurePolicyOverride switch
            {
                ConvaiActionFailurePolicyOverride.StopBatch => "StopBatch (action override)",
                ConvaiActionFailurePolicyOverride.ContinueBatch => "ContinueBatch (action override)",
                _ => $"{(_dispatcher != null ? _dispatcher.FailurePolicy : ConvaiActionBatchFailurePolicy.StopBatch)} (dispatcher default)"
            };
        }

        private static string FormatBatchJson(IReadOnlyList<ConvaiActionCommand> actions)
        {
            if (actions == null)
                return "{\"actions\":null}";

            var builder = new StringBuilder();
            builder.Append("{\"actions\":[");
            for (int i = 0; i < actions.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');

                AppendActionJson(builder, actions[i]);
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private static string FormatTemplatePreview(IReadOnlyList<ConvaiActionDefinition> definitions)
        {
            var builder = new StringBuilder();
            builder.Append("{\"renderedActions\":[");
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    if (i > 0)
                        builder.Append(',');

                    AppendJsonString(builder, definitions[i]?.ToActionConfigString() ?? string.Empty);
                }
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private static void AppendActionJson(StringBuilder builder, ConvaiActionCommand action)
        {
            if (action == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('{');
            AppendJsonProperty(builder, "name", action.Name, first: true);
            AppendJsonProperty(builder, "target", action.Target);
            AppendJsonProperty(builder, "actionString", action.ActionString);
            builder.Append(",\"waitForBotSpeech\":").Append(action.WaitForBotSpeech ? "true" : "false");
            builder.Append(",\"delayAfterBotSpeechSeconds\":")
                .Append(action.DelayAfterBotSpeechSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(",\"parameters\":{");

            bool first = true;
            if (action.Parameters != null)
            {
                foreach (KeyValuePair<string, ConvaiActionParameterValue> pair in action.Parameters)
                {
                    if (!first)
                        builder.Append(',');

                    AppendJsonString(builder, pair.Key);
                    builder.Append(':');
                    AppendParameterJson(builder, pair.Value);
                    first = false;
                }
            }

            builder.Append("}}");
        }

        private static void AppendParameterJson(StringBuilder builder, ConvaiActionParameterValue value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('{');
            AppendJsonProperty(builder, "raw", value.RawValue, first: true);
            AppendJsonProperty(builder, "type", value.Type.ToString());
            AppendJsonProperty(builder, "string", value.StringValue);
            builder.Append(",\"number\":").Append(value.NumberValue.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(",\"bool\":").Append(value.BoolValue ? "true" : "false");
            builder.Append(",\"constraint\":").Append(value.IsConstraintMatch ? "true" : "false");
            if (value.ResolvedReference != null)
            {
                builder.Append(",\"reference\":{");
                AppendJsonProperty(builder, "name", value.ResolvedReference.Name, first: true);
                AppendJsonProperty(builder, "kind", value.ResolvedReference.Kind.ToString());
                builder.Append('}');
            }

            builder.Append('}');
        }

        private static void AppendJsonProperty(
            StringBuilder builder,
            string name,
            string value,
            bool first = false)
        {
            if (!first)
                builder.Append(',');

            AppendJsonString(builder, name);
            builder.Append(':');
            AppendJsonString(builder, value ?? string.Empty);
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    switch (c)
                    {
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            builder.Append(c);
                            break;
                    }
                }
            }

            builder.Append('"');
        }

        private readonly struct ActionDebugEvent
        {
            public ActionDebugEvent(DateTime time, string kind, string payload)
            {
                Time = time;
                Kind = kind;
                Payload = payload;
            }

            public DateTime Time { get; }
            public string Kind { get; }
            public string Payload { get; }
        }
    }

}
