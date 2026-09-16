using Convai.Domain.Embodiment.Readings;
using Convai.Modules.DialogueAnimation.Components;
using Convai.Modules.DialogueAnimation.Profiles;
using Convai.Runtime.Animation;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.DialogueAnimation
{
    /// <summary>
    ///     Component-level tests for <see cref="ConvaiDialogueAnimationController" />
    ///     complementing <see cref="ConvaiDialogueAnimationControllerInvariantsTests" />.
    ///     Focus: graceful no-op behaviour without an <c>Animator</c>, public state
    ///     accessors under a null runtime, and tick safety with injected fake sources.
    /// </summary>
    [TestFixture]
    public sealed class ConvaiDialogueAnimationControllerComponentTests
    {
        private EmbodimentTestRig _rig;
        private EmbodimentReceiverHarness<ConvaiDialogueAnimationController, ConvaiDialogueAnimationProfile> _harness;

        [SetUp]
        public void SetUp()
        {
            _rig = EmbodimentTestRig.Create(nameof(ConvaiDialogueAnimationControllerComponentTests));
            _harness = new EmbodimentReceiverHarness<ConvaiDialogueAnimationController, ConvaiDialogueAnimationProfile>(_rig);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.NoUnexpectedReceived();
            _rig.Dispose();
        }

        // ── Lifecycle null-safety ──────────────────────────────────────────────

        [Test]
        public void OnEnable_WithoutAnimator_DoesNotThrow()
        {
            // Verified implicitly: the controller was already enabled in SetUp.
            // A second enable/disable cycle must also be safe.
            Assert.DoesNotThrow(() =>
            {
                _harness.Disable();
                _harness.Enable();
            });
        }

        [Test]
        public void OnDisable_WithoutAnimator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _harness.Disable());
        }

        // ── Public state accessors with no runtime ────────────────────────────

        [Test]
        public void HasValidIdleLibrary_WithNoLibrary_IsFalse()
        {
            Assert.That(_harness.Controller.HasValidIdleLibrary, Is.False);
        }

        [Test]
        public void LastIdleIndex_WithNoRuntime_IsMinusOne()
        {
            Assert.That(_harness.Controller.LastIdleIndex, Is.EqualTo(-1));
        }

        [Test]
        public void LastTalkIndex_WithNoRuntime_IsMinusOne()
        {
            Assert.That(_harness.Controller.LastTalkIndex, Is.EqualTo(-1));
        }

        [Test]
        public void TalkLayerWeights_WithNoRuntime_AreZero()
        {
            Assert.That(_harness.Controller.CurrentHeadTalkLayerWeight, Is.EqualTo(0f));
            Assert.That(_harness.Controller.CurrentBodyTalkLayerWeight, Is.EqualTo(0f));
            Assert.That(_harness.Controller.CurrentTalkLayerWeight, Is.EqualTo(0f));
        }

        // ── Tick safety ────────────────────────────────────────────────────────

        [Test]
        public void EmbodimentTick_WithNullRuntime_DoesNotThrow()
        {
            // Runtime is not built because no Animator is present; tick is gated on _runtimeBuilt.
            Assert.DoesNotThrow(() => _harness.Tick(1f / 60f));
        }

        [Test]
        public void EmbodimentTick_WithFakeFlowAndEmotionSources_DoesNotThrow()
        {
            var flow = new FakeConversationFlowSource();
            _rig.Context.RegisterConversationFlowSource(flow);

            var emotion = new FakeEmotionStateSource();
            emotion.SetEmotion("neutral", 1f);
            _rig.Context.RegisterEmotionStateSource(emotion);

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 10; i++)
                    _harness.Tick(1f / 60f);
            });
        }

        [Test]
        public void EmbodimentTick_100Ticks_DoesNotThrow()
        {
            float dt = 1f / 60f;
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 100; i++)
                    _harness.Tick(dt);
            });
        }

        [Test]
        public void EmbodimentTick_ZeroDeltaTime_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _harness.Tick(0f));
        }

        // ── Profile application ────────────────────────────────────────────────

        [Test]
        public void ApplyProfile_ValidProfile_DoesNotThrow()
        {
            ConvaiDialogueAnimationProfile profile = ConvaiDialogueAnimationProfile.CreateDefault();
            try
            {
                Assert.DoesNotThrow(() => _harness.ApplyProfile(profile));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
