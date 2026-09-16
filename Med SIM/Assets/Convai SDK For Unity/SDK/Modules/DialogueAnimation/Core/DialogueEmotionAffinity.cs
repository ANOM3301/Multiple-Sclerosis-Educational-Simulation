namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Coarse emotional affinity tag attached to dialogue animation clips. Used by the
    ///     <see cref="IDialogueVariantSelector" /> to bias clip selection toward authored
    ///     mood variants that best match the character's current
    ///     <see cref="Convai.Domain.Embodiment.Readings.EmotionReading" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The tag set is intentionally small so designers can reason about authored
    ///         content at a glance. <see cref="DialogueEmotionAffinityMapping" /> resolves
    ///         the current emotion reading into one of these buckets.
    ///     </para>
    ///     <para>
    ///         A clip with zero affinity tags is treated as universally acceptable; clips
    ///         with one or more tags are boosted when the character's dominant emotion
    ///         belongs to any of the tagged buckets.
    ///     </para>
    /// </remarks>
    public enum DialogueEmotionAffinity
    {
        /// <summary>Calm / ambient. Default for unmarked clips.</summary>
        Neutral = 0,

        /// <summary>Positive valence, low-to-mid arousal (joy, contentment, amusement).</summary>
        Happy = 1,

        /// <summary>Negative valence, low arousal (sadness, disappointment, grief).</summary>
        Sad = 2,

        /// <summary>Negative valence, high arousal (anger, annoyance, disgust).</summary>
        Angry = 3,

        /// <summary>High arousal, ambiguous valence (surprise, excitement, realization).</summary>
        Surprised = 4,

        /// <summary>Low valence, high arousal fight-or-flight (fear, nervousness).</summary>
        Fearful = 5,

        /// <summary>Inquisitive, exploratory (curiosity, thoughtful).</summary>
        Curious = 6,

        /// <summary>High energy, confident presentation (excitement, pride, assertive speech).</summary>
        Energetic = 7
    }
}
