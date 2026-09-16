using System;
using System.Collections.Generic;
using System.Text;
using Convai.Domain.DomainEvents.Runtime;
using Convai.Domain.EventSystem;
using Convai.Runtime;
using Convai.Runtime.Actions;
using Convai.Runtime.Components;
using Convai.Runtime.DynamicContext;
using Convai.Runtime.Facades;
using Convai.Shared.Actions;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.Actions
{
    public sealed partial class ConvaiActionDebugWindow
    {
        private readonly HashSet<string> _observedRuntimeUpdateIds = new(StringComparer.Ordinal);
        private Vector2 _windowScroll;
        private bool _runtimeStateExpanded = true;
        private bool _runtimePatchExpanded = true;
        private ConvaiActionDebugPatchDraft _runtimePatchDraft = new();
        private string _runtimePatchStatus = string.Empty;
        private MessageType _runtimePatchStatusType = MessageType.Info;
        private ConvaiEvents _subscribedEvents;
        private SubscriptionToken _filterDiagnosticToken;
        private DynamicContextUpdateResultReceived _lastActionUpdateAcknowledgement;
        private bool _hasLastActionUpdateAcknowledgement;

        private void DrawRuntimeSessionState()
        {
            _runtimeStateExpanded = EditorGUILayout.Foldout(
                _runtimeStateExpanded,
                "Runtime action state",
                true,
                EditorStyles.foldoutHeader);
            if (!_runtimeStateExpanded)
                return;

            if (!UnityEngine.Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to inspect backend-confirmed action state, pending patches, and ACK metadata.",
                    MessageType.Info);
                return;
            }

            if (_character == null)
            {
                EditorGUILayout.HelpBox("No ConvaiCharacter selected.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(
                "Session",
                _character.IsInConversation ? "Connected and ready" : "Not ready for runtime action updates");

            ConvaiActionConfig confirmed = _character.ActionConfig;
            DrawReadOnlyText(
                "Backend-confirmed snapshot",
                FormatRuntimeSnapshot(
                    confirmed,
                    _character.ActionDefinitions,
                    _character.GetRuntimeActionDefinitionCatalog()),
                8);

            IReadOnlyList<ConvaiRuntimeActionUpdateDebugInfo> pending =
                _character.GetPendingRuntimeActionUpdateDebugInfo();
            EditorGUILayout.LabelField($"Pending runtime updates ({pending.Count})", EditorStyles.miniBoldLabel);
            if (pending.Count == 0)
            {
                EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    ConvaiRuntimeActionUpdateDebugInfo item = pending[i];
                    double ageSeconds = Math.Max(0d, (DateTime.UtcNow - item.SentAtUtc).TotalSeconds);
                    string mutation = item.MutatesActionConfig && item.MutatesTopLevelAttention
                        ? "config + attention"
                        : item.MutatesActionConfig
                            ? "config"
                            : "attention";
                    string ack = item.HasAcknowledgement
                        ? $"ACK received ({item.AcknowledgementStatus})"
                        : "waiting for ACK";
                    EditorGUILayout.LabelField(
                        item.UpdateId,
                        $"{mutation}; {ack}; {ageSeconds:0.0}s",
                        EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.LabelField("Last action-update ACK", EditorStyles.miniBoldLabel);
            if (_hasLastActionUpdateAcknowledgement)
                DrawReadOnlyText(null, FormatActionUpdateAcknowledgement(_lastActionUpdateAcknowledgement), 5);
            else
                EditorGUILayout.LabelField("None observed by this window.", EditorStyles.miniLabel);
        }

        private void DrawRuntimePatchComposer()
        {
            _runtimePatchExpanded = EditorGUILayout.Foldout(
                _runtimePatchExpanded,
                "Runtime patch composer",
                true,
                EditorStyles.foldoutHeader);
            if (!_runtimePatchExpanded)
                return;

            EditorGUILayout.HelpBox(
                "Unchecked field = omit/preserve. Checked field with no values = explicitly clear. " +
                "Confirmed state changes only after a matching successful backend ACK.",
                MessageType.Info);

            _runtimePatchDraft.IncludeActions = EditorGUILayout.ToggleLeft(
                "Include actions replacement",
                _runtimePatchDraft.IncludeActions);
            if (_runtimePatchDraft.IncludeActions)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("One action per line. Empty text clears request-level actions.",
                    EditorStyles.miniLabel);
                _runtimePatchDraft.ActionsText = EditorGUILayout.TextArea(
                    _runtimePatchDraft.ActionsText ?? string.Empty,
                    GUILayout.MinHeight(52f));
                EditorGUI.indentLevel--;
            }

            _runtimePatchDraft.IncludeObjects = EditorGUILayout.ToggleLeft(
                "Include object replacement",
                _runtimePatchDraft.IncludeObjects);
            if (_runtimePatchDraft.IncludeObjects)
                DrawObjectPatchRows();

            _runtimePatchDraft.IncludeCharacters = EditorGUILayout.ToggleLeft(
                "Include character replacement",
                _runtimePatchDraft.IncludeCharacters);
            if (_runtimePatchDraft.IncludeCharacters)
                DrawCharacterPatchRows();

            _runtimePatchDraft.IncludeNestedAttention = EditorGUILayout.ToggleLeft(
                "Include action_config attention",
                _runtimePatchDraft.IncludeNestedAttention);
            if (_runtimePatchDraft.IncludeNestedAttention)
            {
                EditorGUI.indentLevel++;
                _runtimePatchDraft.NestedAttention = EditorGUILayout.TextField(
                    "Attention object",
                    _runtimePatchDraft.NestedAttention ?? string.Empty);
                EditorGUILayout.LabelField("Empty value clears attention.", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            _runtimePatchDraft.IncludeTopLevelAttention = EditorGUILayout.ToggleLeft(
                "Include top-level attention override",
                _runtimePatchDraft.IncludeTopLevelAttention);
            if (_runtimePatchDraft.IncludeTopLevelAttention)
            {
                EditorGUI.indentLevel++;
                _runtimePatchDraft.TopLevelAttention = EditorGUILayout.TextField(
                    "Attention object",
                    _runtimePatchDraft.TopLevelAttention ?? string.Empty);
                EditorGUILayout.LabelField(
                    "Top-level value wins when nested attention is also included. Empty value clears.",
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            _runtimePatchDraft.Reaction = (ConvaiRespondMode)EditorGUILayout.EnumPopup(
                "Reaction",
                _runtimePatchDraft.Reaction);
            _runtimePatchDraft.UpdateId = EditorGUILayout.TextField(
                "Update ID",
                _runtimePatchDraft.UpdateId ?? string.Empty);
            EditorGUILayout.LabelField("Leave blank to generate an action-debug ID.", EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load confirmed"))
                    LoadConfirmedRuntimePatchDraft();

                if (GUILayout.Button("Reset draft"))
                    ResetRuntimePatchDraft();

                using (new EditorGUI.DisabledScope(_character == null || !_runtimePatchDraft.HasMutation))
                {
                    if (GUILayout.Button("Preview"))
                        PreviewRuntimePatch();
                }

                bool canSend = UnityEngine.Application.isPlaying &&
                               _character != null &&
                               _character.IsInConversation &&
                               _runtimePatchDraft.HasMutation;
                using (new EditorGUI.DisabledScope(!canSend))
                {
                    if (GUILayout.Button("Send patch"))
                        SendRuntimePatch();
                }
            }

            if (!string.IsNullOrEmpty(_runtimePatchStatus))
                EditorGUILayout.HelpBox(_runtimePatchStatus, _runtimePatchStatusType);
        }

        private void DrawObjectPatchRows()
        {
            EditorGUI.indentLevel++;
            int removeIndex = -1;
            for (int i = 0; i < _runtimePatchDraft.Objects.Count; i++)
            {
                ConvaiActionObjectDefinition item = _runtimePatchDraft.Objects[i] ??= new ConvaiActionObjectDefinition();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    item.Name = EditorGUILayout.TextField("Name", item.Name ?? string.Empty);
                    item.Description = EditorGUILayout.TextField("Description", item.Description ?? string.Empty);
                    item.GameObjectReference = (GameObject)EditorGUILayout.ObjectField(
                        "GameObject",
                        item.GameObjectReference,
                        typeof(GameObject),
                        true);
                    if (GUILayout.Button("Remove object"))
                        removeIndex = i;
                }
            }

            if (removeIndex >= 0)
                _runtimePatchDraft.Objects.RemoveAt(removeIndex);

            if (GUILayout.Button("Add object"))
                _runtimePatchDraft.Objects.Add(new ConvaiActionObjectDefinition());

            if (_runtimePatchDraft.Objects.Count == 0)
                EditorGUILayout.LabelField("No rows: objects will be cleared.", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        private void DrawCharacterPatchRows()
        {
            EditorGUI.indentLevel++;
            int removeIndex = -1;
            for (int i = 0; i < _runtimePatchDraft.Characters.Count; i++)
            {
                ConvaiActionCharacterDefinition item =
                    _runtimePatchDraft.Characters[i] ??= new ConvaiActionCharacterDefinition();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    item.Name = EditorGUILayout.TextField("Name", item.Name ?? string.Empty);
                    item.Bio = EditorGUILayout.TextField("Bio", item.Bio ?? string.Empty);
                    item.GameObjectReference = (GameObject)EditorGUILayout.ObjectField(
                        "GameObject",
                        item.GameObjectReference,
                        typeof(GameObject),
                        true);
                    if (GUILayout.Button("Remove character"))
                        removeIndex = i;
                }
            }

            if (removeIndex >= 0)
                _runtimePatchDraft.Characters.RemoveAt(removeIndex);

            if (GUILayout.Button("Add character"))
                _runtimePatchDraft.Characters.Add(new ConvaiActionCharacterDefinition());

            if (_runtimePatchDraft.Characters.Count == 0)
                EditorGUILayout.LabelField("No rows: characters will be cleared.", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        private void LoadConfirmedRuntimePatchDraft()
        {
            ConvaiActionConfig config = _character?.ActionConfig ?? _source?.BuildActionConfig();
            if (config == null)
            {
                SetRuntimePatchStatus("No action config available to load.", MessageType.Warning);
                return;
            }

            _runtimePatchDraft.Load(config);
            SetRuntimePatchStatus(
                "Loaded current snapshot. Every loaded field is included as a replacement; uncheck fields to preserve them.",
                MessageType.Info);
        }

        private void ResetRuntimePatchDraft()
        {
            _runtimePatchDraft = new ConvaiActionDebugPatchDraft();
            SetRuntimePatchStatus("Draft reset. All fields omitted.", MessageType.Info);
        }

        private void PreviewRuntimePatch()
        {
            if (!TryPreviewRuntimePatch(out _, out _, out ConvaiActionConfig predicted, out string error))
            {
                SetRuntimePatchStatus($"Patch rejected locally: {error}", MessageType.Error);
                return;
            }

            SetRuntimePatchStatus(
                "Valid local preview. " + FormatPredictedSnapshot(predicted),
                MessageType.Info);
        }

        private void SendRuntimePatch()
        {
            if (_character == null || !_character.IsInConversation)
            {
                SetRuntimePatchStatus("Character must be connected and ready before sending.", MessageType.Warning);
                return;
            }

            if (!TryPreviewRuntimePatch(
                    out ConvaiActionConfigPatch patch,
                    out object topLevelAttention,
                    out ConvaiActionConfig predicted,
                    out string error))
            {
                SetRuntimePatchStatus($"Patch rejected locally: {error}", MessageType.Error);
                return;
            }

            string updateId = string.IsNullOrWhiteSpace(_runtimePatchDraft.UpdateId)
                ? $"action-debug-{Guid.NewGuid():N}"
                : _runtimePatchDraft.UpdateId.Trim();
            _runtimePatchDraft.UpdateId = updateId;
            _observedRuntimeUpdateIds.Add(updateId);

            _character.DynamicContext.Apply(new ConvaiDynamicContextUpdate(
                text: null,
                reaction: _runtimePatchDraft.Reaction,
                currentAttentionObject: topLevelAttention,
                updateId: updateId,
                actionConfig: patch));

            IReadOnlyList<ConvaiRuntimeActionUpdateDebugInfo> pending =
                _character.GetPendingRuntimeActionUpdateDebugInfo();
            bool queued = false;
            for (int i = 0; i < pending.Count; i++)
            {
                if (!string.Equals(pending[i].UpdateId, updateId, StringComparison.Ordinal))
                    continue;

                queued = true;
                break;
            }

            if (!queued)
            {
                SetRuntimePatchStatus(
                    $"Update {updateId} was not queued. Inspect Console transport warnings.",
                    MessageType.Error);
                return;
            }

            string summary = FormatPredictedSnapshot(predicted);
            SetRuntimePatchStatus(
                $"Pending ACK: {updateId}. Confirmed ActionConfig remains unchanged until commit. {summary}",
                MessageType.Info);
            AddEvent("Runtime patch queued", $"update_id={updateId}; {summary}");
        }

        private bool TryPreviewRuntimePatch(
            out ConvaiActionConfigPatch patch,
            out object topLevelAttention,
            out ConvaiActionConfig predicted,
            out string error)
        {
            patch = _runtimePatchDraft.BuildActionConfigPatch();
            topLevelAttention = _runtimePatchDraft.BuildTopLevelAttention();
            predicted = null;
            if (!_runtimePatchDraft.HasMutation)
            {
                error = "select at least one patch or attention field";
                return false;
            }

            if (_character == null)
            {
                error = "no ConvaiCharacter selected";
                return false;
            }

            return _character.TryPreviewRuntimeActionStateUpdate(
                patch,
                topLevelAttention,
                out predicted,
                out error);
        }

        private void SubscribeRuntimeDiagnostics()
        {
            ConvaiManager manager = ConvaiManager.ActiveManager;
            ConvaiEvents next = null;
            if (manager != null && manager.IsInitialized)
                next = manager.Events;

            if (ReferenceEquals(_subscribedEvents, next))
                return;

            UnsubscribeRuntimeDiagnostics();
            _subscribedEvents = next;
            if (_subscribedEvents == null)
                return;

            _subscribedEvents.OnDynamicContextUpdateResultReceived += HandleDynamicContextUpdateResult;
            _filterDiagnosticToken = _subscribedEvents.Raw.Subscribe<ConvaiActionResponseFilterDiagnostic>(
                HandleActionResponseFilterDiagnostic);
        }

        private void UnsubscribeRuntimeDiagnostics()
        {
            if (_subscribedEvents == null)
                return;

            _subscribedEvents.OnDynamicContextUpdateResultReceived -= HandleDynamicContextUpdateResult;
            if (_filterDiagnosticToken != default)
                _subscribedEvents.Raw.Unsubscribe(_filterDiagnosticToken);

            _filterDiagnosticToken = default;
            _subscribedEvents = null;
        }

        private void CapturePendingRuntimeUpdateIds()
        {
            if (!UnityEngine.Application.isPlaying || _character == null)
                return;

            IReadOnlyList<ConvaiRuntimeActionUpdateDebugInfo> pending =
                _character.GetPendingRuntimeActionUpdateDebugInfo();
            for (int i = 0; i < pending.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(pending[i].UpdateId))
                    _observedRuntimeUpdateIds.Add(pending[i].UpdateId);
            }
        }

        private void HandleDynamicContextUpdateResult(DynamicContextUpdateResultReceived result)
        {
            bool knownUpdate = !string.IsNullOrWhiteSpace(result.UpdateId) &&
                               _observedRuntimeUpdateIds.Contains(result.UpdateId);
            bool hasActionMetadata = result.ActionConfigUpdated.HasValue ||
                                     result.ActionConfigCreated.HasValue ||
                                     result.ActionsCount.HasValue ||
                                     result.ObjectsCount.HasValue ||
                                     result.CharactersCount.HasValue ||
                                     result.ActionGenerationStrategyChanged.HasValue ||
                                     !string.IsNullOrWhiteSpace(result.ActionGenerationStrategyStatus);
            if (!knownUpdate && !hasActionMetadata)
                return;

            _lastActionUpdateAcknowledgement = result;
            _hasLastActionUpdateAcknowledgement = true;
            AddEvent("Runtime action ACK", FormatActionUpdateAcknowledgement(result));
        }

        private void HandleActionResponseFilterDiagnostic(ConvaiActionResponseFilterDiagnostic diagnostic)
        {
            if (_character != null &&
                !string.IsNullOrWhiteSpace(_character.CharacterId) &&
                !string.Equals(_character.CharacterId, diagnostic.CharacterId, StringComparison.Ordinal))
                return;

            AddEvent("Action filter", FormatFilterDiagnostic(diagnostic));
        }

        private void SetRuntimePatchStatus(string message, MessageType type)
        {
            _runtimePatchStatus = message ?? string.Empty;
            _runtimePatchStatusType = type;
            Repaint();
        }

        private void ClearRuntimeDiagnosticState()
        {
            _observedRuntimeUpdateIds.Clear();
            _lastActionUpdateAcknowledgement = default;
            _hasLastActionUpdateAcknowledgement = false;
            _runtimePatchStatus = string.Empty;
        }

        private void HandleRuntimeCharacterSelectionChanged()
        {
            ClearRuntimeDiagnosticState();
            _runtimePatchDraft = new ConvaiActionDebugPatchDraft();
        }

        private static void DrawReadOnlyText(string label, string value, int minimumLines)
        {
            if (!string.IsNullOrEmpty(label))
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

            int lineCount = 1;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    if (value[i] == '\n') lineCount++;
                }
            }

            float height = Mathf.Clamp(Math.Max(minimumLines, lineCount) * 16f + 8f, 48f, 220f);
            EditorGUILayout.SelectableLabel(
                value ?? string.Empty,
                EditorStyles.textArea,
                GUILayout.Height(height));
        }

        private static string FormatRuntimeSnapshot(
            ConvaiActionConfig config,
            IReadOnlyList<ConvaiActionDefinition> activeDefinitions,
            IReadOnlyList<ConvaiActionDefinition> catalog)
        {
            if (config == null)
                return "No confirmed action config.";

            var builder = new StringBuilder();
            builder.Append("actions (").Append(config.Actions?.Count ?? 0).Append("): ")
                .Append(JoinActions(config.Actions)).AppendLine();
            builder.Append("objects (").Append(config.Objects?.Count ?? 0).AppendLine("):");
            AppendObjectTargets(builder, config.Objects);
            builder.Append("characters (").Append(config.Characters?.Count ?? 0).AppendLine("):");
            AppendCharacterTargets(builder, config.Characters);
            builder.Append("attention: ")
                .Append(string.IsNullOrWhiteSpace(config.CurrentAttentionObject)
                    ? "<none>"
                    : config.CurrentAttentionObject)
                .AppendLine();
            builder.Append("active definitions: ").Append(activeDefinitions?.Count ?? 0).AppendLine();
            builder.Append("executable catalog: ").Append(catalog?.Count ?? 0);
            return builder.ToString();
        }

        private static void AppendObjectTargets(
            StringBuilder builder,
            IReadOnlyList<ConvaiActionObjectDefinition> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                ConvaiActionObjectDefinition target = targets[i];
                builder.Append("  ").Append(target?.Name ?? "<blank>").Append(" -> ")
                    .Append(FormatGameObjectBinding(target?.GameObjectReference)).AppendLine();
            }
        }

        private static void AppendCharacterTargets(
            StringBuilder builder,
            IReadOnlyList<ConvaiActionCharacterDefinition> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                ConvaiActionCharacterDefinition target = targets[i];
                builder.Append("  ").Append(target?.Name ?? "<blank>").Append(" -> ")
                    .Append(FormatGameObjectBinding(target?.GameObjectReference)).AppendLine();
            }
        }

        private static string FormatGameObjectBinding(GameObject gameObject) =>
            gameObject == null
                ? "UNBOUND"
                : gameObject.name;

        private static string JoinActions(IReadOnlyList<string> actions)
        {
            if (actions == null || actions.Count == 0)
                return "<none>";

            var builder = new StringBuilder();
            for (int i = 0; i < actions.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(actions[i]);
            }

            return builder.ToString();
        }

        private static string FormatPredictedSnapshot(ConvaiActionConfig predicted) =>
            predicted == null
                ? "No predicted config."
                : $"Predicted actions={predicted.Actions?.Count ?? 0}, " +
                  $"objects={predicted.Objects?.Count ?? 0}, " +
                  $"characters={predicted.Characters?.Count ?? 0}, " +
                  $"attention={predicted.CurrentAttentionObject ?? "<none>"}.";

        private static string FormatActionUpdateAcknowledgement(
            DynamicContextUpdateResultReceived acknowledgement) =>
            $"update_id={acknowledgement.UpdateId}\n" +
            $"status={acknowledgement.Status}; action_config_updated={FormatNullable(acknowledgement.ActionConfigUpdated)}; " +
            $"action_config_created={FormatNullable(acknowledgement.ActionConfigCreated)}\n" +
            $"counts: actions={FormatNullable(acknowledgement.ActionsCount)}, " +
            $"objects={FormatNullable(acknowledgement.ObjectsCount)}, " +
            $"characters={FormatNullable(acknowledgement.CharactersCount)}\n" +
            $"attention={acknowledgement.CurrentAttentionObject ?? "<none>"}; " +
            $"cleared={FormatNullable(acknowledgement.CurrentAttentionObjectCleared)}\n" +
            $"generation_strategy_changed={FormatNullable(acknowledgement.ActionGenerationStrategyChanged)}; " +
            $"generation_strategy_status={acknowledgement.ActionGenerationStrategyStatus ?? "<none>"}; " +
            $"prompt_rebuild={acknowledgement.PromptRebuildStatus ?? "<none>"}";

        private static string FormatFilterDiagnostic(ConvaiActionResponseFilterDiagnostic diagnostic)
        {
            var builder = new StringBuilder();
            builder.Append("character_id=").Append(diagnostic.CharacterId)
                .Append("; participant_id=").Append(diagnostic.ParticipantId)
                .Append("; received=").Append(diagnostic.ReceivedCount)
                .Append("; accepted=").Append(diagnostic.AcceptedCount)
                .Append("; rejected=").Append(diagnostic.RejectedCount)
                .Append("; reasons=");

            if (diagnostic.RejectedByReason == null || diagnostic.RejectedByReason.Count == 0)
            {
                builder.Append("none");
                return builder.ToString();
            }

            var keys = new List<string>(diagnostic.RejectedByReason.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) builder.Append(',');
                string key = keys[i];
                builder.Append(key).Append(':').Append(diagnostic.RejectedByReason[key]);
            }

            return builder.ToString();
        }

        private static string FormatNullable(bool? value) =>
            value.HasValue ? (value.Value ? "true" : "false") : "<missing>";

        private static string FormatNullable(int? value) =>
            value.HasValue ? value.Value.ToString() : "<missing>";

    }

    internal sealed class ConvaiActionDebugPatchDraft
    {
        public bool IncludeActions { get; set; }
        public string ActionsText { get; set; } = string.Empty;
        public bool IncludeObjects { get; set; }
        public List<ConvaiActionObjectDefinition> Objects { get; } = new();
        public bool IncludeCharacters { get; set; }
        public List<ConvaiActionCharacterDefinition> Characters { get; } = new();
        public bool IncludeNestedAttention { get; set; }
        public string NestedAttention { get; set; } = string.Empty;
        public bool IncludeTopLevelAttention { get; set; }
        public string TopLevelAttention { get; set; } = string.Empty;
        public ConvaiRespondMode Reaction { get; set; } = ConvaiRespondMode.Silent;
        public string UpdateId { get; set; } = string.Empty;

        public bool HasMutation =>
            IncludeActions ||
            IncludeObjects ||
            IncludeCharacters ||
            IncludeNestedAttention ||
            IncludeTopLevelAttention;

        public ConvaiActionConfigPatch BuildActionConfigPatch()
        {
            if (!IncludeActions && !IncludeObjects && !IncludeCharacters && !IncludeNestedAttention)
                return null;

            return new ConvaiActionConfigPatch
            {
                Actions = IncludeActions ? ParseActionLines(ActionsText) : null,
                Objects = IncludeObjects ? CloneObjects(Objects) : null,
                Characters = IncludeCharacters ? CloneCharacters(Characters) : null,
                CurrentAttentionObject = IncludeNestedAttention ? NestedAttention ?? string.Empty : null
            };
        }

        public object BuildTopLevelAttention() =>
            IncludeTopLevelAttention ? TopLevelAttention ?? string.Empty : null;

        public void Load(ConvaiActionConfig config)
        {
            config ??= new ConvaiActionConfig();
            IncludeActions = true;
            ActionsText = config.Actions == null ? string.Empty : string.Join("\n", config.Actions);
            IncludeObjects = true;
            Objects.Clear();
            Objects.AddRange(CloneObjects(config.Objects));
            IncludeCharacters = true;
            Characters.Clear();
            Characters.AddRange(CloneCharacters(config.Characters));
            IncludeNestedAttention = true;
            NestedAttention = config.CurrentAttentionObject ?? string.Empty;
            IncludeTopLevelAttention = false;
            TopLevelAttention = string.Empty;
        }

        internal static List<string> ParseActionLines(string text)
        {
            var actions = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return actions;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string action = lines[i]?.Trim();
                if (!string.IsNullOrEmpty(action))
                    actions.Add(action);
            }

            return actions;
        }

        private static List<ConvaiActionObjectDefinition> CloneObjects(
            IReadOnlyList<ConvaiActionObjectDefinition> source)
        {
            var clone = new List<ConvaiActionObjectDefinition>(source?.Count ?? 0);
            if (source == null)
                return clone;

            for (int i = 0; i < source.Count; i++)
                clone.Add(source[i]?.Clone());
            return clone;
        }

        private static List<ConvaiActionCharacterDefinition> CloneCharacters(
            IReadOnlyList<ConvaiActionCharacterDefinition> source)
        {
            var clone = new List<ConvaiActionCharacterDefinition>(source?.Count ?? 0);
            if (source == null)
                return clone;

            for (int i = 0; i < source.Count; i++)
                clone.Add(source[i]?.Clone());
            return clone;
        }
    }
}
