using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Convai.Shared.Types;

namespace Convai.Runtime.Actions
{
    /// <summary>
    ///     Renders <see cref="ConvaiActionDefinition" /> templates into the deterministic
    ///     ASCII wire strings sent to the backend as available actions.
    /// </summary>
    internal static class ConvaiActionTemplateRenderer
    {
        private sealed class RenderCacheEntry
        {
            public int InputHash;
            public string Rendered;
        }

        // Definitions render repeatedly (config builds, allow-list filtering, command enrichment).
        // Cache per definition instance, validated by an input hash so field edits still re-render.
        private static readonly ConditionalWeakTable<ConvaiActionDefinition, RenderCacheEntry> RenderCache = new();

        /// <summary>Renders the wire template for <paramref name="definition" />.</summary>
        public static string Render(ConvaiActionDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            int inputHash = ComputeInputHash(definition);
            RenderCacheEntry entry = RenderCache.GetOrCreateValue(definition);
            if (entry.Rendered != null && entry.InputHash == inputHash)
                return entry.Rendered;

            string rendered = RenderUncached(definition);
            entry.InputHash = inputHash;
            entry.Rendered = rendered;
            return rendered;
        }

        private static int ComputeInputHash(ConvaiActionDefinition definition)
        {
            var hash = new HashCode();
            AddString(ref hash, definition.ActionName);
            AddString(ref hash, definition.Description);

            IReadOnlyList<ConvaiActionParameterDefinition> parameters = definition.Parameters;
            if (parameters != null)
            {
                for (int i = 0; i < parameters.Count; i++)
                {
                    ConvaiActionParameterDefinition parameter = parameters[i];
                    if (parameter == null)
                    {
                        hash.Add(0);
                        continue;
                    }

                    AddString(ref hash, parameter.Name);
                    AddString(ref hash, parameter.Description);
                    hash.Add((int)parameter.Type);
                    AddString(ref hash, parameter.Connector);

                    if (parameter.Choices != null)
                    {
                        for (int choiceIndex = 0; choiceIndex < parameter.Choices.Count; choiceIndex++)
                            AddString(ref hash, parameter.Choices[choiceIndex]);
                    }
                }
            }

            return hash.ToHashCode();
        }

        private static void AddString(ref HashCode hash, string value) =>
            hash.Add(value == null ? 0 : StringComparer.Ordinal.GetHashCode(value));

        private static string RenderUncached(ConvaiActionDefinition definition)
        {
            string actionName = NormalizeForWire(ConvaiActionDefinition.NormalizeActionName(definition.ActionName));
            if (string.IsNullOrEmpty(actionName))
                return string.Empty;

            var builder = new StringBuilder(actionName);
            IReadOnlyList<ConvaiActionParameterDefinition> parameters = definition.Parameters;
            if (parameters != null)
            {
                for (int i = 0; i < parameters.Count; i++)
                {
                    ConvaiActionParameterDefinition parameter = parameters[i];
                    string parameterName = NormalizeForWire(ConvaiActionParameterDefinition.Normalize(parameter?.Name));
                    if (string.IsNullOrEmpty(parameterName))
                        continue;

                    string connector = NormalizeForWire(ConvaiActionParameterDefinition.Normalize(parameter.Connector));
                    builder.Append(' ');
                    if (!string.IsNullOrEmpty(connector))
                        builder.Append(connector).Append(' ');

                    builder.Append('{')
                        .Append(parameterName)
                        .Append(": ")
                        .Append(ToWireType(parameter.Type));

                    if (parameter.Type == ConvaiActionParameterType.Choice &&
                        parameter.Choices != null &&
                        parameter.Choices.Count > 0)
                    {
                        builder.Append(" [");
                        bool wroteChoice = false;
                        for (int choiceIndex = 0; choiceIndex < parameter.Choices.Count; choiceIndex++)
                        {
                            string choice = NormalizeForWire(ConvaiActionParameterDefinition.Normalize(parameter.Choices[choiceIndex]));
                            if (string.IsNullOrEmpty(choice))
                                continue;

                            if (wroteChoice)
                                builder.Append('|');

                            builder.Append(choice);
                            wroteChoice = true;
                        }

                        builder.Append(']');
                    }

                    builder.Append('}');
                }
            }

            AppendDescriptions(builder, definition);
            return builder.ToString();
        }

        private static void AppendDescriptions(StringBuilder builder, ConvaiActionDefinition definition)
        {
            string description = NormalizeForWire(ConvaiActionParameterDefinition.Normalize(definition.Description));
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(description))
                parts.Add(description);

            if (definition.Parameters != null)
            {
                for (int i = 0; i < definition.Parameters.Count; i++)
                {
                    ConvaiActionParameterDefinition parameter = definition.Parameters[i];
                    string name = NormalizeForWire(ConvaiActionParameterDefinition.Normalize(parameter?.Name));
                    string paramDescription = NormalizeForWire(ConvaiActionParameterDefinition.Normalize(parameter?.Description));
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(paramDescription))
                        continue;

                    parts.Add($"{name}: {paramDescription}");
                }
            }

            if (parts.Count == 0)
                return;

            builder.Append(" - ").Append(string.Join(" ", parts));
        }

        private static string ToWireType(ConvaiActionParameterType type) =>
            type switch
            {
                ConvaiActionParameterType.Reference => "reference",
                ConvaiActionParameterType.String => "string",
                ConvaiActionParameterType.Number => "number",
                ConvaiActionParameterType.Bool => "bool",
                ConvaiActionParameterType.Choice => "choice",
                _ => "auto"
            };

        private static string NormalizeForWire(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            bool previousWasSpace = false;
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                bool isAsciiPrintable = c >= 32 && c <= 126;
                char output = isAsciiPrintable ? c : ' ';
                if (char.IsWhiteSpace(output))
                    output = ' ';

                if (output == ' ')
                {
                    if (previousWasSpace)
                        continue;

                    previousWasSpace = true;
                }
                else
                {
                    previousWasSpace = false;
                }

                builder.Append(output);
            }

            return builder.ToString().Trim();
        }
    }
}
