namespace Convai.Domain.Embodiment.Modules
{
    /// <summary>
    ///     Canonical module identifiers used by embodiment receivers when registering profiles
    ///     with the host context. Centralizing the strings here eliminates the silent
    ///     dispatch failures caused by typos in per-component literals.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Values are <see langword="const"/> so external references continue to compile
    ///         against the literal strings; updating a constant is a binary-compatible
    ///         operation as long as the literal value is preserved.
    ///     </para>
    ///     <para>
    ///         Each id is the routing key written into a
    ///         <c>CharacterEmbodimentPreset</c> profile slot. Renaming a literal here invalidates
    ///         every preset asset referencing the old key, so prefer additive changes.
    ///     </para>
    /// </remarks>
    public static class ModuleIds
    {
        /// <summary>Routes <c>ConvaiAttentionProfile</c> to <c>ConvaiAttentionController</c>.</summary>
        public const string Attention = "convai.attention";

        /// <summary>Routes <c>ConvaiFacialAnimationProfile</c> (baked clip) to <c>ConvaiFacialClipPlayer</c>.</summary>
        public const string BakedFacialClip = "convai.baked-facial-clip";

        /// <summary>Routes <c>ConvaiConversationFlowProfile</c> to <c>ConvaiConversationFlowController</c>.</summary>
        public const string ConversationFlow = "convai.conversation-flow";

        /// <summary>Routes <c>ConvaiDialogueAnimationProfile</c> to <c>ConvaiDialogueAnimationController</c>.</summary>
        public const string DialogueAnimation = "convai.dialogue-animation";

        /// <summary>Routes <c>ConvaiEmotionProfile</c> to <c>ConvaiEmotionController</c>.</summary>
        public const string Emotion = "convai.emotion";

        /// <summary>Routes <c>ConvaiGazeEyeProfile</c> to <c>ConvaiEyeGazeActuator</c>.</summary>
        public const string GazeEye = "convai.gaze-eye";

        /// <summary>Routes <c>ConvaiGazeCoordinationProfile</c> to <c>ConvaiGazeCoordinator</c>.</summary>
        public const string GazeCoordination = "convai.gaze-coordination";

        /// <summary>Routes <c>ConvaiGazeHeadProfile</c> to <c>ConvaiHeadLookActuator</c>.</summary>
        public const string GazeHead = "convai.gaze-head";

        /// <summary>Routes <c>ConvaiRuntimeFacialClipProfile</c> to <c>ConvaiFacialClipRuntimePlayer</c>.</summary>
        public const string RuntimeFacialClip = "convai.runtime-facial-clip";
    }
}
