using System;
using System.Collections.Generic;
using Convai.Shared.Types;
using UnityEngine;

namespace Convai.Runtime.Actions
{
    /// <summary>
    ///     Target constraint an action definition imposes on incoming commands.
    /// </summary>
    public enum ConvaiActionTargetRequirement
    {
        /// <summary>The action does not use a target.</summary>
        None = 0,

        /// <summary>The action requires a resolved object target.</summary>
        Object = 1,

        /// <summary>The action requires a resolved character target.</summary>
        Character = 2,

        /// <summary>The action accepts either an object or a character target.</summary>
        Either = 3
    }

    /// <summary>
    ///     Per-action override of the dispatcher-wide batch failure policy.
    /// </summary>
    public enum ConvaiActionFailurePolicyOverride
    {
        /// <summary>Follow the <see cref="ConvaiActionDispatcher" /> failure policy.</summary>
        UseDispatcherDefault = 0,

        /// <summary>A non-success result aborts the remaining batch.</summary>
        StopBatch = 1,

        /// <summary>A non-success result lets the remaining batch continue.</summary>
        ContinueBatch = 2
    }

    /// <summary>
    ///     Authoring definition for a single typed action parameter.
    /// </summary>
    [Serializable]
    public sealed class ConvaiActionParameterDefinition
    {
        /// <summary>Parameter name used as the wire key and template anchor.</summary>
        public string Name;

        /// <summary>Optional description sent to the backend for grounding.</summary>
        public string Description;

        /// <summary>Declared parameter type; <see cref="ConvaiActionParameterType.Auto" /> infers from the value.</summary>
        public ConvaiActionParameterType Type = ConvaiActionParameterType.Auto;

        /// <summary>Optional connector word rendered before the parameter in the wire template (for example "on" or "in").</summary>
        public string Connector;

        /// <summary>Allowed values when <see cref="Type" /> is <see cref="ConvaiActionParameterType.Choice" />.</summary>
        public List<string> Choices = new();

        /// <summary>Creates a normalized copy of this parameter definition.</summary>
        public ConvaiActionParameterDefinition Clone() =>
            new()
            {
                Name = Normalize(Name),
                Description = Normalize(Description),
                Type = Type,
                Connector = Normalize(Connector),
                Choices = Choices == null ? new List<string>() : new List<string>(Choices)
            };

        internal static string Normalize(string value) => ConvaiActionText.Normalize(value);
    }

    /// <summary>
    ///     Authoring definition that binds a backend action name to a local executor,
    ///     its typed parameters, and its dispatch behavior.
    /// </summary>
    [Serializable]
    public sealed class ConvaiActionDefinition
    {
        /// <summary>Canonical action name matched against backend commands (case-insensitive).</summary>
        public string ActionName;

        /// <summary>Optional description sent to the backend for grounding.</summary>
        public string Description;

        /// <summary>Ordered typed parameters rendered into the wire template.</summary>
        public List<ConvaiActionParameterDefinition> Parameters = new();

        /// <summary>Target constraint validated before the executor runs.</summary>
        public ConvaiActionTargetRequirement TargetRequirement;

        /// <summary>Executor component; must implement <see cref="IConvaiActionExecutor" />.</summary>
        public MonoBehaviour Executor;

        /// <summary>Per-step timeout in seconds; zero or less disables the timeout.</summary>
        public float TimeoutSeconds;

        /// <summary>Per-action override of the dispatcher batch failure policy.</summary>
        public ConvaiActionFailurePolicyOverride FailurePolicyOverride;

        /// <summary>Whether the first step of a fresh batch waits for character speech.</summary>
        public bool WaitForBotSpeech;

        /// <summary>Optional delay after the speech gate releases.</summary>
        public float DelayAfterBotSpeechSeconds;

        /// <summary>Creates a normalized deep copy of this definition (executor reference is shared).</summary>
        public ConvaiActionDefinition Clone() =>
            new()
            {
                ActionName = NormalizeActionName(ActionName),
                Description = ConvaiActionParameterDefinition.Normalize(Description),
                Parameters = CloneParameters(Parameters),
                TargetRequirement = TargetRequirement,
                Executor = Executor,
                TimeoutSeconds = TimeoutSeconds,
                FailurePolicyOverride = FailurePolicyOverride,
                WaitForBotSpeech = WaitForBotSpeech,
                DelayAfterBotSpeechSeconds = DelayAfterBotSpeechSeconds
            };

