using Convai.Modules.DialogueAnimation.Core;
using Convai.Modules.DialogueAnimation.Runtime;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Profiles
{
    /// <summary>
    ///     Bundles dialogue animation content, runtime tuning, animator contract, and
    ///     optional runtime dependency policy for preset-based character setup.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ConvaiDialogueAnimationProfile",
        menuName = "Convai/Embodiment/Dialogue Animation Profile",
        order = 120)]
    public sealed class ConvaiDialogueAnimationProfile : ScriptableObject
    {
        [SerializeField] private DialogueAnimationLibrary library;
        [SerializeField] private DialogueAnimationRuntimeConfig runtimeConfig;
        [SerializeField] private DialogueAnimatorContract animatorContract;
        [SerializeField] private AnimationClip foundationIdleClip;
        [SerializeField] private CharacterGender characterGender = CharacterGender.Neutral;

        [Tooltip(
            "When true, if no IConversationFlowSource is registered, the dialogue animation controller " +
            "requests the hidden ConvaiConversationFlowDriver at runtime so Speaking/Reacting states " +
            "drive talk layers. Set false when you provide your own conversation flow source or only " +
            "want Idle-phase behaviour without auto flow.")]
        [SerializeField] private bool autoCreateConversationFlow = true;

        public DialogueAnimationLibrary Library => library;
        public DialogueAnimationRuntimeConfig RuntimeConfig => runtimeConfig;
        public DialogueAnimatorContract AnimatorContract => animatorContract;
        public AnimationClip FoundationIdleClip => foundationIdleClip;
        public CharacterGender CharacterGender => characterGender;
        public bool AutoCreateConversationFlow => autoCreateConversationFlow;

        /// <summary>Runtime default when no profile asset is assigned (OwnedProfile / preset parity).</summary>
        public static ConvaiDialogueAnimationProfile CreateDefault()
        {
            ConvaiDialogueAnimationProfile instance = CreateInstance<ConvaiDialogueAnimationProfile>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }
    }
}
