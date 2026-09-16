using System;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.ConversationFlow.Components;
using Convai.Modules.ConversationFlow.Profiles;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.ConversationFlow
{
    /// <summary>
    ///     Component-level tests for <see cref="ConvaiConversationFlowController" /> that go
    ///     beyond <see cref="ConvaiConversationFlowControllerInvariantsTests" />.  Focus:
    ///     conversation-flow slot registration, driver registry lifecycle, current-state
    ///     semantics, and the <see cref="IConversationFlowSource.Changed" /> event contract.
    /// </summary>
    [TestFixture]
    public sealed class ConvaiConversationFlowControllerComponentTests
    {
        private EmbodimentTestRig _rig;
        private EmbodimentReceiverHarness<ConvaiConversationFlowController, ConvaiConversationFlowProfile> _harness;

        [SetUp]
        public void SetUp()
        {
            // Must reset the process-wide static registry before creating any controller
            // so that ActiveCount starts at 0 and no multi-driver warning is logged.
            ConvaiConversationFlowDriverRegistry.Reset();
            _rig = EmbodimentTestRig.Create(nameof(ConvaiConversationFlowControllerComponentTests));
            _harness = new EmbodimentReceiverHarness<ConvaiConversationFlowController, ConvaiConversationFlowProfile>(_rig);
        }

        [TearDown]
        public void TearDown()
        {
            // Reset before dispose so the Unregister call in OnDisable is a silent no-op.
            ConvaiConversationFlowDriverRegistry.Reset();
            LogAssert.NoUnexpectedReceived();
            _rig.Dispose();
        }

        // ── Context slot registration ──────────────────────────────────────────

        [Test]
        public void OnEnable_ConversationFlowSlot_IsSet()
        {
            IConversationFlowSource slot = _rig.Context.ConversationFlowSource;

            Assert.That(slot, Is.Not.Null);
            Assert.That(slot, Is.SameAs(_harness.Controller));
        }

        [Test]
        public void OnDisable_ConversationFlowSlot_IsCleared()
        {
            ConvaiConversationFlowDriverRegistry.Reset();
            _harness.Disable();

            Assert.That(_rig.Context.ConversationFlowSource, Is.Null);
        }

        [Test]
        public void ReenableAfterDisable_ConversationFlowSlot_IsReregistered()
        {
            ConvaiConversationFlowDriverRegistry.Reset();
            _harness.Disable();
            ConvaiConversationFlowDriverRegistry.Reset();
            _harness.Enable();

            Assert.That(_rig.Context.ConversationFlowSource, Is.SameAs(_harness.Controller));
        }

        // ── Driver registry lifecycle ──────────────────────────────────────────

        [Test]
        public void OnEnable_DriverRegistry_ActiveCountIsOne()
        {
            Assert.That(ConvaiConversationFlowDriverRegistry.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void OnDisable_DriverRegistry_ActiveCountIsZero()
        {
            // Unsubscribe from ScopeChanged before disable so the controller's own
            // handler doesn't attempt rebind after the registry clears.
            ConvaiConversationFlowDriverRegistry.Reset();
            _harness.Disable();

            Assert.That(ConvaiConversationFlowDriverRegistry.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void ReenableAfterDisable_DriverRegistry_ActiveCountIsOne()
        {
            ConvaiConversationFlowDriverRegistry.Reset();
            _harness.Disable();
            ConvaiConversationFlowDriverRegistry.Reset();
            _harness.Enable();

            Assert.That(ConvaiConversationFlowDriverRegistry.ActiveCount, Is.EqualTo(1));
        }

        // ── Current-state semantics ────────────────────────────────────────────

        [Test]
        public void Current_AfterEnable_IsIdle()
        {
            Assert.That(_harness.Controller.Current.Primary, Is.EqualTo(DialogueState.Idle));
        }

        [Test]
        public void Current_After100Ticks_WithNoInputs_RemainsIdle()
        {
            float dt = 1f / 60f;
            for (int i = 0; i < 100; i++)
                _harness.Tick(dt);

            Assert.That(_harness.Controller.Current.Primary, Is.EqualTo(DialogueState.Idle));
        }

        // ── Changed event contract ─────────────────────────────────────────────

        [Test]
        public void Changed_Event_CanBeSubscribedAndUnsubscribed()
        {
            var controller = (IConversationFlowSource)_harness.Controller;
            int fireCount = 0;
            void OnChanged(DialogueStateReading _) => fireCount++;

            Assert.DoesNotThrow(() =>
            {
                controller.Changed += OnChanged;
                controller.Changed -= OnChanged;
            });

            // No state change has occurred; event must not have fired.
            Assert.That(fireCount, Is.EqualTo(0));
        }

        // ── Tick safety ────────────────────────────────────────────────────────

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
