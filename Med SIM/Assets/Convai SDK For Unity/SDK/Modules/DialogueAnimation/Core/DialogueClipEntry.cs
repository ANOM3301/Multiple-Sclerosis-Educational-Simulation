using System;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     Authored metadata describing one selectable idle or talk clip in a
    ///     <see cref="DialogueAnimationLibrary" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Each entry pairs an <see cref="AnimationClip" /> with the metadata the
    ///         runtime selector needs: rig gender, mood affinity tags, a base weight for
    ///         weighted draws, and optional crossfade override.
    ///     </para>
    ///     <para>
    ///         The struct is a pure-data DTO: no runtime logic lives here. Interpretation
    ///         is the selector's job so the data stays reusable across selection
    ///         strategies.
    ///     </para>
    /// </remarks>
    [Serializable]
    public struct DialogueClipEntry
    {
        [Tooltip("Animation clip played for this library entry. Must be a humanoid or " +
                 "generic clip compatible with the character's animator rig.")]
        [SerializeField] private AnimationClip _clip;

        [Tooltip("Rig gender this clip was authored for. Clips tagged Neutral are " +
                 "eligible for every character; gendered clips are filtered out when the " +
                 "character's configured gender does not match.")]
        [SerializeField] private CharacterGender _gender;

        [Tooltip("Mood affinities this clip best suits. Leave empty to make the clip " +
                 "universally acceptable regardless of the character's emotion.")]
        [SerializeField] private DialogueEmotionAffinity[] _preferredEmotions;

        [Tooltip("Base selection weight. Higher values make the clip more likely to be " +
                 "drawn by weighted selectors. Ignored by round-robin selectors.")]
        [Range(0.1f, 5f)]
        [SerializeField] private float _selectionWeight;

        [Tooltip("Per-clip crossfade duration override in seconds. Zero or negative values " +
                 "fall back to the library's default crossfade.")]
        [Min(0f)]
        [SerializeField] private float _crossFadeDurationOverride;

        [Tooltip("Talk pool routing. HeadOnly / BodyAndHead compete for the head pass; " +
                 "BodyOnly is picked separately for the body talk pass when it is not already " +
                 "filled by a combined clip on the same utterance.")]
        [SerializeField] private DialogueTalkBodyCoverage _talkBodyCoverage;

        /// <summary>Source animation clip.</summary>
        public AnimationClip Clip => _clip;

        /// <summary>Rig gender this clip was authored for.</summary>
        public CharacterGender Gender => _gender;

        /// <summary>Mood affinity tags. Never null when read through this accessor.</summary>
        public DialogueEmotionAffinity[] PreferredEmotions =>
            _preferredEmotions ?? Array.Empty<DialogueEmotionAffinity>();

        /// <summary>Base selection weight clamped to <c>[0.1, 5]</c>.</summary>
        public float SelectionWeight => Mathf.Clamp(_selectionWeight <= 0f ? 1f : _selectionWeight, 0.1f, 5f);

        /// <summary>
        ///     Per-clip crossfade override in seconds, or <c>0</c> when the entry wants the
        ///     library default.
        /// </summary>
        public float CrossFadeDurationOverride => _crossFadeDurationOverride < 0f ? 0f : _crossFadeDurationOverride;

        /// <summary>How this talk clip uses the split upper / head talk layers.</summary>
        public DialogueTalkBodyCoverage TalkBodyCoverage => _talkBodyCoverage;

        /// <summary>Convenience flag for validators and editor tooling.</summary>
        public bool IsValid => _clip != null;
    }
}
