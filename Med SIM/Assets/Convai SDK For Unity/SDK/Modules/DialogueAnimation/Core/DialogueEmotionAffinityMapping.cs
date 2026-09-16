using System;
using System.Collections.Generic;
using Convai.Domain.Embodiment.Readings;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Maps canonical <see cref="EmotionReading.DominantLabel" /> strings onto a coarse
    ///     <see cref="DialogueEmotionAffinity" /> bucket used for clip selection.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The mapping follows the same Russell / Plutchik approximation used by the
    ///         SDK's shared <c>EmotionTaxonomy</c>. Unknown labels fall back to
    ///         <see cref="DialogueEmotionAffinity.Neutral" />.
    ///     </para>
    ///     <para>
    ///         The class is pure: no allocation per call. The internal dictionary is built
    ///         once at type-init time.
    ///     </para>
    /// </remarks>
    public static class DialogueEmotionAffinityMapping
    {
        private static readonly Dictionary<string, DialogueEmotionAffinity> _labelToAffinity =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "neutral", DialogueEmotionAffinity.Neutral },
                { "calm", DialogueEmotionAffinity.Neutral },
                { "approval", DialogueEmotionAffinity.Neutral },
                { "realization", DialogueEmotionAffinity.Curious },

                { "joy", DialogueEmotionAffinity.Happy },
                { "happy", DialogueEmotionAffinity.Happy },
                { "amusement", DialogueEmotionAffinity.Happy },
                { "love", DialogueEmotionAffinity.Happy },
                { "gratitude", DialogueEmotionAffinity.Happy },
                { "relief", DialogueEmotionAffinity.Happy },
                { "admiration", DialogueEmotionAffinity.Happy },
                { "caring", DialogueEmotionAffinity.Happy },
                { "optimism", DialogueEmotionAffinity.Happy },

                { "pride", DialogueEmotionAffinity.Energetic },
                { "excitement", DialogueEmotionAffinity.Energetic },
                { "desire", DialogueEmotionAffinity.Energetic },

                { "surprise", DialogueEmotionAffinity.Surprised },

                { "curiosity", DialogueEmotionAffinity.Curious },
                { "confusion", DialogueEmotionAffinity.Curious },

                { "fear", DialogueEmotionAffinity.Fearful },
                { "nervousness", DialogueEmotionAffinity.Fearful },
                { "embarrassment", DialogueEmotionAffinity.Fearful },

                { "anger", DialogueEmotionAffinity.Angry },
                { "annoyance", DialogueEmotionAffinity.Angry },
                { "disgust", DialogueEmotionAffinity.Angry },
                { "disapproval", DialogueEmotionAffinity.Angry },

                { "sadness", DialogueEmotionAffinity.Sad },
                { "disappointment", DialogueEmotionAffinity.Sad },
                { "grief", DialogueEmotionAffinity.Sad },
                { "remorse", DialogueEmotionAffinity.Sad }
            };

        /// <summary>
        ///     Resolves the affinity bucket for the supplied reading's dominant label.
        ///     Returns <see cref="DialogueEmotionAffinity.Neutral" /> when the label is
        ///     unknown or the reading is neutral / empty.
        /// </summary>
        public static DialogueEmotionAffinity Resolve(in EmotionReading reading)
        {
            if (reading.IsNeutral || string.IsNullOrEmpty(reading.DominantLabel))
                return DialogueEmotionAffinity.Neutral;

            return _labelToAffinity.TryGetValue(reading.DominantLabel, out DialogueEmotionAffinity affinity)
                ? affinity
                : DialogueEmotionAffinity.Neutral;
        }
    }
}