        /// <summary>Renders the wire template string sent to the backend for this definition.</summary>
        public string ToActionConfigString() => ConvaiActionTemplateRenderer.Render(this);

        /// <summary>Creates a normalized deep copy of a definition list.</summary>
        public static List<ConvaiActionDefinition> CloneList(IReadOnlyList<ConvaiActionDefinition> definitions)
        {
            var clone = new List<ConvaiActionDefinition>(definitions?.Count ?? 0);
            if (definitions == null)
                return clone;

            for (int i = 0; i < definitions.Count; i++)
                clone.Add(definitions[i]?.Clone());

            return clone;
        }

        internal static List<ConvaiActionDefinition> FilterAndClone(
            IReadOnlyList<ConvaiActionDefinition> definitions,
            IReadOnlyList<string> allowedActionNames = null,
            Action<string> onDuplicate = null,
            bool requireExecutable = false)
        {
            if (definitions == null || definitions.Count == 0)
                return new List<ConvaiActionDefinition>();

            HashSet<string> allowed = BuildAllowedNameSet(allowedActionNames);
            var filtered = new List<ConvaiActionDefinition>(definitions.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < definitions.Count; i++)
            {
                ConvaiActionDefinition definition = definitions[i];
                string actionName = NormalizeActionName(definition?.ActionName);
                if (string.IsNullOrEmpty(actionName))
                    continue;

                if (!IsActionAllowed(definition, actionName, allowed))
                    continue;

                if (requireExecutable && !ConvaiActionConfigValidator.IsExecutableDefinition(definition))
                    continue;

                if (!seen.Add(actionName))
                {
                    onDuplicate?.Invoke(actionName);
                    continue;
                }

                filtered.Add(definition.Clone());
            }

            return filtered;
        }

        internal static Dictionary<string, ConvaiActionDefinition> BuildLookup(
            IReadOnlyList<ConvaiActionDefinition> definitions)
        {
            var lookup = new Dictionary<string, ConvaiActionDefinition>(StringComparer.OrdinalIgnoreCase);
            if (definitions == null)
                return lookup;

            for (int i = 0; i < definitions.Count; i++)
            {
                ConvaiActionDefinition definition = definitions[i];
                string actionName = NormalizeActionName(definition?.ActionName);
                if (string.IsNullOrEmpty(actionName) || lookup.ContainsKey(actionName))
                    continue;

                lookup[actionName] = definition;
            }

            return lookup;
        }

        internal static string NormalizeActionName(string actionName) => ConvaiActionText.Normalize(actionName);

        private static List<ConvaiActionParameterDefinition> CloneParameters(
            IReadOnlyList<ConvaiActionParameterDefinition> parameters)
        {
            var clone = new List<ConvaiActionParameterDefinition>(parameters?.Count ?? 0);
            if (parameters == null)
                return clone;

            for (int i = 0; i < parameters.Count; i++)
                clone.Add(parameters[i]?.Clone());

            return clone;
        }

        private static HashSet<string> BuildAllowedNameSet(IReadOnlyList<string> allowedActionNames)
        {
            if (allowedActionNames == null || allowedActionNames.Count == 0)
                return null;

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < allowedActionNames.Count; i++)
            {
                string actionString = NormalizeActionName(allowedActionNames[i]);
                if (string.IsNullOrEmpty(actionString))
                    continue;

                allowed.Add(actionString);

                string canonicalName = ExtractCanonicalActionName(actionString);
                if (!string.IsNullOrEmpty(canonicalName))
                    allowed.Add(canonicalName);
            }

            return allowed;
        }

        private static bool IsActionAllowed(
            ConvaiActionDefinition definition,
            string actionName,
            HashSet<string> allowed)
        {
            if (allowed == null)
                return true;

            if (allowed.Contains(actionName))
                return true;

            string rendered = NormalizeActionName(definition?.ToActionConfigString());
            return !string.IsNullOrEmpty(rendered) && allowed.Contains(rendered);
        }

        internal static string ExtractCanonicalActionName(string actionString)
        {
            string value = NormalizeActionName(actionString);
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            int parameterStart = value.IndexOf(" {", StringComparison.Ordinal);
            int descriptionStart = value.IndexOf(" - ", StringComparison.Ordinal);
            int delimiter = -1;

            if (parameterStart >= 0)
                delimiter = parameterStart;

            if (descriptionStart >= 0 && (delimiter < 0 || descriptionStart < delimiter))
                delimiter = descriptionStart;

            return delimiter >= 0 ? NormalizeActionName(value.Substring(0, delimiter)) : value;
        }
    }
}
