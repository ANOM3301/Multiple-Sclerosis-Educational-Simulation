using Convai.Domain.Embodiment.Modules;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Modules.Gaze.Components;
using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Embodiment;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Gaze
{
    /// <summary>
    ///     Component-level tests for <see cref="ConvaiHeadLookActuator" />.  The actuator
    ///     drives head/neck bones in <c>LateUpdate</c> and does not implement
    ///     <see cref="IEmbodimentTickable" />, so the shared invariant suite does not apply.
    ///     Tests verify module identity, profile routing, lifecycle null-safety with no rig
    ///     bones, and the public read-only state accessors before any frames are pumped.
    /// </summary>
    [TestFixture]
    public sealed class ConvaiHeadLookActuatorComponentTests
    {
        private EmbodimentTestRig _rig;
        private ConvaiHeadLookActuator _actuator;
        private IEmbodimentProfileReceiver _receiver;

        [SetUp]
        public void SetUp()
        {
            _rig = EmbodimentTestRig.Create(nameof(ConvaiHeadLookActuatorComponentTests));
            _actuator = _rig.AddComponent<ConvaiHeadLookActuator>();
            _receiver = (IEmbodimentProfileReceiver)_actuator;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.NoUnexpectedReceived();
            _rig.Dispose();
        }

        // ── Module identity ────────────────────────────────────────────────────

        [Test]
        public void ModuleId_EqualsGazeHead()
        {
            Assert.That(_receiver.ModuleId, Is.EqualTo(ModuleIds.GazeHead));
        }

        // ── Profile routing ────────────────────────────────────────────────────

        [Test]
        public void ApplyProfile_ValidType_ReturnsTrue()
        {
            ConvaiGazeHeadProfile profile = ConvaiGazeHeadProfile.CreateDefault();
            try
            {
                Assert.That(_receiver.ApplyProfile(profile), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ApplyProfile_NullProfile_ReturnsTrue()
        {
            Assert.That(_receiver.ApplyProfile(null), Is.True);
        }

        // ── Lifecycle null-safety ──────────────────────────────────────────────

        [Test]
        public void OnEnable_WithNoBones_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _actuator.enabled = false;
                _actuator.enabled = true;
            });
        }

        // ── Public state accessors ─────────────────────────────────────────────

        [Test]
        public void StateAccessors_BeforeAnyLateUpdate_AreAtRest()
        {
            // No frames have been pumped, so all solved angles and weights stay at
            // their reset values.
            Assert.That(_actuator.CurrentHeadAuthority, Is.EqualTo(0f));
            Assert.That(_actuator.IsIdleExploring, Is.False);
            Assert.That(_actuator.CurrentSolvedAngles.x, Is.EqualTo(0f));
            Assert.That(_actuator.CurrentSolvedAngles.y, Is.EqualTo(0f));
        }
    }
}
