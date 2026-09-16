using Convai.Modules.DialogueAnimation.Runtime;
using Convai.Modules.DialogueAnimation.Runtime.Driver;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.DialogueAnimation
{
    /// <summary>
    ///     Verifies the contract-vs-inspector resolution order of
    ///     <see cref="DialogueAnimatorContractView" /> so the controller can collapse 15 ternary
    ///     fallbacks into a single snapshot.
    /// </summary>
    public sealed class DialogueAnimatorContractViewTests
    {
        [Test]
        public void Resolve_NullContract_UsesInspectorFallbacks()
        {
            DialogueAnimatorContractView view = DialogueAnimatorContractView.Resolve(
                contract: null,
                inspectorBaseIdleLayer: 0,
                inspectorIdleOverlayLayer: 1,
                inspectorBodyTalkLayer: 2,
                inspectorHeadTalkLayer: 3,
                inspectorBaseIdleState: "BaseIdle",
                inspectorIdleOverlayStateA: "IdleOverlayA",
                inspectorIdleOverlayStateB: "IdleOverlayB",
                inspectorBodyTalkStateA: "BodyTalkA",
                inspectorBodyTalkStateB: "BodyTalkB",
                inspectorHeadTalkStateA: "HeadA",
                inspectorHeadTalkStateB: "HeadB",
                inspectorBasePlaceholder: "BasePlaceholder",
                inspectorIdleOverlayPlaceholderA: "IdleOverlayPlaceholderA",
                inspectorIdleOverlayPlaceholderB: "IdleOverlayPlaceholderB",
                inspectorBodyTalkPlaceholderA: "BodyTalkPlaceholderA",
                inspectorBodyTalkPlaceholderB: "BodyTalkPlaceholderB",
                inspectorHeadTalkPlaceholderA: "HeadPlaceholderA",
                inspectorHeadTalkPlaceholderB: "HeadPlaceholderB");

            Assert.AreEqual(0, view.BaseIdleLayerIndex);
            Assert.AreEqual(1, view.IdleOverlayLayerIndex);
            Assert.AreEqual(2, view.BodyTalkLayerIndex);
            Assert.AreEqual(3, view.HeadTalkLayerIndex);

            Assert.AreEqual("BaseIdle", view.BaseIdleStateName);
            Assert.AreEqual("IdleOverlayA", view.IdleOverlayStateA);
            Assert.AreEqual("HeadPlaceholderB", view.HeadTalkPlaceholderB);
        }

        [Test]
        public void Resolve_WithContract_PrefersContractValues()
        {
            DialogueAnimatorContract contract = ScriptableObject.CreateInstance<DialogueAnimatorContract>();
            try
            {
                DialogueAnimatorContractView view = DialogueAnimatorContractView.Resolve(
                    contract,
                    inspectorBaseIdleLayer: 99,
                    inspectorIdleOverlayLayer: 99,
                    inspectorBodyTalkLayer: 99,
                    inspectorHeadTalkLayer: 99,
                    inspectorBaseIdleState: "ShouldNotUse",
                    inspectorIdleOverlayStateA: "ShouldNotUse",
                    inspectorIdleOverlayStateB: "ShouldNotUse",
                    inspectorBodyTalkStateA: "ShouldNotUse",
                    inspectorBodyTalkStateB: "ShouldNotUse",
                    inspectorHeadTalkStateA: "ShouldNotUse",
                    inspectorHeadTalkStateB: "ShouldNotUse",
                    inspectorBasePlaceholder: "ShouldNotUse",
                    inspectorIdleOverlayPlaceholderA: "ShouldNotUse",
                    inspectorIdleOverlayPlaceholderB: "ShouldNotUse",
                    inspectorBodyTalkPlaceholderA: "ShouldNotUse",
                    inspectorBodyTalkPlaceholderB: "ShouldNotUse",
                    inspectorHeadTalkPlaceholderA: "ShouldNotUse",
                    inspectorHeadTalkPlaceholderB: "ShouldNotUse");

                Assert.AreEqual(contract.BaseIdleLayerIndex, view.BaseIdleLayerIndex);
                Assert.AreEqual(contract.IdleOverlayLayerIndex, view.IdleOverlayLayerIndex);
                Assert.AreEqual(contract.BodyTalkLayerIndex, view.BodyTalkLayerIndex);
                Assert.AreEqual(contract.HeadTalkLayerIndex, view.HeadTalkLayerIndex);
                Assert.AreEqual(contract.BaseIdleStateName, view.BaseIdleStateName);
                Assert.AreEqual(contract.BasePlaceholderName, view.BasePlaceholderName);
                Assert.AreNotEqual("ShouldNotUse", view.HeadTalkPlaceholderB);
            }
            finally
            {
                Object.DestroyImmediate(contract);
            }
        }

        [Test]
        public void ToLayerSet_RoundTripsLayerIndices()
        {
            DialogueAnimatorContractView view = DialogueAnimatorContractView.Resolve(
                contract: null,
                inspectorBaseIdleLayer: 0,
                inspectorIdleOverlayLayer: 1,
                inspectorBodyTalkLayer: 2,
                inspectorHeadTalkLayer: 3,
                inspectorBaseIdleState: "x",
                inspectorIdleOverlayStateA: "x",
                inspectorIdleOverlayStateB: "x",
                inspectorBodyTalkStateA: "x",
                inspectorBodyTalkStateB: "x",
                inspectorHeadTalkStateA: "x",
                inspectorHeadTalkStateB: "x",
                inspectorBasePlaceholder: "x",
                inspectorIdleOverlayPlaceholderA: "x",
                inspectorIdleOverlayPlaceholderB: "x",
                inspectorBodyTalkPlaceholderA: "x",
                inspectorBodyTalkPlaceholderB: "x",
                inspectorHeadTalkPlaceholderA: "x",
                inspectorHeadTalkPlaceholderB: "x");

            DialogueAnimatorLayerSet layers = view.ToLayerSet();

            Assert.AreEqual(0, layers.BaseIdle);
            Assert.AreEqual(1, layers.IdleOverlay);
            Assert.AreEqual(2, layers.BodyTalk);
            Assert.AreEqual(3, layers.HeadTalk);
        }
    }
}
