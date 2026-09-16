using System.Collections.Generic;
using Convai.Domain.Logging;
using Convai.Runtime.Logging;
using UnityEngine;

namespace Convai.Modules.Embodiment.Presets
{
    /// <summary>
    ///     Searchable collection of <see cref="CharacterEmbodimentPreset" /> assets. Used by
    ///     tooling and character selector UIs to present a drop-down of
    ///     archetypes to the user and to resolve a preset by its <see cref="CharacterEmbodimentPreset.PresetId" />
    ///     at runtime.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Convai/Embodiment/Embodiment Preset Library",
        fileName = "EmbodimentPresetLibrary")]
    public sealed class EmbodimentPresetLibrary : ScriptableObject
    {
        [SerializeField]
        private List<CharacterEmbodimentPreset> presets = new();

        public IReadOnlyList<CharacterEmbodimentPreset> Presets => presets;

        private void OnValidate()
        {
            if (HasDuplicatePresetIds(out string message))
                ConvaiLogger.Warning($"{message}", LogCategory.Character);
        }

        /// <summary>
        ///     Returns the preset whose <see cref="CharacterEmbodimentPreset.PresetId" /> matches
        ///     <paramref name="id" /> (case-insensitive). Null when no entry matches.
        /// </summary>
        public CharacterEmbodimentPreset Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < presets.Count; i++)
            {
                CharacterEmbodimentPreset preset = presets[i];
                if (preset != null && string.Equals(preset.PresetId, id, System.StringComparison.OrdinalIgnoreCase))
                    return preset;
            }
            return null;
        }

        public bool HasDuplicatePresetIds(out string message)
        {
            if (!DuplicateDetector.HasDuplicates(
                    presets,
                    preset => preset?.PresetId,
                    out string duplicateKeys))
            {
                message = null;
                return false;
            }

            message = $"Duplicate preset ids: {duplicateKeys}. First matching preset is used.";
            return true;
        }
    }
}
