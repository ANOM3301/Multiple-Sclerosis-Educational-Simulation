namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Gender bucket used by dialogue animation libraries to filter a
    ///     character's available idle and talk clips.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The enum is intentionally minimal ; it only exists to keep rig-appropriate
    ///         clips out of each other's candidate pools. Libraries may tag clips with
    ///         <see cref="Neutral" /> to make them eligible for every character regardless
    ///         of the character's configured gender.
    ///     </para>
    ///     <para>
    ///         This is not a character-identity abstraction; richer persona modeling belongs
    ///         in higher-level systems.
    ///     </para>
    /// </remarks>
    public enum CharacterGender
    {
        /// <summary>Rig-agnostic clip or character. Always eligible.</summary>
        Neutral = 0,

        /// <summary>Clip authored for, or character configured as, a masculine frame.</summary>
        Male = 1,

        /// <summary>Clip authored for, or character configured as, a feminine frame.</summary>
        Female = 2
    }
}
