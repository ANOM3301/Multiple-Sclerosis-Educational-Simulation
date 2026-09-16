using System.Collections.Generic;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using Convai.Domain.DomainEvents.Runtime;
using Convai.Modules.Emotion.Components;
using Convai.Modules.Emotion.Profiles;
using Convai.Runtime.Components;
using Convai.Runtime.Animation;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace Convai.Tests.EditMode.Emotion
{
    /// <summary>
    ///     Component-level tests for <see cref="ConvaiEmotionController" /> complementing
    ///     <see cref="ConvaiEmotionControllerInvariantsTests" />.  Focus: dual-slot registration
    ///     (emotion state + mouth provider), initial state, and tick null-safety.
    /// </summary>
    [TestFixture]
    public sealed class ConvaiEmotionControllerComponentTests
    {
        private const string CharacterId = "test-char-id";

        private EmbodimentTestRig _rig;
        private EmbodimentReceiverHarness<ConvaiEmotionController, ConvaiEmotionProfile> _harness;

        [SetUp]
        public void SetUp()
        {
            _rig = EmbodimentTestRig.Create(nameof(ConvaiEmotionControllerComponentTests));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ConvaiManager not found in scene"));
            ConvaiCharacter character = _rig.Root.AddComponent<ConvaiCharacter>();
            character.Configure(CharacterId, "Test Character");
            _harness = new EmbodimentReceiverHarness<ConvaiEmotionController, ConvaiEmotionProfile>(_rig);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.NoUnexpectedReceived();
            _rig.Dispose();
        }

        // ── Slot registration ──────────────────────────────────────────────────

        [Test]
        public void OnEnable_EmotionStateSlot_IsSet()
        {
            IEmotionStateSource slot = _rig.Context.EmotionStateSource;

            Assert.That(slot, Is.Not.Null);
            Assert.That(slot, Is.SameAs(_harness.Controller));
        }

        [Test]
        public void OnEnable_EmotionMouthSlot_IsSet()
        {
            // The controller always registers as mouth provider even without a
            // blendshape binding (returns zero weight in that case).
            IEmotionMouthWeightProvider mouthSlot = _rig.Context.EmotionMouthProvider;

            Assert.That(mouthSlot, Is.Not.Null);
            Assert.That(mouthSlot, Is.SameAs(_harness.Controller));
        }

        [Test]
        public void OnDisable_EmotionStateSlot_IsCleared()
        {
            _harness.Disable();

            Assert.That(_rig.Context.EmotionStateSource, Is.Null);
        }

        [Test]
        public void OnDisable_EmotionMouthSlot_IsCleared()
        {
            _harness.Disable();

            Assert.That(_rig.Context.EmotionMouthProvider, Is.Null);
        }

        [Test]
        public void ReenableAfterDisable_BothSlots_AreReregistered()
        {
            _harness.Disable();
            _harness.Enable();

            Assert.That(_rig.Context.EmotionStateSource, Is.SameAs(_harness.Controller));
            Assert.That(_rig.Context.EmotionMouthProvider, Is.SameAs(_harness.Controller));
        }

        // ── Current-state semantics ────────────────────────────────────────────

        [Test]
        public void Current_AfterEnable_IsDominantNeutral()
        {
            // Without server events the accumulator settles on neutral.
            EmotionReading reading = _harness.Controller.Current;

            Assert.That(reading.DominantLabel,
                Is.EqualTo(EmotionReading.Neutral.DominantLabel),
                "Default dominant emotion must be 'neutral' before any server events.");
        }

        [Test]
        public void Current_AfterTick_IsDominantNeutral()
        {
            _harness.Tick(1f / 60f);

            Assert.That(_harness.Controller.Current.DominantLabel,
                Is.EqualTo(EmotionReading.Neutral.DominantLabel));
        }

        [Test]
        public void ScoreAccess_UsesSourceReading_NotStaleSnapshot()
        {
            _harness.Controller.LockEmotion("joy", 0.75f);
            _harness.Tick(1f / 60f);

            IEmotionStateSource source = _harness.Controller;
            var destination = new Dictionary<string, float> { ["stale"] = 1f };

            float score = source.Current.GetScore("joy");
            source.Current.CopyScoresTo(destination);

            Assert.That(score, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(destination.ContainsKey("stale"), Is.False);
            Assert.That(destination["joy"], Is.EqualTo(0.75f).Within(1e-6f));
        }

        [Test]
        public void CurrentResolvedState_UsesComposedReading()
        {
            _harness.Controller.LockEmotion("joy", 0.75f);
            _harness.Tick(1f / 60f);

            Assert.That(_harness.Controller.CurrentResolvedEmotion, Is.EqualTo("joy"));
            Assert.That(_harness.Controller.CurrentNormalizedIntensity, Is.EqualTo(0.75f).Within(1e-6f));
        }

        [Test]
        public void UnknownServerEmotion_LogsWarningAndFallsBackToNeutral()
        {
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("Unknown backend emotion label.*confused-but-not-taxonomy"));

            _rig.EventHub.Publish(CharacterEmotionChanged.Create(CharacterId, "confused-but-not-taxonomy", 2));

            Assert.That(_harness.Controller.Current.IsNeutral, Is.True);
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

        // ── Profile application ────────────────────────────────────────────────

        [Test]
        public void ApplyProfile_ValidProfile_DoesNotThrow()
        {
            ConvaiEmotionProfile profile = ConvaiEmotionProfile.CreateDefault();
            try
            {
                Assert.DoesNotThrow(() => _harness.ApplyProfile(profile));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ApplyProfile_AfterReenable_DoesNotThrow()
        {
            _harness.Disable();
            _harness.Enable();

            ConvaiEmotionProfile profile = ConvaiEmotionProfile.CreateDefault();
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
