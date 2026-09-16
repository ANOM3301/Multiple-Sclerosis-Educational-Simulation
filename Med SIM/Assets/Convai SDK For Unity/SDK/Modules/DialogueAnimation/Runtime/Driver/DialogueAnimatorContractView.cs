namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Resolved snapshot of the animator contract (layer indices, state names, placeholder
    ///     names) that the controller drives at runtime. Built once in <c>BuildRuntime</c> from
    ///     either an explicit <see cref="DialogueAnimatorContract" /> asset or the inspector
    ///     fallback fields, then used unchanged for the lifetime of that build.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Capturing the contract once keeps every downstream subsystem (conductor binding,
    ///         talk variant starter, idle scheduler) free of the asset-vs-inspector branch and
    ///         removes 15 ternary properties from the controller.
    ///     </para>
    /// </remarks>
    internal readonly struct DialogueAnimatorContractView
    {
        public int BaseIdleLayerIndex { get; }
        public int IdleOverlayLayerIndex { get; }
        public int BodyTalkLayerIndex { get; }
        public int HeadTalkLayerIndex { get; }

        public string BaseIdleStateName { get; }
        public string IdleOverlayStateA { get; }
        public string IdleOverlayStateB { get; }
        public string BodyTalkStateA { get; }
        public string BodyTalkStateB { get; }
        public string HeadTalkStateA { get; }
        public string HeadTalkStateB { get; }

        public string BasePlaceholderName { get; }
        public string IdleOverlayPlaceholderA { get; }
        public string IdleOverlayPlaceholderB { get; }
        public string BodyTalkPlaceholderA { get; }
        public string BodyTalkPlaceholderB { get; }
        public string HeadTalkPlaceholderA { get; }
        public string HeadTalkPlaceholderB { get; }

        private DialogueAnimatorContractView(
            int baseLayer,
            int idleOverlayLayer,
            int bodyTalkLayer,
            int headTalkLayer,
            string baseIdleState,
            string idleOverlayStateA,
            string idleOverlayStateB,
            string bodyTalkStateA,
            string bodyTalkStateB,
            string headTalkStateA,
            string headTalkStateB,
            string basePlaceholder,
            string idleOverlayPlaceholderA,
            string idleOverlayPlaceholderB,
            string bodyTalkPlaceholderA,
            string bodyTalkPlaceholderB,
            string headTalkPlaceholderA,
            string headTalkPlaceholderB)
        {
            BaseIdleLayerIndex = baseLayer;
            IdleOverlayLayerIndex = idleOverlayLayer;
            BodyTalkLayerIndex = bodyTalkLayer;
            HeadTalkLayerIndex = headTalkLayer;

            BaseIdleStateName = baseIdleState;
            IdleOverlayStateA = idleOverlayStateA;
            IdleOverlayStateB = idleOverlayStateB;
            BodyTalkStateA = bodyTalkStateA;
            BodyTalkStateB = bodyTalkStateB;
            HeadTalkStateA = headTalkStateA;
            HeadTalkStateB = headTalkStateB;

            BasePlaceholderName = basePlaceholder;
            IdleOverlayPlaceholderA = idleOverlayPlaceholderA;
            IdleOverlayPlaceholderB = idleOverlayPlaceholderB;
            BodyTalkPlaceholderA = bodyTalkPlaceholderA;
            BodyTalkPlaceholderB = bodyTalkPlaceholderB;
            HeadTalkPlaceholderA = headTalkPlaceholderA;
            HeadTalkPlaceholderB = headTalkPlaceholderB;
        }

        /// <summary>
        ///     Builds a contract view by preferring values from <paramref name="contract" /> when
        ///     non-null and falling back to the inspector-provided arguments otherwise.
        /// </summary>
        public static DialogueAnimatorContractView Resolve(
            DialogueAnimatorContract contract,
            int inspectorBaseIdleLayer,
            int inspectorIdleOverlayLayer,
            int inspectorBodyTalkLayer,
            int inspectorHeadTalkLayer,
            string inspectorBaseIdleState,
            string inspectorIdleOverlayStateA,
            string inspectorIdleOverlayStateB,
            string inspectorBodyTalkStateA,
            string inspectorBodyTalkStateB,
            string inspectorHeadTalkStateA,
            string inspectorHeadTalkStateB,
            string inspectorBasePlaceholder,
            string inspectorIdleOverlayPlaceholderA,
            string inspectorIdleOverlayPlaceholderB,
            string inspectorBodyTalkPlaceholderA,
            string inspectorBodyTalkPlaceholderB,
            string inspectorHeadTalkPlaceholderA,
            string inspectorHeadTalkPlaceholderB)
        {
            if (contract != null)
            {
                return new DialogueAnimatorContractView(
                    contract.BaseIdleLayerIndex,
                    contract.IdleOverlayLayerIndex,
                    contract.BodyTalkLayerIndex,
                    contract.HeadTalkLayerIndex,
                    contract.BaseIdleStateName,
                    contract.IdleOverlayStateA,
                    contract.IdleOverlayStateB,
                    contract.BodyTalkStateA,
                    contract.BodyTalkStateB,
                    contract.HeadTalkStateA,
                    contract.HeadTalkStateB,
                    contract.BasePlaceholderName,
                    contract.IdleOverlayPlaceholderA,
                    contract.IdleOverlayPlaceholderB,
                    contract.BodyTalkPlaceholderA,
                    contract.BodyTalkPlaceholderB,
                    contract.HeadTalkPlaceholderA,
                    contract.HeadTalkPlaceholderB);
            }

            return new DialogueAnimatorContractView(
                inspectorBaseIdleLayer,
                inspectorIdleOverlayLayer,
                inspectorBodyTalkLayer,
                inspectorHeadTalkLayer,
                inspectorBaseIdleState,
                inspectorIdleOverlayStateA,
                inspectorIdleOverlayStateB,
                inspectorBodyTalkStateA,
                inspectorBodyTalkStateB,
                inspectorHeadTalkStateA,
                inspectorHeadTalkStateB,
                inspectorBasePlaceholder,
                inspectorIdleOverlayPlaceholderA,
                inspectorIdleOverlayPlaceholderB,
                inspectorBodyTalkPlaceholderA,
                inspectorBodyTalkPlaceholderB,
                inspectorHeadTalkPlaceholderA,
                inspectorHeadTalkPlaceholderB);
        }

        public DialogueAnimatorLayerSet ToLayerSet() =>
            new(BaseIdleLayerIndex, IdleOverlayLayerIndex, BodyTalkLayerIndex, HeadTalkLayerIndex);
    }
}
