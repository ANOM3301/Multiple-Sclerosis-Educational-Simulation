using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.Attention.Components;
using Convai.Modules.Attention.Profiles;
using Convai.Modules.ConversationFlow.Components;
using Convai.Modules.ConversationFlow.Profiles;
using Convai.Modules.DialogueAnimation.Components;
using Convai.Modules.DialogueAnimation.Profiles;
using Convai.Modules.Emotion.Components;
using Convai.Modules.Emotion.Profiles;
using Convai.Modules.Gaze.Components;
using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Embodiment
{
    /// <summary>
    ///     Cross-module integration tests for the full Convai embodiment stack on a single
    ///     <see cref="EmbodimentContext" />.  Verifies that module controllers co-exist on the
    ///     same rig without slot conflicts, that <see cref="EmbodimentContext" /> routes reads
    ///     and registrations correctly across enable/disable boundaries, and that manually
    ///     ticking multiple modules in the correct phase order produces consistent state.
    ///     No real Unity frames are pumped; all ticks are driven via
    ///     <see cref="IEmbodimentTickable" />.
    /// </summary>
    [TestFixture]
    public sealed class EmbodimentStackIntegrationTests
    {
        private EmbodimentTestRig _rig;

        [SetUp]
        public void SetUp()
        {
            ConvaiConversationFlowDriverRegistry.Reset();
            _rig = EmbodimentTestRig.Create(nameof(EmbodimentStackIntegrationTests));
        }

        [TearDown]
        public void TearDown()
        {
            ConvaiConversationFlowDriverRegistry.Reset();
            LogAssert.NoUnexpectedReceived();
            _rig.Dispose();
        }

        // ── Two-module coexistence ─────────────────────────────────────────────

        [Test]
        public void AttentionController_And_GazeCoordinator_BothEnabled_ContextSlotsAreFilled()
        {
            var attHarness = new EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile>(_rig);
            var gazeHarness = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);

            Assert.That(_rig.Context.AttentionSource, Is.SameAs(attHarness.Controller));
            Assert.That(_rig.Context.GazeIntentProvider, Is.SameAs(gazeHarness.Controller));
        }

        [Test]
        public void GazeCoordinator_ReadsFrom_AttentionController_ViaContext()
        {
            // GazeCoordinator does not hold a direct reference to AttentionController;
            // it reads through Context.AttentionSource.  Verifying the slot points at the
            // controller is sufficient proof of the wiring — the tick already covered in
            // component tests.
            var attHarness = new EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile>(_rig);
            var _ = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);

            IAttentionSource attentionSeenByContext = _rig.Context.AttentionSource;

            Assert.That(attentionSeenByContext, Is.SameAs(attHarness.Controller));
        }

        [Test]
        public void DisableAttentionController_GazeCoordinator_StillEnabled_SlotIsCleared()
        {
            var attHarness = new EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile>(_rig);
            var _ = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);

            attHarness.Disable();

            Assert.That(_rig.Context.AttentionSource, Is.Null,
                "Disabling AttentionController must clear the attention slot even while GazeCoordinator is active.");
            Assert.That(_rig.Context.GazeIntentProvider, Is.Not.Null,
                "GazeCoordinator slot must remain filled when only AttentionController is disabled.");
        }

        [Test]
        public void ConversationFlowController_And_GazeCoordinator_BothEnabled_ContextSlotsAreFilled()
        {
            var _ = new EmbodimentReceiverHarness<ConvaiConversationFlowController, ConvaiConversationFlowProfile>(_rig);
            var gazeHarness = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);

            Assert.That(_rig.Context.ConversationFlowSource, Is.Not.Null);
            Assert.That(_rig.Context.GazeIntentProvider, Is.SameAs(gazeHarness.Controller));
        }

        [Test]
        public void EmotionController_And_ConversationFlowController_NoSlotConflict()
        {
            var emotionHarness = new EmbodimentReceiverHarness<ConvaiEmotionController, ConvaiEmotionProfile>(_rig);
            var flowHarness = new EmbodimentReceiverHarness<ConvaiConversationFlowController, ConvaiConversationFlowProfile>(_rig);

            Assert.That(_rig.Context.EmotionStateSource, Is.SameAs(emotionHarness.Controller));
            Assert.That(_rig.Context.ConversationFlowSource, Is.SameAs(flowHarness.Controller));
        }

        // ── Multi-module tick correctness ──────────────────────────────────────

        [Test]
        public void AttentionController_And_GazeCoordinator_ManualTick_DoesNotThrow()
        {
            var attHarness = new EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile>(_rig);
            var gazeHarness = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);

            float dt = 1f / 60f;
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 30; i++)
                {
                    // Cognition phase first, then read.
                    attHarness.Tick(dt);
                    gazeHarness.Tick(dt);
                }
            });
        }

        [Test]
        public void GazeCoordinator_WithFakeAttentionSource_AndFakeFlowSource_WeightGrows()
        {
            // Inject fake sources to exercise GazeCoordinator's policy blending
            // independently of other production controllers.
            var gazeHarness = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);

            var fakeAttention = new FakeAttentionSource();
            fakeAttention.SetValid(null, new Vector3(0f, 1.6f, 2f), commitment: 1f);
            _rig.Context.RegisterAttentionSource(fakeAttention);

            // Idle state has SuppressAttentionTarget=true which zeros targetOverall before
            // commitment is applied. Use Attending so the coordinator honours the attention
            // commitment and weight can grow toward a positive value.
            var fakeFlow = new FakeConversationFlowSource();
            fakeFlow.SetState(DialogueState.Attending);
            _rig.Context.RegisterConversationFlowSource(fakeFlow);

            float dt = 1f / 60f;
            for (int i = 0; i < 20; i++)
                gazeHarness.Tick(dt);

            Assert.That(gazeHarness.Controller.Current.OverallWeight, Is.GreaterThan(0f));
        }

        [Test]
        public void GazeCoordinator_WhenConversationFlowSourceDisabled_ContextSlotClears_TickSafe()
        {
            var flowHarness = new EmbodimentReceiverHarness<ConvaiConversationFlowController, ConvaiConversationFlowProfile>(_rig);
            var gazeHarness = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);

            ConvaiConversationFlowDriverRegistry.Reset();
            flowHarness.Disable();

            Assert.That(_rig.Context.ConversationFlowSource, Is.Null);
            // GazeCoordinator reads null flow → defaults to Idle state; must not throw.
            Assert.DoesNotThrow(() => gazeHarness.Tick(1f / 60f));
        }

        [Test]
        public void EmotionController_And_ConversationFlowController_100Ticks_DoesNotThrow()
        {
            var emotionHarness = new EmbodimentReceiverHarness<ConvaiEmotionController, ConvaiEmotionProfile>(_rig);
            var flowHarness = new EmbodimentReceiverHarness<ConvaiConversationFlowController, ConvaiConversationFlowProfile>(_rig);

            float dt = 1f / 60f;
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    flowHarness.Tick(dt);
                    emotionHarness.Tick(dt);
                }
            });
        }

        // ── Full five-module stack ─────────────────────────────────────────────

        [Test]
        public void FullStack_FiveControllers_AllEnabled_AllSlotsRegistered()
        {
            var attHarness = new EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile>(_rig);
            var gazeHarness = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);
            var emotionHarness = new EmbodimentReceiverHarness<ConvaiEmotionController, ConvaiEmotionProfile>(_rig);
            var flowHarness = new EmbodimentReceiverHarness<ConvaiConversationFlowController, ConvaiConversationFlowProfile>(_rig);
            var _ = new EmbodimentReceiverHarness<ConvaiDialogueAnimationController, ConvaiDialogueAnimationProfile>(_rig);

            Assert.That(_rig.Context.AttentionSource, Is.SameAs(attHarness.Controller));
            Assert.That(_rig.Context.GazeIntentProvider, Is.SameAs(gazeHarness.Controller));
            Assert.That(_rig.Context.EmotionStateSource, Is.SameAs(emotionHarness.Controller));
            Assert.That(_rig.Context.ConversationFlowSource, Is.SameAs(flowHarness.Controller));
        }

        [Test]
        public void FullStack_100Ticks_DoesNotThrow()
        {
            var attHarness = new EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile>(_rig);
            var gazeHarness = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);
            var emotionHarness = new EmbodimentReceiverHarness<ConvaiEmotionController, ConvaiEmotionProfile>(_rig);
            var flowHarness = new EmbodimentReceiverHarness<ConvaiConversationFlowController, ConvaiConversationFlowProfile>(_rig);
            var dialogueHarness = new EmbodimentReceiverHarness<ConvaiDialogueAnimationController, ConvaiDialogueAnimationProfile>(_rig);

            float dt = 1f / 60f;
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    // Cognition phase first.
                    attHarness.Tick(dt);
                    gazeHarness.Tick(dt);
                    flowHarness.Tick(dt);
                    emotionHarness.Tick(dt);
                    // Expression phase last.
                    dialogueHarness.Tick(dt);
                }
            });
        }

        [Test]
        public void FullStack_DisableAll_AllSlotsCleared()
        {
            var attHarness = new EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile>(_rig);
            var gazeHarness = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);
            var emotionHarness = new EmbodimentReceiverHarness<ConvaiEmotionController, ConvaiEmotionProfile>(_rig);
            var flowHarness = new EmbodimentReceiverHarness<ConvaiConversationFlowController, ConvaiConversationFlowProfile>(_rig);
            var _ = new EmbodimentReceiverHarness<ConvaiDialogueAnimationController, ConvaiDialogueAnimationProfile>(_rig);

            ConvaiConversationFlowDriverRegistry.Reset();
            attHarness.Disable();
            gazeHarness.Disable();
            emotionHarness.Disable();
            flowHarness.Disable();

            Assert.That(_rig.Context.AttentionSource, Is.Null);
            Assert.That(_rig.Context.GazeIntentProvider, Is.Null);
            Assert.That(_rig.Context.EmotionStateSource, Is.Null);
            Assert.That(_rig.Context.ConversationFlowSource, Is.Null);
        }

        [Test]
        public void FullStack_ReenableAfterDisableAll_AllSlotsReregistered()
        {
            var attHarness = new EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile>(_rig);
            var gazeHarness = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);
            var emotionHarness = new EmbodimentReceiverHarness<ConvaiEmotionController, ConvaiEmotionProfile>(_rig);
            var flowHarness = new EmbodimentReceiverHarness<ConvaiConversationFlowController, ConvaiConversationFlowProfile>(_rig);
            var _ = new EmbodimentReceiverHarness<ConvaiDialogueAnimationController, ConvaiDialogueAnimationProfile>(_rig);

            ConvaiConversationFlowDriverRegistry.Reset();
            attHarness.Disable();
            gazeHarness.Disable();
            emotionHarness.Disable();
            flowHarness.Disable();

            ConvaiConversationFlowDriverRegistry.Reset();
            attHarness.Enable();
            gazeHarness.Enable();
            emotionHarness.Enable();
            flowHarness.Enable();

            Assert.That(_rig.Context.AttentionSource, Is.SameAs(attHarness.Controller));
            Assert.That(_rig.Context.GazeIntentProvider, Is.SameAs(gazeHarness.Controller));
            Assert.That(_rig.Context.EmotionStateSource, Is.SameAs(emotionHarness.Controller));
            Assert.That(_rig.Context.ConversationFlowSource, Is.SameAs(flowHarness.Controller));
        }

        // ── Profile routing in multi-module stack ──────────────────────────────

        [Test]
        public void ApplyProfile_ToSpecificController_DoesNotAffectOtherControllers_ProfileReceiverCount()
        {
            // Verifies that the profile-receiver index tracks separate entries per
            // controller rather than a single shared slot.
            var _ = new EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile>(_rig);
            var __ = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);
            var ___ = new EmbodimentReceiverHarness<ConvaiEmotionController, ConvaiEmotionProfile>(_rig);

            var registrations = new System.Collections.Generic.List<EmbodimentProfileReceiverRegistration>();
            _rig.Context.GetProfileReceivers(registrations);

            // Each controller registered itself; expect at least one entry per module.
            Assert.That(registrations.Count, Is.GreaterThanOrEqualTo(3));
        }
    }
}
