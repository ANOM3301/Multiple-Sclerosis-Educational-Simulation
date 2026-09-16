using Convai.Modules.DialogueAnimation.Runtime;
using NUnit.Framework;

namespace Convai.Tests.EditMode.DialogueAnimation
{
    /// <summary>
    ///     Verifies the framerate-independent slew math in
    ///     <see cref="AnimatorLayerBlender" />: monotonic progress toward target, target snap
    ///     at fade==0, and re-arm semantics on a new target mid-fade.
    /// </summary>
    public sealed class AnimatorLayerBlenderTests
    {
        private const float Epsilon = 1e-5f;

        [Test]
        public void ForceWeight_Clamps01_AndStopsFade()
        {
            AnimatorLayerBlender blender = new();
            blender.ForceWeight(2f);

            Assert.AreEqual(1f, blender.CurrentWeight, Epsilon);
            Assert.AreEqual(1f, blender.TargetWeight, Epsilon);
            Assert.IsFalse(blender.IsFading);
        }

        [Test]
        public void SetTarget_ZeroFade_SnapsImmediately()
        {
            AnimatorLayerBlender blender = new();
            blender.ForceWeight(0f);
            blender.SetTarget(0.7f, fadeSeconds: 0f);

            Assert.AreEqual(0.7f, blender.CurrentWeight, Epsilon);
            Assert.IsFalse(blender.IsFading);
        }

        [Test]
        public void Tick_LinearSlew_ReachesTargetInExpectedTime()
        {
            AnimatorLayerBlender blender = new();
            blender.ForceWeight(0f);
            blender.SetTarget(1f, fadeSeconds: 1f);

            blender.Tick(0.5f);
            Assert.AreEqual(0.5f, blender.CurrentWeight, Epsilon);

            blender.Tick(0.5f);
            Assert.AreEqual(1f, blender.CurrentWeight, Epsilon);
            Assert.IsFalse(blender.IsFading);
        }

        [Test]
        public void SetTarget_ReArms_WithShorterFadeAccelerates()
        {
            AnimatorLayerBlender blender = new();
            blender.ForceWeight(0f);
            blender.SetTarget(1f, fadeSeconds: 4f);
            blender.Tick(1f);
            Assert.AreEqual(0.25f, blender.CurrentWeight, Epsilon);

            blender.SetTarget(1f, fadeSeconds: 0.5f);
            blender.Tick(0.25f);
            Assert.AreEqual(0.625f, blender.CurrentWeight, Epsilon);

            blender.Tick(0.25f);
            Assert.AreEqual(1f, blender.CurrentWeight, Epsilon);
        }

        [Test]
        public void Tick_AtTarget_NoMovement()
        {
            AnimatorLayerBlender blender = new();
            blender.ForceWeight(0.4f);
            blender.SetTarget(0.4f, fadeSeconds: 0.5f);

            blender.Tick(1f);
            Assert.AreEqual(0.4f, blender.CurrentWeight, Epsilon);
        }
    }
}
