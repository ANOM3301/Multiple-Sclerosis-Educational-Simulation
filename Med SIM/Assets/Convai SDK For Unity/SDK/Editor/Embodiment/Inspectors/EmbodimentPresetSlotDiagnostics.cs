using System;
using System.Collections.Generic;
using Convai.Modules.Attention.Profiles;
using Convai.Modules.Embodiment.Presets;
using Convai.Modules.ConversationFlow.Profiles;
using Convai.Modules.DialogueAnimation.Profiles;
using Convai.Modules.Emotion.Profiles;
using Convai.Modules.FacialAnimation.Profiles;
using Convai.Modules.Gaze.Profiles;
using UnityEngine;

namespace Convai.Editor.Embodiment.Inspectors
{
    internal enum EmbodimentPresetSlotSeverity
    {
        Info,
        Warning,
        Error
    }

    internal readonly struct EmbodimentPresetSlotDiagnostic
    {
        public EmbodimentPresetSlotDiagnostic(int slotIndex, string moduleId, string message,
            EmbodimentPresetSlotSeverity severity)
        {
            SlotIndex = slotIndex;
            ModuleId = moduleId;
            Message = message;
            Severity = severity;
        }

        public int SlotIndex { get; }
        public string ModuleId { get; }
        public string Message { get; }
        public EmbodimentPresetSlotSeverity Severity { get; }
    }

    internal static class EmbodimentPresetSlotDiagnostics
    {
        public static readonly IReadOnlyDictionary<string, Type> KnownModuleProfileTypes =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "convai.conversation-flow",  typeof(ConvaiConversationFlowProfile)   },
                { "convai.attention",          typeof(ConvaiAttentionProfile)           },
                { "convai.emotion",            typeof(ConvaiEmotionProfile)             },
                { "convai.gaze-eye",           typeof(ConvaiGazeEyeProfile)            },
                { "convai.gaze-head",          typeof(ConvaiGazeHeadProfile)           },
                { "convai.gaze-coordination",   typeof(ConvaiGazeCoordinationProfile)    },
                { "convai.dialogue-animation", typeof(ConvaiDialogueAnimationProfile)  },
                { "convai.baked-facial-clip",  typeof(ConvaiFacialAnimationProfile)    },
            };

        public static List<EmbodimentPresetSlotDiagnostic> Analyze(IReadOnlyList<EmbodimentProfileSlot> slots)
        {
            var diagnostics = new List<EmbodimentPresetSlotDiagnostic>();
            if (slots == null || slots.Count == 0)
            {
                diagnostics.Add(new EmbodimentPresetSlotDiagnostic(
                    -1,
                    string.Empty,
                    "No slots defined. Add entries to the profileSlots list.",
                    EmbodimentPresetSlotSeverity.Warning));
                return diagnostics;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < slots.Count; i++)
            {
                EmbodimentProfileSlot slot = slots[i];
                if (slot == null)
                {
                    diagnostics.Add(new EmbodimentPresetSlotDiagnostic(
                        i,
                        string.Empty,
                        $"Slot [{i}] is null.",
                        EmbodimentPresetSlotSeverity.Error));
                    continue;
                }

                string moduleId = slot.ModuleId;
                bool emptyId = string.IsNullOrWhiteSpace(moduleId);
                if (emptyId)
                {
                    diagnostics.Add(new EmbodimentPresetSlotDiagnostic(
                        i,
                        string.Empty,
                        $"Slot [{i}] has an empty module ID. Assign a module ID.",
                        EmbodimentPresetSlotSeverity.Error));
                    continue;
                }

                if (!seen.Add(moduleId))
                {
                    diagnostics.Add(new EmbodimentPresetSlotDiagnostic(
                        i,
                        moduleId,
                        $"Duplicate module ID '{moduleId}'. Only the first slot is used.",
                        EmbodimentPresetSlotSeverity.Error));
                }

                ScriptableObject profile = slot.Profile;
                if (profile == null)
                {
                    diagnostics.Add(new EmbodimentPresetSlotDiagnostic(
                        i,
                        moduleId,
                        $"'{moduleId}': profile is null. The module will use its runtime default.",
                        EmbodimentPresetSlotSeverity.Warning));
                    continue;
                }

                if (!KnownModuleProfileTypes.TryGetValue(moduleId, out Type expectedType))
                {
                    diagnostics.Add(new EmbodimentPresetSlotDiagnostic(
                        i,
                        moduleId,
                        $"'{moduleId}' is not a recognized built-in module ID. Verify spelling or ignore if custom.",
                        EmbodimentPresetSlotSeverity.Warning));
                    continue;
                }

                if (!expectedType.IsInstanceOfType(profile))
                {
                    diagnostics.Add(new EmbodimentPresetSlotDiagnostic(
                        i,
                        moduleId,
                        $"'{moduleId}': expected {expectedType.Name} but got {profile.GetType().Name}. The module will reject this profile at runtime.",
                        EmbodimentPresetSlotSeverity.Error));
                }
            }

            return diagnostics;
        }

        public static bool HasErrors(IReadOnlyList<EmbodimentPresetSlotDiagnostic> diagnostics)
        {
            if (diagnostics == null) return false;
            for (int i = 0; i < diagnostics.Count; i++)
                if (diagnostics[i].Severity == EmbodimentPresetSlotSeverity.Error)
                    return true;
            return false;
        }
    }
}
