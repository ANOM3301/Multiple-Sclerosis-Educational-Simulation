using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Convai.Domain.Logging;
using Convai.Runtime.Logging;
using Convai.Shared.Actions;
using Convai.Shared.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Convai.Runtime.Actions
{
    /// <summary>
    ///     Enriches raw backend action commands into typed parameter sets using the
    ///     character's authored action templates.
    /// </summary>
    internal static class ConvaiActionResponseParser
    {
        /// <summary>Parameter key used for the implicit target parameter when no template matches.</summary>
        internal const string TargetParameterKey = "target";
        internal const string RejectionMalformedEntry = "malformed_entry";
        internal const string RejectionRuntimeSourceUnavailable = "runtime_source_unavailable";
        internal const string RejectionUnknownOrUnexecutableAction = "unknown_or_unexecutable_action";
        internal const string RejectionRequiredTargetUnresolved = "required_target_unresolved";
        internal const string RejectionReferenceParameterUnresolved = "reference_parameter_unresolved";

        private static readonly Regex BraceWrappedRegex =
            new("\\{([^}]*)\\}", RegexOptions.Compiled);

        private static readonly Regex QuotedRegex =
            new("\"([^\"]*)\"|'([^']*)'", RegexOptions.Compiled);

        private static readonly Regex BracketedNoiseRegex =
            new("\\[[^\\]]*\\]", RegexOptions.Compiled);

        /// <summary>
        ///     Returns an enriched copy of <paramref name="command" /> with typed parameters
        ///     split and coerced against the matching action definition template.
        /// </summary>
        public static ConvaiActionCommand Enrich(
            ConvaiActionCommand command,
            ConvaiActionConfig actionConfig,
            IReadOnlyList<ConvaiActionDefinition> definitions)
        {
            if (command == null)
                return new ConvaiActionCommand { Enriched = true };

            ConvaiActionCommand enriched = command.Clone();
            enriched.Enriched = true;
            string rawName = ConvaiActionText.Normalize(command.Name);
            string target = ConvaiActionText.Normalize(command.Target);
            enriched.ActionString = string.IsNullOrEmpty(target) ? rawName : $"{rawName} {target}".Trim();
            enriched.Parameters ??= new Dictionary<string, ConvaiActionParameterValue>(StringComparer.OrdinalIgnoreCase);
            enriched.Parameters.Clear();

            ConvaiActionDefinition definition = FindTemplate(rawName, definitions);
            if (definition == null)
            {
                if (!string.IsNullOrEmpty(target))
                    enriched.Parameters[TargetParameterKey] =
                        Coerce(target, ConvaiActionParameterType.Auto, actionConfig, null);

                return enriched;
            }

            enriched.Name = ConvaiActionDefinition.NormalizeActionName(definition.ActionName);
            enriched.WaitForBotSpeech = definition.WaitForBotSpeech;
            enriched.DelayAfterBotSpeechSeconds = definition.WaitForBotSpeech
                ? Math.Max(0f, definition.DelayAfterBotSpeechSeconds)
                : 0f;

            string nameLeftover = StripTemplatePrefix(rawName, definition);
            string blob = Combine(nameLeftover, target);
            IReadOnlyList<ConvaiActionParameterDefinition> parameters = definition.Parameters;
            if (parameters == null || parameters.Count == 0)
            {
                if (!string.IsNullOrEmpty(blob))
                    enriched.Parameters[TargetParameterKey] =
                        Coerce(blob, ConvaiActionParameterType.Auto, actionConfig, null);

                return enriched;
            }

            List<string> values = SplitParameterValues(blob, parameters);
            for (int i = 0; i < parameters.Count; i++)
            {
                ConvaiActionParameterDefinition parameter = parameters[i];
                string name = ConvaiActionParameterDefinition.Normalize(parameter?.Name);
                if (string.IsNullOrEmpty(name))
                    continue;

                string rawValue = i < values.Count ? StripParamNameMimicry(values[i], name) : string.Empty;
                ConvaiActionParameterValue value = Coerce(rawValue, parameter.Type, actionConfig, parameter);
                enriched.Parameters[name] = value;
                if (parameter.Type == ConvaiActionParameterType.Choice && !value.IsConstraintMatch)
                {
                    ConvaiLogger.Warning(
                        $"Action '{enriched.Name}' parameter '{name}' value '{rawValue}' is not in authored choices.",
                        LogCategory.Character);
                }
            }

            return enriched;
        }

        /// <summary>Enriches every command in a batch; see <see cref="Enrich" />.</summary>
        public static IReadOnlyList<ConvaiActionCommand> EnrichBatch(
            IReadOnlyList<ConvaiActionCommand> commands,
            ConvaiActionConfig actionConfig,
            IReadOnlyList<ConvaiActionDefinition> definitions)
        {
            if (commands == null || commands.Count == 0)
                return Array.Empty<ConvaiActionCommand>();

            var enriched = new ConvaiActionCommand[commands.Count];
            for (int i = 0; i < commands.Count; i++)
                enriched[i] = Enrich(commands[i], actionConfig, definitions);

            return enriched;
        }

        /// <summary>
        ///     Parses valid commands from an action-response payload while counting malformed entries.
        /// </summary>
        public static bool TryParseBatch(
            JObject payload,
            out IReadOnlyList<ConvaiActionCommand> actions,
            out int skippedEntries)
        {
            actions = Array.Empty<ConvaiActionCommand>();
            skippedEntries = 0;

            if (payload?["actions"] is not JArray actionArray)
                return false;

            var parsed = new List<ConvaiActionCommand>(actionArray.Count);
            for (int i = 0; i < actionArray.Count; i++)
            {
                JToken token = actionArray[i];
                if (token == null || token.Type != JTokenType.Object)
                {
                    skippedEntries++;
                    continue;
                }

                ConvaiActionCommand command;
                try
                {
                    command = token.ToObject<ConvaiActionCommand>();
                }
                catch (JsonException)
                {
                    skippedEntries++;
                    continue;
                }

                if (command == null || string.IsNullOrWhiteSpace(command.Name))
                {
                    skippedEntries++;
                    continue;
                }

                parsed.Add(command.Clone());
            }

            actions = parsed;
            return true;
        }

        /// <summary>
        ///     Enriches and filters one backend batch against the Unity-executable catalog and the
        ///     latest backend-confirmed action config. Rejected commands never reach public events.
        /// </summary>
        internal static IReadOnlyList<ConvaiActionCommand> FilterExecutableBatch(
            IReadOnlyList<ConvaiActionCommand> commands,
            ConvaiActionConfig actionConfig,
            IReadOnlyList<ConvaiActionDefinition> definitions,
            IDictionary<string, int> rejectedByReason)
        {
            if (commands == null || commands.Count == 0)
                return Array.Empty<ConvaiActionCommand>();

            var accepted = new List<ConvaiActionCommand>(commands.Count);
            for (int i = 0; i < commands.Count; i++)
            {
                ConvaiActionCommand command = commands[i];
                ConvaiActionDefinition definition = FindTemplate(
                    ConvaiActionText.Normalize(command?.Name),
                    definitions);
                if (!ConvaiActionConfigValidator.IsExecutableDefinition(definition))
                {
                    IncrementReason(rejectedByReason, RejectionUnknownOrUnexecutableAction);
                    continue;
                }

                ConvaiActionCommand enriched = Enrich(command, actionConfig, definitions);
                if (!HasResolvedRequiredTarget(enriched, definition, actionConfig))
                {
                    IncrementReason(rejectedByReason, RejectionRequiredTargetUnresolved);
                    continue;
                }

                if (!HasResolvedReferenceParameters(enriched, definition, actionConfig))
                {
                    IncrementReason(rejectedByReason, RejectionReferenceParameterUnresolved);
                    continue;
                }

                accepted.Add(enriched);
            }

            return accepted;
        }

        internal static ConvaiActionDefinition FindTemplate(
            string rawName,
            IReadOnlyList<ConvaiActionDefinition> definitions)
        {
            if (definitions == null || string.IsNullOrEmpty(rawName))
                return null;

            for (int i = 0; i < definitions.Count; i++)
            {
                ConvaiActionDefinition definition = definitions[i];
                if (string.Equals(rawName, definition?.ActionName, StringComparison.OrdinalIgnoreCase))
                    return definition;
            }

            ConvaiActionDefinition best = null;
            int bestLength = -1;
            for (int i = 0; i < definitions.Count; i++)
            {
                ConvaiActionDefinition definition = definitions[i];
                string name = ConvaiActionDefinition.NormalizeActionName(definition?.ActionName);
                if (name.Length <= bestLength || !StartsWithActionName(rawName, name))
                    continue;

                best = definition;
                bestLength = name.Length;
            }

            return best;
        }

        private static bool HasResolvedRequiredTarget(
            ConvaiActionCommand command,
            ConvaiActionDefinition definition,
            ConvaiActionConfig actionConfig)
        {
            ConvaiActionTargetRequirement requirement = definition.TargetRequirement;
            if (requirement == ConvaiActionTargetRequirement.None)
                return true;

            ConvaiResolvedActionTarget target = ConvaiResolvedActionTarget.Resolve(
                command?.Target,
                actionConfig,
                requirement);
            if (TargetMatchesRequirement(target, requirement))
                return true;

            IReadOnlyList<ConvaiActionParameterDefinition> parameters = definition.Parameters;
            if (parameters == null || command?.Parameters == null)
                return false;

            for (int i = 0; i < parameters.Count; i++)
            {
                ConvaiActionParameterDefinition parameter = parameters[i];
                if (parameter == null ||
                    parameter.Type is not (ConvaiActionParameterType.Auto or ConvaiActionParameterType.Reference))
                    continue;

                string parameterName = ConvaiActionParameterDefinition.Normalize(parameter.Name);
                if (parameterName.Length == 0 ||
                    !command.Parameters.TryGetValue(parameterName, out ConvaiActionParameterValue value))
                    continue;

                target = ConvaiActionTargetReferenceResolver.Resolve(value, actionConfig, requirement);
                if (TargetMatchesRequirement(target, requirement))
                    return true;
            }

            return false;
        }

        private static bool HasResolvedReferenceParameters(
            ConvaiActionCommand command,
            ConvaiActionDefinition definition,
            ConvaiActionConfig actionConfig)
        {
            IReadOnlyList<ConvaiActionParameterDefinition> parameters = definition.Parameters;
            if (parameters == null || parameters.Count == 0)
                return true;

            for (int i = 0; i < parameters.Count; i++)
            {
                ConvaiActionParameterDefinition parameter = parameters[i];
                if (parameter?.Type != ConvaiActionParameterType.Reference)
                    continue;

                string parameterName = ConvaiActionParameterDefinition.Normalize(parameter.Name);
                if (parameterName.Length == 0 ||
                    command?.Parameters == null ||
                    !command.Parameters.TryGetValue(parameterName, out ConvaiActionParameterValue value) ||
                    value?.ResolvedReference == null)
                    return false;

                ConvaiResolvedActionTarget resolved = ConvaiActionTargetReferenceResolver.Resolve(
                    value,
                    actionConfig,
                    definition.TargetRequirement);
                if (resolved?.GameObjectReference == null)
                    return false;
            }

            return true;
        }

        private static bool TargetMatchesRequirement(
            ConvaiResolvedActionTarget target,
            ConvaiActionTargetRequirement requirement)
        {
            if (target?.GameObjectReference == null)
                return false;

            return requirement switch
            {
                ConvaiActionTargetRequirement.None => true,
                ConvaiActionTargetRequirement.Object => target?.Kind == ConvaiActionTargetKind.Object,
                ConvaiActionTargetRequirement.Character => target?.Kind == ConvaiActionTargetKind.Character,
                ConvaiActionTargetRequirement.Either =>
                    target?.Kind is ConvaiActionTargetKind.Object or ConvaiActionTargetKind.Character,
                _ => false
            };
        }

        private static void IncrementReason(IDictionary<string, int> reasons, string reason)
        {
            if (reasons == null)
                return;

            reasons.TryGetValue(reason, out int count);
            reasons[reason] = count + 1;
        }

        private static bool StartsWithActionName(string rawName, string actionName)
        {
            if (string.IsNullOrEmpty(actionName) || rawName.Length < actionName.Length)
                return false;

            if (!rawName.StartsWith(actionName, StringComparison.OrdinalIgnoreCase))
                return false;

            return rawName.Length == actionName.Length ||
                   char.IsWhiteSpace(rawName[actionName.Length]) ||
                   rawName[actionName.Length] == '{';
        }

        private static string StripTemplatePrefix(string rawName, ConvaiActionDefinition definition)
        {
            rawName = ConvaiActionText.Normalize(rawName);
            if (definition == null)
                return rawName;

            string rendered = ConvaiActionText.Normalize(definition.ToActionConfigString());
            if (!string.IsNullOrEmpty(rendered) &&
                rawName.StartsWith(rendered, StringComparison.OrdinalIgnoreCase))
            {
                return rawName.Length == rendered.Length
                    ? string.Empty
                    : rawName.Substring(rendered.Length).Trim();
            }

            return StripActionPrefix(rawName, definition.ActionName);
        }

        private static string StripActionPrefix(string rawName, string actionName)
        {
            rawName = ConvaiActionText.Normalize(rawName);
            actionName = ConvaiActionDefinition.NormalizeActionName(actionName);
            if (!StartsWithActionName(rawName, actionName))
                return rawName;

            return rawName.Substring(actionName.Length).Trim();
        }

        private static string Combine(string first, string second)
        {
            first = ConvaiActionText.Normalize(first);
            second = ConvaiActionText.Normalize(second);
            if (string.IsNullOrEmpty(first)) return second;
            if (string.IsNullOrEmpty(second)) return first;
            return $"{first} {second}";
        }

        private static List<string> SplitParameterValues(
            string blob,
            IReadOnlyList<ConvaiActionParameterDefinition> parameters)
        {
            int expected = parameters?.Count ?? 0;
            var values = new List<string>(expected);
            blob = ConvaiActionText.Normalize(blob);
            if (expected == 0)
                return values;

            values = SplitBraceWrapped(blob);
            if (values.Count > 0)
                return Pad(values, expected);

            values = SplitNamedAnchors(blob, parameters);
            if (values.Count > 0)
                return Pad(values, expected);

            values = SplitByConnectors(blob, parameters);
            if (values.Count > 0)
                return Pad(values, expected);

            values = SplitQuoted(blob);
            if (values.Count > 0)
                return Pad(values, expected);

            return Pad(SplitWhitespace(blob, expected), expected);
        }

        private static List<string> SplitBraceWrapped(string blob)
        {
            var values = new List<string>();
            foreach (Match match in BraceWrappedRegex.Matches(blob))
                values.Add(match.Groups[1].Value.Trim());
            return values;
        }

        private static List<string> SplitNamedAnchors(
            string blob,
            IReadOnlyList<ConvaiActionParameterDefinition> parameters)
        {
            var anchors = new List<(int ParameterIndex, int StartIndex, int ValueIndex)>();
            for (int i = 0; i < parameters.Count; i++)
            {
                string name = ConvaiActionParameterDefinition.Normalize(parameters[i]?.Name);
                if (string.IsNullOrEmpty(name))
                    continue;

                string needle = name + ":";
                int index = CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                    blob,
                    needle,
                    CompareOptions.IgnoreCase);
                if (index < 0)
                    continue;

                anchors.Add((i, index, index + needle.Length));
            }

            if (anchors.Count == 0)
                return new List<string>();

            anchors.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
            var values = new string[parameters.Count];
            for (int i = 0; i < anchors.Count; i++)
            {
                int start = anchors[i].ValueIndex;
                int end = i + 1 < anchors.Count ? anchors[i + 1].StartIndex : blob.Length;
                values[anchors[i].ParameterIndex] = blob.Substring(start, Math.Max(0, end - start)).Trim();
            }

            return new List<string>(values);
        }

        private static List<string> SplitByConnectors(
            string blob,
            IReadOnlyList<ConvaiActionParameterDefinition> parameters)
        {
            if (parameters.Count < 2)
                return new List<string>();

            var values = new List<string>(parameters.Count);
            string remaining = blob;
            for (int i = 1; i < parameters.Count; i++)
            {
                string connector = ConvaiActionParameterDefinition.Normalize(parameters[i]?.Connector);
                if (string.IsNullOrEmpty(connector))
                    return new List<string>();

                string separator = " " + connector + " ";
                int index = CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                    remaining,
                    separator,
                    CompareOptions.IgnoreCase);
                if (index < 0)
                    return new List<string>();

                values.Add(remaining.Substring(0, index).Trim());
                remaining = remaining.Substring(index + separator.Length).Trim();
            }

            values.Add(remaining);
            return values;
        }

        private static List<string> SplitQuoted(string blob)
        {
            var values = new List<string>();
            foreach (Match match in QuotedRegex.Matches(blob))
                values.Add((match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim());
            return values;
        }

        private static List<string> SplitWhitespace(string blob, int expected)
        {
            var values = new List<string>();
            if (string.IsNullOrEmpty(blob))
                return values;

            if (expected <= 1)
            {
                values.Add(blob);
                return values;
            }

            string[] tokens = blob.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length <= expected)
            {
                values.AddRange(tokens);
                return values;
            }

            for (int i = 0; i < expected - 1; i++)
                values.Add(tokens[i]);

            values.Add(string.Join(" ", tokens, expected - 1, tokens.Length - expected + 1));
            return values;
        }

        private static List<string> Pad(List<string> values, int expected)
        {
            values ??= new List<string>();
            while (values.Count < expected)
                values.Add(string.Empty);
            if (values.Count > expected)
                values.RemoveRange(expected, values.Count - expected);
            return values;
        }

        private static string StripParamNameMimicry(string value, string parameterName)
        {
            value = ConvaiActionText.Normalize(value);
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(parameterName))
                return value;

            value = BracketedNoiseRegex.Replace(value, string.Empty).Trim();
            string prefix = parameterName + ":";
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return value.Substring(prefix.Length).Trim();

            return value;
        }

        private static ConvaiActionParameterValue Coerce(
            string rawValue,
            ConvaiActionParameterType declaredType,
            ConvaiActionConfig actionConfig,
            ConvaiActionParameterDefinition definition)
        {
            rawValue = ConvaiActionText.Normalize(rawValue);
            bool hasNumber = float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float number);
            bool hasBool = TryParseBool(rawValue, out bool boolValue);
            ConvaiResolvedActionTarget reference = ConvaiResolvedActionTarget.Resolve(
                rawValue,
                actionConfig,
                (ConvaiActionTargetRequirement?)null);
            bool hasReference = reference != null;
            bool isConstraintMatch = MatchesChoice(rawValue, definition?.Choices);

            ConvaiActionParameterType type = declaredType;
            if (type == ConvaiActionParameterType.Auto)
            {
                if (hasReference) type = ConvaiActionParameterType.Reference;
                else if (hasNumber) type = ConvaiActionParameterType.Number;
                else if (hasBool) type = ConvaiActionParameterType.Bool;
                else type = ConvaiActionParameterType.String;
            }

            return new ConvaiActionParameterValue
            {
                Type = type,
                RawValue = rawValue,
                StringValue = rawValue,
                NumberValue = hasNumber ? number : 0f,
                BoolValue = hasBool && boolValue,
                ResolvedReference = hasReference
                    ? new ConvaiActionParameterReference(reference.Name, reference.Kind)
                    : null,
                IsConstraintMatch = declaredType != ConvaiActionParameterType.Choice || isConstraintMatch
            };
        }

        private static bool TryParseBool(string value, out bool result)
        {
            value = ConvaiActionText.Normalize(value).ToLowerInvariant();
            if (value == "true" || value == "yes" || value == "1")
            {
                result = true;
                return true;
            }

            if (value == "false" || value == "no" || value == "0")
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }

        private static bool MatchesChoice(string value, IReadOnlyList<string> choices)
        {
            if (choices == null || choices.Count == 0)
                return true;

            for (int i = 0; i < choices.Count; i++)
            {
                string choice = ConvaiActionParameterDefinition.Normalize(choices[i]);
                if (string.Equals(value, choice, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
