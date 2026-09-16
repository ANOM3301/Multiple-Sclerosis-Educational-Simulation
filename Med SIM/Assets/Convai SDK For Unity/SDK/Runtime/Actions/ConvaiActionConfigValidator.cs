using System;
using System.Collections.Generic;
using Convai.Runtime.Components;
using Convai.Shared.Actions;
using Convai.Shared.Types;

namespace Convai.Runtime.Actions
{
    /// <summary>How seriously a <see cref="ConvaiActionConfigDiagnostic" /> should be treated.</summary>
    public enum ConvaiActionConfigDiagnosticSeverity
    {
        /// <summary>Advisory only; the config still connects and executes.</summary>
        Info = 0,

        /// <summary>Likely authoring mistake (e.g. missing description); the config still works.</summary>
        Warning = 1,

        /// <summary>The config is broken (e.g. unbound executor) and the flagged part will not work.</summary>
        Error = 2
    }

    /// <summary>
    ///     One authoring finding from <see cref="ConvaiActionConfigValidator.Validate" />, with the
    ///     severity, an actionable message, and the config region it refers to.
    /// </summary>
    public sealed class ConvaiActionConfigDiagnostic
    {
        /// <summary>How seriously to treat this finding.</summary>
        public ConvaiActionConfigDiagnosticSeverity Severity { get; }

        /// <summary>Actionable description of the problem.</summary>
        public string Message { get; }

        /// <summary>Config region the finding refers to (e.g. "Actionable object #2"); may be empty.</summary>
        public string Context { get; }

