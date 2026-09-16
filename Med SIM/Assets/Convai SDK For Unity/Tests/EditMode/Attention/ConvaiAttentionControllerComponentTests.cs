using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using Convai.Modules.Attention.Components;
using Convai.Modules.Attention.Profiles;
using Convai.Runtime.Embodiment;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Attention
{
    /// <summary>
    ///     Component-level tests for <see cref="ConvaiAttentionController" /> that go
    ///     beyond the invariants already verified by
    ///     <see cref="ConvaiAttentionControllerInvariantsTests" />.  Focus: module-specific
    ///     slot registration, current-state semantics, and null-safety.
    /// </summary>
    [TestFixture]
    public sealed class ConvaiAttentionControllerComponentTests
    {
        private EmbodimentTestRig _rig;
        private EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile> _harness;

        [SetUp]
        public void SetUp()
        {
            _rig = EmbodimentTestRig.Create(nameof(ConvaiAttentionControllerComponentTests));
            _harness = new EmbodimentReceiverHarness<ConvaiAttentionController, ConvaiAttentionProfile>(_rig);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.NoUnexpectedReceived();
            _rig.Dispose();
        }

        // ── Slot registration ──────────────────────────────────────────────────

        [Test]
        public void OnEnable_AttentionSourceSlot_IsSet()
        {
            IAttentionSource slot = _rig.Context.AttentionSource;

            Assert.That(slot, Is.Not.Null);
            Assert.That(slot, Is.SameAs(_harness.Controller));
        }

        [Test]
        public void OnDisable_AttentionSourceSlot_IsCleared()
        {
            _harness.Disable();

            Assert.That(_rig.Context.AttentionSource, Is.Null);
        }

        [Test]
        public void ReenableAfterDisable_AttentionSourceSlot_IsReregistered()
        {
            _harness.Disable();
            _harness.Enable();

            Assert.That(_rig.Context.AttentionSource, Is.SameAs(_harness.Controller));
        }

        // ── Current-state semantics ────────────────────────────────────────────

        [Test]
        public void Current_BeforeFirstTick_IsInvalid()
        {
            Assert.That(_harness.Controller.Current.IsValid, Is.False);
        }

        [Test]
        public void EmbodimentTick_WithNoProviders_CurrentRemainsInvalid()
        {
            _harness.Tick(1f / 60f);

            Assert.That(_harness.Controller.Current.IsValid, Is.False);
        }

        [Test]
        public void OnDisable_Current_EqualsEmptyReading()
        {
            _harness.Tick(1f / 60f);
            _harness.Disable();

            Assert.That(_harness.Controller.Current, Is.EqualTo(AttentionReading.Empty));
        }

        // ── Profile application ────────────────────────────────────────────────

        [Test]
        public void OnProfileApplied_ValidProfile_CurrentRemainsInvalid()
        {
            // Ticking before apply ensures director has run at least once.
            _harness.Tick(1f / 60f);

            ConvaiAttentionProfile profile = ConvaiAttentionProfile.CreateDefault();
            try
            {
                _harness.ApplyProfile(profile);

                Assert.That(_harness.Controller.Current.IsValid, Is.False,
                    "Applying a profile should reset the director; prior invalid state preserved.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        // ── Provider refresh ───────────────────────────────────────────────────

        [Test]
        public void RefreshProviders_WhenEnabled_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _harness.Controller.RefreshProviders());
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

        [Test]
        public void EmbodimentTick_ZeroDeltaTime_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _harness.Tick(0f));
        }
    }
}
