using System.Collections.Generic;

namespace Convai.Editor.AI
{
    internal static class ConvaiMcpToolCatalog
    {
        internal static readonly IReadOnlyList<string> All = new[]
        {
            "Convai.BootstrapScene",
            "Convai.ConfigureCharacter",
            "Convai.ConfigureActions",
            "Convai.ConfigureLipSync",
            "Convai.ConfigureNarrative",
            "Convai.ConfigurePlayer",
            "Convai.ConfigureRoom",
            "Convai.ConfigureTranscripts",
            "Convai.DiagnoseActions",
            "Convai.DiagnoseConversation",
            "Convai.DiagnoseLipSync",
            "Convai.DiagnoseNarrative",
            "Convai.DiagnoseTranscripts",
            "Convai.GetGuidance",
            "Convai.GetProjectStatus",
            "Convai.InspectScene",
            "Convai.SetupConversationScene",
            "Convai.SimulateAction",
            "Convai.TraceRuntimeEvents",
            "Convai.ValidateSetup"
        };
    }
}
