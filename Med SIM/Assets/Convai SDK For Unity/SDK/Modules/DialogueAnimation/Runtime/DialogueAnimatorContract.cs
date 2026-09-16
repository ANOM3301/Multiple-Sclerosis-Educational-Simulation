using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime
{
    /// <summary>
    ///     Animator-layer, state-name, and placeholder-clip contract for dialogue animation.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DialogueAnimatorContract",
        menuName = "Convai/Embodiment/Dialogue Animator Contract",
        order = 120)]
    public sealed class DialogueAnimatorContract : ScriptableObject
    {
        /// <summary>
        ///     Default animator state and placeholder-clip names. Single source of truth
        ///     consumed by the dialogue animation runtime to avoid drift between the
        ///     controller, the contract asset, and tests.
        /// </summary>
        public static class Defaults
        {
            public const string BaseIdleStateName = "BaseIdle";

            public const string IdleOverlayStateA = "IdleOverlay_A";
            public const string IdleOverlayStateB = "IdleOverlay_B";

            public const string BodyTalkStateA = "BodyTalk_A";
            public const string BodyTalkStateB = "BodyTalk_B";

            public const string HeadTalkStateA = "HeadTalk_A";
            public const string HeadTalkStateB = "HeadTalk_B";

            public const string BasePlaceholderName = "ConvaiDialogueSlot_BaseIdle";

            public const string IdleOverlayPlaceholderA = "ConvaiDialogueSlot_IdleOverlayA";
            public const string IdleOverlayPlaceholderB = "ConvaiDialogueSlot_IdleOverlayB";

            public const string BodyTalkPlaceholderA = "ConvaiDialogueSlot_BodyTalkA";
            public const string BodyTalkPlaceholderB = "ConvaiDialogueSlot_BodyTalkB";

            public const string HeadTalkPlaceholderA = "ConvaiDialogueSlot_HeadTalkA";
            public const string HeadTalkPlaceholderB = "ConvaiDialogueSlot_HeadTalkB";
        }

        [Header("Animator Layers")]
        [SerializeField, Min(0)] private int _baseIdleLayerIndex;
        [SerializeField, Min(0)] private int _idleOverlayLayerIndex = 1;
        [SerializeField, Min(0)] private int _bodyTalkLayerIndex = 2;
        [SerializeField, Min(0)] private int _headTalkLayerIndex = 3;

        [Header("Animator State Names")]
        [SerializeField] private string _baseIdleStateName = Defaults.BaseIdleStateName;
        [SerializeField] private string _idleOverlayStateA = Defaults.IdleOverlayStateA;
        [SerializeField] private string _idleOverlayStateB = Defaults.IdleOverlayStateB;
        [SerializeField] private string _bodyTalkStateA = Defaults.BodyTalkStateA;
        [SerializeField] private string _bodyTalkStateB = Defaults.BodyTalkStateB;
        [SerializeField] private string _headTalkStateA = Defaults.HeadTalkStateA;
        [SerializeField] private string _headTalkStateB = Defaults.HeadTalkStateB;

        [Header("Placeholder Clip Names")]
        [SerializeField] private string _basePlaceholderName = Defaults.BasePlaceholderName;
        [SerializeField] private string _idleOverlayPlaceholderA = Defaults.IdleOverlayPlaceholderA;
        [SerializeField] private string _idleOverlayPlaceholderB = Defaults.IdleOverlayPlaceholderB;
        [SerializeField] private string _bodyTalkPlaceholderA = Defaults.BodyTalkPlaceholderA;
        [SerializeField] private string _bodyTalkPlaceholderB = Defaults.BodyTalkPlaceholderB;
        [SerializeField] private string _headTalkPlaceholderA = Defaults.HeadTalkPlaceholderA;
        [SerializeField] private string _headTalkPlaceholderB = Defaults.HeadTalkPlaceholderB;

        public int BaseIdleLayerIndex => _baseIdleLayerIndex;
        public int IdleOverlayLayerIndex => _idleOverlayLayerIndex;
        public int BodyTalkLayerIndex => _bodyTalkLayerIndex;
        public int HeadTalkLayerIndex => _headTalkLayerIndex;

        public string BaseIdleStateName => _baseIdleStateName;
        public string IdleOverlayStateA => _idleOverlayStateA;
        public string IdleOverlayStateB => _idleOverlayStateB;
        public string BodyTalkStateA => _bodyTalkStateA;
        public string BodyTalkStateB => _bodyTalkStateB;
        public string HeadTalkStateA => _headTalkStateA;
        public string HeadTalkStateB => _headTalkStateB;

        public string BasePlaceholderName => _basePlaceholderName;
        public string IdleOverlayPlaceholderA => _idleOverlayPlaceholderA;
        public string IdleOverlayPlaceholderB => _idleOverlayPlaceholderB;
        public string BodyTalkPlaceholderA => _bodyTalkPlaceholderA;
        public string BodyTalkPlaceholderB => _bodyTalkPlaceholderB;
        public string HeadTalkPlaceholderA => _headTalkPlaceholderA;
        public string HeadTalkPlaceholderB => _headTalkPlaceholderB;
    }
}
