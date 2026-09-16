using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.Gaze.Components;
using Convai.Modules.Gaze.Profiles;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Gaze
{
    /// <summary>
    ///     Component-level tests for <see cref="ConvaiGazeCoordinator" /> complementing the
    ///     <see cref="ConvaiGazeCoordinatorInvariantsTests" />.  Focus: gaze intent slot
    ///     registration, state after enable/disable, and tick interactions with injected
    ///     fake sources.
    /// </summary>
    [TestFixture]
    public sealed class ConvaiGazeCoordinatorComponentTests
    {
        private EmbodimentTestRig _rig;
        private EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile> _harness;

        [SetUp]
        public void SetUp()
        {
            _rig = EmbodimentTestRig.Create(nameof(ConvaiGazeCoordinatorComponentTests));
            _harness = new EmbodimentReceiverHarness<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>(_rig);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.NoUnexpectedReceived();
            _rig.Dispose();
        }

        // ── Slot registration ──────────────────────────────────────────────────

        [Test]
        public void OnEnable_GazeIntentSlot_IsSet()
        {
            IGazeIntentProvider slot = _rig.Context.GazeIntentProvider;

            Assert.That(slot, Is.Not.Null);
            Assert.That(slot, Is.SameAs(_harness.Controller));
        }

        [Test]
        public void OnDisable_GazeIntentSlot_IsCleared()
        {
            _harness.Disable();

            Assert.That(_rig.Context.GazeIntentProvider, Is.Null);
        }

        [Test]
        public void ReenableAfterDisable_GazeIntentSlot_IsReregistered()
        {
            _harness.Disable();
            _harness.Enable();

            Assert.That(_rig.Context.GazeIntentProvider, Is.SameAs(_harness.Controller));
        }

        // ── Current-state semantics ────────────────────────────────────────────

        [Test]
        public void Current_AfterEnable_IsRelaxed()
        {
            Assert.That(_harness.Controller.Current, Is.EqualTo(GazeIntent.Relaxed));
        }

        [Test]
        public void OnDisable_Current_IsRelaxed()
        {
            // Seed a non-relaxed state by ticking with a valid attention source.
            var fakeAttention = new FakeAttentionSource();
            fakeAttention.SetValid(null, new Vector3(0f, 1.6f, 2f), commitment: 1f);
            _rig.Context.RegisterAttentionSource(fakeAttention);

            for (int i = 0; i < 30; i++)
                _harness.Tick(1f / 60f);

            _harness.Disable();

            Assert.That(_harness.Controller.Current, Is.EqualTo(GazeIntent.Relaxed));
        }

        // ── Tick interactions ──────────────────────────────────────────────────

        [Test]
        public void EmbodimentTick_WithNoAttentionSource_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _harness.Tick(1f / 60f));
        }

        [Test]
        public void EmbodimentTick_WithFakeAttentionSource_DoesNotThrow()
        {
            var fakeAttention = new FakeAttentionSource();
            fakeAttention.SetValid(null, new Vector3(0f, 1.6f, 2f), commitment: 1f);
            _rig.Context.RegisterAttentionSource(fakeAttention);

            Assert.DoesNotThrow(() => _harness.Tick(1f / 60f));
        }

        [Test]
        public void EmbodimentTick_WithFakeFlowSource_DoesNotThrow()
        {
            var fakeFlow = new FakeConversationFlowSource();
            _rig.Context.RegisterConversationFlowSource(fakeFlow);

            Assert.DoesNotThrow(() => _harness.Tick(1f / 60f));
        }

        [Test]
        public void EmbodimentTick_ValidAttentionSource_OverallWeightGrowsOverTime()
        {
            // Arrange — full commitment from a valid source at a plausible head-level point.
            var fakeAttention = new FakeAttentionSource();
            fakeAttention.SetValid(null, new Vector3(0f, 1.6f, 2f), commitment: 1f);
            _rig.Context.RegisterAttentionSource(fakeAttention);

            // Idle state has SuppressAttentionTarget=true; use Attending so the coordinator
            // does not zero targetOverall before applying commitment.
            var fakeFlow = new FakeConversationFlowSource();
            fakeFlow.SetState(DialogueState.Attending);
            _rig.Context.RegisterConversationFlowSource(fakeFlow);

            // Seed the coordinator for several frames; the exponential blend ensures
            // weight converges toward the target but is strictly positive after at least
            // one tick of a profile with a non-zero blend speed.
            for (int i = 0; i < 10; i++)
                _harness.Tick(1f / 60f);

            float weight = _harness.Controller.Current.OverallWeight;

            Assert.That(weight, Is.GreaterThan(0f),
                "OverallWeight must be > 0 after several ticks with a valid committed attention source.");
        }

        // ── Stability ──────────────────────────────────────────────────────────

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
    }
}
