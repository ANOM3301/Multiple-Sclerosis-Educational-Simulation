using System;
using System.Collections.Generic;

namespace Convai.Shared.Types
{
    /// <summary>How an authored action parameter's raw text is coerced during enrichment.</summary>
    public enum ConvaiActionParameterType
    {
        /// <summary>Infer reference, number, bool, or string best-effort (in that order).</summary>
        Auto = 0,

        /// <summary>Resolve an authored object or character target by name.</summary>
        Reference = 1,

        /// <summary>Keep the raw text.</summary>
        String = 2,

        /// <summary>Parse an invariant-culture float.</summary>
        Number = 3,

        /// <summary>Parse true/yes/1 or false/no/0.</summary>
        Bool = 4,

        /// <summary>Require one of the authored choice strings; mismatch flags the value.</summary>
        Choice = 5
    }

    /// <summary>
    ///     Shared string hygiene for action names, targets, and parameter keys:
    ///     null/whitespace collapses to <see cref="string.Empty" />, everything else is trimmed.
    /// </summary>
    internal static class ConvaiActionText
    {
        internal static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    /// <summary>
    ///     Kind of scene entity an action target resolved to.
    /// </summary>
    public enum ConvaiActionTargetKind
    {
        /// <summary>No target resolved.</summary>
        None = 0,

        /// <summary>An authored action object.</summary>
        Object = 1,

        /// <summary>An authored action character.</summary>
        Character = 2
    }

    /// <summary>
    ///     Name-and-kind handle a Reference parameter resolved to during enrichment. It is a lookup
    ///     key, not a scene binding: resolve it to live objects through
    ///     <c>ConvaiActionInvocation.GetReference(name)</c>.
    /// </summary>
    [Serializable]
    public sealed class ConvaiActionParameterReference
    {
        /// <summary>Authored target name the raw value matched (trimmed, never null).</summary>
        public string Name { get; set; }

        /// <summary>Whether the name matched an authored object or character.</summary>
        public ConvaiActionTargetKind Kind { get; set; }

        public ConvaiActionParameterReference()
        {
        }

        public ConvaiActionParameterReference(string name, ConvaiActionTargetKind kind = ConvaiActionTargetKind.None)
        {
            Name = ConvaiActionText.Normalize(name);
            Kind = kind;
        }

        public ConvaiActionParameterReference Clone() =>
            new(Name, Kind);
    }

    /// <summary>
    ///     One typed parameter after enrichment. All representations are populated best-effort
    ///     from the raw text; <see cref="Type" /> says which one the authored template intends.
    /// </summary>
    [Serializable]
    public sealed class ConvaiActionParameterValue
    {
        /// <summary>Effective type after coercion (an authored Auto resolves to a concrete type).</summary>
        public ConvaiActionParameterType Type { get; set; } = ConvaiActionParameterType.String;

        /// <summary>Trimmed raw text this value was parsed from.</summary>
        public string RawValue { get; set; }

        /// <summary>The value as text (same as <see cref="RawValue" /> after trimming).</summary>
        public string StringValue { get; set; }

        /// <summary>Parsed float, or 0 when the text is not numeric.</summary>
        public float NumberValue { get; set; }

        /// <summary>Parsed bool, or false when the text is not a recognized boolean.</summary>
        public bool BoolValue { get; set; }

        /// <summary>Matched authored target when the text named one; null otherwise.</summary>
        public ConvaiActionParameterReference ResolvedReference { get; set; }

        /// <summary>False only when a Choice parameter's text is not one of its authored choices.</summary>
        public bool IsConstraintMatch { get; set; } = true;

        public ConvaiActionParameterValue()
        {
        }

        /// <summary>Creates a deep copy of this parameter value.</summary>
        public ConvaiActionParameterValue Clone() =>
            new()
            {
                Type = Type,
                RawValue = RawValue,
                StringValue = StringValue,
                NumberValue = NumberValue,
                BoolValue = BoolValue,
                ResolvedReference = ResolvedReference?.Clone(),
                IsConstraintMatch = IsConstraintMatch
            };
    }

    /// <summary>
    ///     Structured action command returned by the backend for the current turn.
    /// </summary>
    [Serializable]
    public sealed class ConvaiActionCommand
    {
        /// <summary>Required action name selected by the backend.</summary>
        public string Name { get; set; }

        /// <summary>Optional object or character target name resolved by the backend.</summary>
        public string Target { get; set; }

        /// <summary>Raw action string reconstructed from backend name and target.</summary>
        public string ActionString { get; set; }

        /// <summary>Typed parameters parsed from the backend response and active Unity template.</summary>
        public Dictionary<string, ConvaiActionParameterValue> Parameters { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether the first action in a fresh batch should wait for character speech.</summary>
        public bool WaitForBotSpeech { get; set; }

        /// <summary>Optional delay after the speech gate releases.</summary>
        public float DelayAfterBotSpeechSeconds { get; set; }

        /// <summary>
        ///     True once the command has been enriched against the active action templates.
        ///     The dispatcher enriches unmarked commands exactly once before dispatch.
        /// </summary>
        public bool Enriched { get; set; }

        /// <summary>Returns true when the command includes a target reference.</summary>
        public bool HasTarget => !string.IsNullOrWhiteSpace(Target);

        public ConvaiActionCommand()
        {
        }

        public ConvaiActionCommand(string name, string target = null)
        {
            Name = ConvaiActionText.Normalize(name);
            Target = ConvaiActionText.Normalize(target);
            ActionString = HasTarget ? $"{Name} {Target}".Trim() : Name;
        }

        /// <summary>
        ///     Deep-clones a batch, replacing null entries with empty commands. Each layer of the
        ///     receive→dispatch path snapshots the batch it was handed because every entry point is
        ///     public and callers may keep mutating their list.
        /// </summary>
        internal static IReadOnlyList<ConvaiActionCommand> CloneBatch(IReadOnlyList<ConvaiActionCommand> actions)
        {
            if (actions == null || actions.Count == 0)
                return Array.Empty<ConvaiActionCommand>();

            var clone = new ConvaiActionCommand[actions.Count];
            for (int i = 0; i < actions.Count; i++)
                clone[i] = actions[i]?.Clone() ?? new ConvaiActionCommand();
            return clone;
        }

        /// <summary>Creates a normalized copy of this action command.</summary>
        public ConvaiActionCommand Clone() =>
            new(ConvaiActionText.Normalize(Name), ConvaiActionText.Normalize(Target))
            {
                ActionString = ConvaiActionText.Normalize(ActionString),
                Parameters = CloneParameters(Parameters),
                WaitForBotSpeech = WaitForBotSpeech,
                DelayAfterBotSpeechSeconds = DelayAfterBotSpeechSeconds,
                Enriched = Enriched
            };

        /// <inheritdoc />
        public override string ToString() => HasTarget ? $"{ConvaiActionText.Normalize(Name)} {ConvaiActionText.Normalize(Target)}" : ConvaiActionText.Normalize(Name);

        private static Dictionary<string, ConvaiActionParameterValue> CloneParameters(
            Dictionary<string, ConvaiActionParameterValue> parameters)
        {
            var clone = new Dictionary<string, ConvaiActionParameterValue>(StringComparer.OrdinalIgnoreCase);
            if (parameters == null)
                return clone;

            foreach (KeyValuePair<string, ConvaiActionParameterValue> pair in parameters)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    continue;

                clone[ConvaiActionText.Normalize(pair.Key)] = pair.Value?.Clone() ?? new ConvaiActionParameterValue();
            }

            return clone;
        }
    }
}
