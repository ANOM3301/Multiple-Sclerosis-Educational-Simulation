using System.Linq;

namespace Convai.Editor.AI
{
    public static partial class ConvaiMcpTools
    {
        private static Guidance BuildGuidance(ConvaiGuidanceTopic topic)
        {
            const string docs = "Packages/com.convai.convai-sdk-for-unity/Documentation~/";
            switch (topic)
            {
                case ConvaiGuidanceTopic.Setup:
                    return new Guidance(
                        "Complete runnable Convai scene setup proactively. An explicit setup request authorizes safe, reversible defaults; do not stop for placeholder, voice-mode, auto-connect, or save confirmations.",
                        new[] { "Convai SDK installed", "Credential status checked; API key configured outside MCP before runtime verification", "Open target scene" },
                        new[]
                        {
                            "Read project status, inspect the scene, validate setup, and capture exact instance IDs.",
                            "Preview Convai.SetupConversationScene, then apply it without duplicate confirmation for an explicit setup request.",
                            "Let the orchestrator reuse unambiguous targets or create standalone Convai Player and visible Capsule Convai Character placeholders; never repurpose Main Camera.",
                            "Default to Audio, HandsFree input, automatic connection, and inline configuration unless the user requested otherwise.",
                            "Do all independent work first. Ask only for irreducible values such as a missing Character ID or an ambiguous authored target.",
                            "Inspect, validate, and run Convai.DiagnoseConversation. Keep the scene dirty and do not save unless explicitly requested."
                        },
                        AllToolIds,
                        new[] { "Unity.ManageGameObject", "Unity.ManageScene" },
                        new[]
                        {
                            docs + "SETUP.md",
                            docs + "API-ENTRYPOINTS.md",
                            "Packages/com.convai.convai-sdk-for-unity/AIAssistantSkills/convai-unity-sdk/references/workflows.md"
                        });
                case ConvaiGuidanceTopic.Actions:
                    return FeatureGuidance(
                        "Author explicit action affordances and bind local executors; never infer affordances from scene metadata.",
                        new[] { "Convai.ConfigureActions", "Convai.DiagnoseActions", "Convai.SimulateAction" },
                        new[] { docs + "ACTIONS.md", docs + "ACTIONS-INTEGRATION-TUTORIAL.md" });
                case ConvaiGuidanceTopic.DynamicContext:
                    return TopicGuidance(
                        "Send state, events, and attention-object changes through the character dynamic-context facade.",
                        docs + "DYNAMIC-CONTEXT.md");
                case ConvaiGuidanceTopic.Vision:
                    return TopicGuidance(
                        "Configure a vision publisher and one frame source under the room hierarchy before enabling video mode.",
                        docs + "DYNAMIC-VISION-CONTEXT.md",
                        "Packages/com.convai.convai-sdk-for-unity/SDK/Modules/Vision/README.md");
                case ConvaiGuidanceTopic.Narrative:
                    return FeatureGuidance(
                        "Use the Narrative module for section state and named or inline trigger workflows.",
                        new[] { "Convai.ConfigureNarrative", "Convai.DiagnoseNarrative" },
                        new[] { "Packages/com.convai.convai-sdk-for-unity/SDK/Modules/Narrative/README.md" });
                case ConvaiGuidanceTopic.Embodiment:
                    return FeatureGuidance(
                        "Configure each embodiment module through its branded component and profile; missing peers must degrade gracefully.",
                        new[] { "Convai.ConfigureLipSync", "Convai.DiagnoseLipSync" },
                        new[] { docs + "GAZE.md", docs + "BODY-ANIMATION.md", docs + "BODY-LANGUAGE.md", docs + "EMOTIONS.md" });
                case ConvaiGuidanceTopic.Events:
                    return TopicGuidance(
                        "Prefer ConvaiManager.Events for typed code and relay components for Inspector-driven UnityEvents.",
                        docs + "WORKING-WITH-EVENTS.md");
                case ConvaiGuidanceTopic.Runtime:
                    return FeatureGuidance(
                        "Use ConvaiManager for session ownership, Audio for room audio, and Transcripts for canonical history.",
                        new[] { "Convai.DiagnoseConversation", "Convai.TraceRuntimeEvents", "Convai.DiagnoseActions", "Convai.DiagnoseTranscripts" },
                        new[] { docs + "API-ENTRYPOINTS.md", docs + "TROUBLESHOOTING.md" });
                default:
                    return new Guidance(
                        "Use Convai tools only for SDK-aware operations; compose official Unity MCP tools for generic project changes.",
                        new[] { "Unity AI Assistant 2.13+", "Unity MCP client approved", "Convai SDK installed" },
                        new[]
                        {
                            "Load topic guidance.",
                            "Inspect before mutation.",
                            "Use exact instance IDs.",
                            "Validate after mutation.",
                            "Never pass API keys through MCP."
                        },
                        new[]
                        {
                            "Convai.GetGuidance",
                            "Convai.GetProjectStatus",
                            "Convai.InspectScene",
                            "Convai.ValidateSetup"
                        },
                        new[]
                        {
                            "Unity.ManageGameObject",
                            "Unity.ManageAsset",
                            "Unity.ManageScript",
                            "Unity.ManageScene"
                        },
                        new[] { docs + "README.md", docs + "API-ENTRYPOINTS.md" });
            }
        }

        private static Guidance TopicGuidance(string summary, params string[] documentation) => new(
            summary,
            new[] { "Inspect relevant Convai components and profile assets before changes." },
            new[]
            {
                "Read the referenced feature documentation.",
                "Use official Unity tools for generic scripts and GameObjects.",
                "Use Convai feature tools when available.",
                "Run Convai.ValidateSetup after configuration."
            },
            new[] { "Convai.GetGuidance", "Convai.InspectScene", "Convai.ValidateSetup" },
            new[] { "Unity.ManageGameObject", "Unity.ManageAsset", "Unity.ManageScript" },
            documentation);

        private static Guidance FeatureGuidance(string summary, string[] featureTools, string[] documentation) => new(
            summary,
            new[] { "Inspect relevant Convai components and profile assets before changes." },
            new[]
            {
                "Read the referenced feature documentation.",
                "Diagnose before repair.",
                "Use official Unity tools for generic scripts and GameObjects.",
                "Preview and apply only missing or broken Convai configuration.",
                "Run feature diagnosis and Convai.ValidateSetup after configuration."
            },
            new[] { "Convai.GetGuidance", "Convai.InspectScene", "Convai.ValidateSetup" }
                .Concat(featureTools ?? System.Array.Empty<string>()).ToArray(),
            new[] { "Unity.ManageGameObject", "Unity.ManageAsset", "Unity.ManageScript" },
            documentation);

        private static readonly string[] AllToolIds = ConvaiMcpToolCatalog.All.ToArray();

        private sealed class Guidance
        {
            public Guidance(
                string summary,
                string[] prerequisites,
                string[] workflow,
                string[] convaiTools,
                string[] unityTools,
                string[] documentation)
            {
                Summary = summary;
                Prerequisites = prerequisites;
                Workflow = workflow;
                ConvaiTools = convaiTools;
                UnityTools = unityTools;
                Documentation = documentation;
            }

            public string Summary { get; }
            public string[] Prerequisites { get; }
            public string[] Workflow { get; }
            public string[] ConvaiTools { get; }
            public string[] UnityTools { get; }
            public string[] Documentation { get; }
        }
    }
}