        public ConvaiActionConfigDiagnostic(
            ConvaiActionConfigDiagnosticSeverity severity,
            string message,
            string context = null)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            Context = context ?? string.Empty;
        }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Context) ? $"{Severity}: {Message}" : $"{Severity}: {Context}: {Message}";
    }

    /// <summary>
    ///     Authoring-time validation for a <see cref="ConvaiActionConfigSource" />: surfaces
    ///     broken executor bindings, empty names, duplicate targets, and unmatched initial
    ///     attention before they turn into silent runtime failures. Used by the inspector and
    ///     the Action Debug Window; safe to call from user tooling.
    /// </summary>
    public static class ConvaiActionConfigValidator
    {
        /// <summary>Validates the authored config and returns every finding (possibly empty, never null).</summary>
        public static IReadOnlyList<ConvaiActionConfigDiagnostic> Validate(ConvaiActionConfigSource source)
        {
            var diagnostics = new List<ConvaiActionConfigDiagnostic>();
            if (source == null)
            {
                diagnostics.Add(new ConvaiActionConfigDiagnostic(
                    ConvaiActionConfigDiagnosticSeverity.Error,
                    "ConvaiActionConfigSource is missing."));
                return diagnostics;
            }

            ValidateDefinitions(source.Definitions, source.Objects, source.Characters, diagnostics);
            ValidateObjects(source.Objects, diagnostics);
            ValidateCharacters(source.Characters, diagnostics);
            ValidateDuplicateTargetNames(source.Objects, source.Characters, diagnostics);
            ValidateInitialAttention(source.InitialAttentionObject, source.Objects, diagnostics);
            return diagnostics;
        }

        internal static bool HasErrors(IReadOnlyList<ConvaiActionConfigDiagnostic> diagnostics)
        {
            if (diagnostics == null)
                return false;

            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i]?.Severity == ConvaiActionConfigDiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

        internal static bool IsExecutableDefinition(ConvaiActionDefinition definition) =>
            definition?.Executor is IConvaiActionExecutor;

        private static void ValidateDefinitions(
            IReadOnlyList<ConvaiActionDefinition> definitions,
            IReadOnlyList<ConvaiActionObjectDefinition> objects,
            IReadOnlyList<ConvaiActionCharacterDefinition> characters,
            ICollection<ConvaiActionConfigDiagnostic> diagnostics)
        {
            if (definitions == null || definitions.Count == 0)
                return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < definitions.Count; i++)
            {
                ConvaiActionDefinition definition = definitions[i];
                string actionName = ConvaiActionDefinition.NormalizeActionName(definition?.ActionName);
                string context = $"Action definition #{i + 1}";
                if (string.IsNullOrEmpty(actionName))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Error,
                        "Action definition has a blank action name.",
                        context));
                    continue;
                }

                if (!seen.Add(actionName))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Error,
                        $"Duplicate action definition '{actionName}'.",
                        context));
                }

                if (!IsExecutableDefinition(definition))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Error,
                        $"Action '{actionName}' is missing a valid executor.",
                        context));
                }

                ValidateTargetAvailability(definition, actionName, objects, characters, diagnostics);
                ValidateParameters(definition, actionName, diagnostics);
            }
        }

        private static void ValidateParameters(
            ConvaiActionDefinition definition,
            string actionName,
            ICollection<ConvaiActionConfigDiagnostic> diagnostics)
        {
            if (definition.Parameters == null || definition.Parameters.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(definition.Description))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Warning,
                        $"Action '{actionName}' has no description or parameters, so backend intent may be vague."));
                }

                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < definition.Parameters.Count; i++)
            {
                ConvaiActionParameterDefinition parameter = definition.Parameters[i];
                string paramName = ConvaiActionParameterDefinition.Normalize(parameter?.Name);
                string context = $"Action '{actionName}' parameter #{i + 1}";
                if (string.IsNullOrEmpty(paramName))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Error,
                        "Parameter has a blank name.",
                        context));
                    continue;
                }

                if (!seen.Add(paramName))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Error,
                        $"Duplicate parameter '{paramName}'.",
                        context));
                }

                if (i == 0 && !string.IsNullOrWhiteSpace(parameter.Connector))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Error,
                        $"Parameter '{paramName}' cannot have a connector because it is the first parameter.",
                        context));
                }

                if (parameter.Type == ConvaiActionParameterType.Choice && !HasChoices(parameter.Choices))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Error,
                        $"Choice parameter '{paramName}' must define at least one choice.",
                        context));
                }

                if (string.IsNullOrWhiteSpace(parameter.Description))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Warning,
                        $"Parameter '{paramName}' is missing description.",
                        context));
                }
            }
        }

        private static bool HasChoices(IReadOnlyList<string> choices)
        {
            if (choices == null)
                return false;

            for (int i = 0; i < choices.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(choices[i]))
                    return true;
            }

            return false;
        }

        private static void ValidateTargetAvailability(
            ConvaiActionDefinition definition,
            string actionName,
            IReadOnlyList<ConvaiActionObjectDefinition> objects,
            IReadOnlyList<ConvaiActionCharacterDefinition> characters,
            ICollection<ConvaiActionConfigDiagnostic> diagnostics)
        {
            bool hasObjects = HasNamedTargets(objects);
            bool hasCharacters = HasNamedTargets(characters);
            switch (definition.TargetRequirement)
            {
                case ConvaiActionTargetRequirement.Object when !hasObjects:
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Warning,
                        $"Action '{actionName}' requires an object target but no actionable objects are named."));
                    break;
                case ConvaiActionTargetRequirement.Character when !hasCharacters:
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Warning,
                        $"Action '{actionName}' requires a character target but no actionable characters are named."));
                    break;
                case ConvaiActionTargetRequirement.Either when !hasObjects && !hasCharacters:
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Warning,
                        $"Action '{actionName}' accepts object or character targets but no targets are named."));
                    break;
            }
        }

        private static void ValidateObjects(
            IReadOnlyList<ConvaiActionObjectDefinition> objects,
            ICollection<ConvaiActionConfigDiagnostic> diagnostics)
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Count; i++)
            {
                ConvaiActionObjectDefinition actionObject = objects[i];
                string name = NormalizeName(actionObject?.Name);
                string context = $"Actionable object #{i + 1}";
                if (string.IsNullOrEmpty(name))
                    continue;

                if (string.IsNullOrWhiteSpace(actionObject.Description))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Warning,
                        $"Actionable object '{name}' is missing object description.",
                        context));
                }

                if (actionObject.GameObjectReference == null)
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Error,
                        $"Actionable object '{name}' is missing GameObject reference.",
                        context));
                }
            }
        }

        private static void ValidateCharacters(
            IReadOnlyList<ConvaiActionCharacterDefinition> characters,
            ICollection<ConvaiActionConfigDiagnostic> diagnostics)
        {
            if (characters == null)
                return;

            for (int i = 0; i < characters.Count; i++)
            {
                ConvaiActionCharacterDefinition character = characters[i];
                string name = NormalizeName(character?.Name);
                string context = $"Actionable character #{i + 1}";
                if (string.IsNullOrEmpty(name))
                    continue;

                if (string.IsNullOrWhiteSpace(character.Bio))
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Warning,
                        $"Actionable character '{name}' is missing character bio.",
                        context));
                }

                if (character.GameObjectReference == null)
                {
                    diagnostics.Add(new ConvaiActionConfigDiagnostic(
                        ConvaiActionConfigDiagnosticSeverity.Error,
                        $"Actionable character '{name}' is missing GameObject reference.",
                        context));
                }
            }
        }

        private static void ValidateDuplicateTargetNames(
            IReadOnlyList<ConvaiActionObjectDefinition> objects,
            IReadOnlyList<ConvaiActionCharacterDefinition> characters,
            ICollection<ConvaiActionConfigDiagnostic> diagnostics)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (objects != null)
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    string name = NormalizeName(objects[i]?.Name);
                    AddTargetName(name, diagnostics, names);
                }
            }

            if (characters == null)
                return;

            for (int i = 0; i < characters.Count; i++)
            {
                string name = NormalizeName(characters[i]?.Name);
                AddTargetName(name, diagnostics, names);
            }
        }

        private static void AddTargetName(
            string name,
            ICollection<ConvaiActionConfigDiagnostic> diagnostics,
            ISet<string> names)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (names.Add(name))
                return;

            diagnostics.Add(new ConvaiActionConfigDiagnostic(
                ConvaiActionConfigDiagnosticSeverity.Error,
                $"Duplicate target name '{name}' across actionable target lists."));
        }

        private static void ValidateInitialAttention(
            string initialAttentionObject,
            IReadOnlyList<ConvaiActionObjectDefinition> objects,
            ICollection<ConvaiActionConfigDiagnostic> diagnostics)
        {
            string attentionName = NormalizeName(initialAttentionObject);
            if (string.IsNullOrEmpty(attentionName))
                return;

            if (!ContainsName(objects, attentionName))
            {
                diagnostics.Add(new ConvaiActionConfigDiagnostic(
                    ConvaiActionConfigDiagnosticSeverity.Warning,
                    $"Initial attention object '{attentionName}' does not match any authored action object."));
            }
        }

        private static bool HasNamedTargets<T>(IReadOnlyList<T> targets)
        {
            if (targets == null)
                return false;

            for (int i = 0; i < targets.Count; i++)
            {
                string name = targets[i] switch
                {
                    ConvaiActionObjectDefinition actionObject => actionObject?.Name,
                    ConvaiActionCharacterDefinition character => character?.Name,
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(name))
                    return true;
            }

            return false;
        }

        private static bool ContainsName(
            IReadOnlyList<ConvaiActionObjectDefinition> objects,
            string name)
        {
            if (objects == null)
                return false;

            for (int i = 0; i < objects.Count; i++)
            {
                if (string.Equals(NormalizeName(objects[i]?.Name), name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string NormalizeName(string value) => ConvaiActionText.Normalize(value);
    }
}
