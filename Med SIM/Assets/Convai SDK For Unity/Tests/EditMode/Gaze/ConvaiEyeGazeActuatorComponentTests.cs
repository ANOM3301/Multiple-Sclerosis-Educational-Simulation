using Convai.Domain.Embodiment.Modules;
using Convai.Domain.Embodiment.Interfaces;
using Convai.Modules.Gaze.Components;
using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Animation;
using Convai.Runtime.Embodiment;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Gaze
{
    /// <summary>
    ///     Component-level tests for <see cref="ConvaiEyeGazeActuator" />.  Because this
    ///     component drives eye bones in <c>LateUpdate</c> rather than implementing
    ///     <see cref="IEmbodimentTickable" />, the
    ///     <see cref="EmbodimentReceiverTestsBase{TController,TProfile}" /> invariant suite
    ///     does not cover it.  These tests verify lifecycle null-safety, profile routing, and
    ///     the <see cref="IFacialBlendshapeSource" /> contract under headless conditions.
    /// </summary>
    [TestFixture]
    public sealed class ConvaiEyeGazeActuatorComponentTests
    {
        private EmbodimentTestRig _rig;
        private ConvaiEyeGazeActuator _actuator;
        private IEmbodimentProfileReceiver _receiver;

        [SetUp]
        public void SetUp()
        {
            _rig = EmbodimentTestRig.Create(nameof(ConvaiEyeGazeActuatorComponentTests));
            _actuator = _rig.AddComponent<ConvaiEyeGazeActuator>();
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
        public void ModuleId_EqualsGazeEye()
        {
            Assert.That(_receiver.ModuleId, Is.EqualTo(ModuleIds.GazeEye));
        }

        // ── Profile routing ────────────────────────────────────────────────────

        [Test]
        public void ApplyProfile_ValidType_ReturnsTrue()
        {
            ConvaiGazeEyeProfile profile = ConvaiGazeEyeProfile.CreateDefault();
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

        [Test]
        public void ApplyProfile_WrongType_ReturnsFalse()
        {
            // Use a different gaze profile type as a mismatch sentinel.
            ConvaiGazeHeadProfile wrong = ConvaiGazeHeadProfile.CreateDefault();
            try
            {
                Assert.That(_receiver.ApplyProfile(wrong), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(wrong);
            }
        }

        // ── Lifecycle null-safety ──────────────────────────────────────────────

        [Test]
        public void OnEnable_WithNoBones_DoesNotThrow()
        {
            // OnEnable already ran during AddComponent in SetUp.
            // Cycling disable/enable must be safe with no rig bones present.
            Assert.DoesNotThrow(() =>
            {
                _actuator.enabled = false;
                _actuator.enabled = true;
            });
        }

        // ── IFacialBlendshapeSource contract ──────────────────────────────────

        [Test]
        public void SourceComponent_IsNotNull_And_IsSameInstance()
        {
            IFacialBlendshapeSource source = _actuator;

            Assert.That(source.SourceComponent, Is.Not.Null);
            Assert.That(source.SourceComponent, Is.SameAs(_actuator));
        }

        [Test]
        public void SourceName_IsNotNullOrEmpty()
        {
            IFacialBlendshapeSource source = _actuator;

            Assert.That(source.SourceName, Is.Not.Null.And.Not.Empty);
        }
    }
}
