using Convai.Domain.Embodiment.Readings;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Per-pick input to an <see cref="IDialogueVariantSelector" />. Bundles everything
    ///     the selector needs in a single immutable struct so the interface signature stays
    ///     compact and future-proof.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The struct is passed by <c>in</c> on selection to avoid unnecessary copies
    ///         in the hot tick path.
    ///     </para>
    /// </remarks>
    public readonly struct VariantSelectionContext
    {
        /// <summary>Character gender; used to filter gendered entries.</summary>
        public CharacterGender CharacterGender { get; }

        /// <summary>Dominant emotion reading from the emotion module.</summary>
        public EmotionReading Emotion { get; }

        /// <summary>
        ///     Index (within the candidate list) of the clip most recently played. A value
        ///     of <c>-1</c> means no clip has been played yet; selectors use this to avoid
        ///     picking the same clip twice in a row.
        /// </summary>
        public int LastPlayedIndex { get; }

        /// <summary>
        ///     Multiplier applied to the match bonus when a candidate's affinity tags
        ///     match the current emotion bucket. A value of <c>0</c> disables emotion
        ///     bias; higher values make emotion-matched clips dominate the draw.
        /// </summary>
        public float EmotionBiasStrength { get; }

        /// <summary>
        ///     Seed for the draw. The controller advances a private stream and passes each
        ///     value here; selectors treat it as an opaque uint (deterministic LCG chain
        ///     when the runtime config enables test mode, otherwise mixed with session
        ///     entropy at startup).
        /// </summary>
        public uint Seed { get; }

        public VariantSelectionContext(
            CharacterGender characterGender,
            in EmotionReading emotion,
            int lastPlayedIndex,
            float emotionBiasStrength,
            uint seed)
        {
            CharacterGender = characterGender;
            Emotion = emotion;
            LastPlayedIndex = lastPlayedIndex < 0 ? -1 : lastPlayedIndex;
            EmotionBiasStrength = emotionBiasStrength < 0f ? 0f : emotionBiasStrength;
            Seed = seed;
        }
    }
}
