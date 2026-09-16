using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Core
{
    /// <summary>
    ///     ScriptableObject asset holding the authored idle and talk clips available to a
    ///     character's <c>ConvaiDialogueAnimationController</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Libraries are usually authored per archetype (e.g. a male human library, a
    ///         female human library, a neutral bystander library) and dropped into a
    ///         character's controller slot. The controller is gender-aware, so a single library can
    ///         safely mix clips authored for multiple rigs; the selector filters at
    ///         runtime.
    ///     </para>
    ///     <para>
    ///         The asset is pure data: no runtime logic lives here. Selection policy is
    ///         injected separately via <see cref="IDialogueVariantSelector" />.
    ///     </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "DialogueAnimationLibrary",
        menuName = "Convai/Embodiment/Dialogue Animation Library",
        order = 120)]
    public sealed class DialogueAnimationLibrary : ScriptableObject, IAnimationClipLibrary
    {
        [Header("Idle Pool")]
        [Tooltip("Clips played during Idle / Attending / Thinking / Settling states. " +
                 "At least one idle clip is required for the controller to function.")]
        [SerializeField] private DialogueClipEntry[] _idles = Array.Empty<DialogueClipEntry>();

        [Header("Talk Pool")]
        [Tooltip("Clips played during Speaking / Reacting states. At least one talk clip " +
                 "is required for the character to gesture while speaking.")]
        [SerializeField] private DialogueClipEntry[] _talks = Array.Empty<DialogueClipEntry>();

        [Header("Blending")]
        [Tooltip("Default crossfade duration in seconds, used when individual clip " +
                 "entries do not override it. Longer values yield smoother variant " +
                 "transitions at the cost of responsiveness.")]
        [Range(0.05f, 2.5f)]
        [SerializeField] private float _defaultCrossFadeDuration = 0.75f;

        /// <inheritdoc />
        public IReadOnlyList<DialogueClipEntry> IdleEntries => _idles ?? (IReadOnlyList<DialogueClipEntry>)Array.Empty<DialogueClipEntry>();

        /// <inheritdoc />
        public IReadOnlyList<DialogueClipEntry> TalkEntries => _talks ?? (IReadOnlyList<DialogueClipEntry>)Array.Empty<DialogueClipEntry>();

        /// <inheritdoc />
        public float DefaultCrossFadeDuration => Mathf.Clamp(_defaultCrossFadeDuration, 0.05f, 2.5f);

        /// <summary>
        ///     Convenience check used by the controller/editor validator to flag libraries
        ///     whose pools are empty or contain only null-clip entries.
        /// </summary>
        public bool HasAnyValidIdle() => HasAnyValid(_idles);

        /// <summary>See <see cref="HasAnyValidIdle" />.</summary>
        public bool HasAnyValidTalk() => HasAnyValid(_talks);

        private static bool HasAnyValid(DialogueClipEntry[] entries)
        {
            if (entries == null) return false;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].IsValid) return true;
            return false;
        }
    }

}
