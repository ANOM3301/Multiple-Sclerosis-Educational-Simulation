using System.Collections.Generic;
using Convai.Shared.Actions;

namespace Convai.Runtime.Actions
{
    /// <summary>
    ///     Read side of a character's live action authoring: what the backend was told it can do
    ///     (<see cref="ActionConfig" />) and the local definitions that execute it. Implemented by
    ///     <c>ConvaiCharacter</c>; consumed by enrichment and dispatch so both always see the
    ///     session's effective configuration rather than raw inspector state.
    /// </summary>
    public interface IConvaiActionRuntimeSource
    {
        /// <summary>
        ///     Effective wire-facing action config for the current session (actions, objects,
        ///     characters, initial attention), or null when the character declares no actions.
        /// </summary>
        ConvaiActionConfig ActionConfig { get; }

        /// <summary>
        ///     Effective local action definitions (deduplicated, executable) used to enrich and
        ///     dispatch backend commands. Never null; empty when the character declares no actions.
        /// </summary>
        IReadOnlyList<ConvaiActionDefinition> ActionDefinitions { get; }
    }

    /// <summary>
    ///     Internal safety view containing every locally executable definition available for the
    ///     session, including definitions outside the confirmed request-level action subset.
    /// </summary>
    internal interface IConvaiActionDefinitionCatalogSource
    {
        IReadOnlyList<ConvaiActionDefinition> ActionDefinitionCatalog { get; }
    }
}
