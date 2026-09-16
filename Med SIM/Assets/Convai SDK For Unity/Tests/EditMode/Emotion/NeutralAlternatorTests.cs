using Convai.Modules.Emotion.Core;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Emotion
{
    [TestFixture]
    public sealed class NeutralAlternatorTests
    {
        [Test]
        public void Initial_FactorIsZero()
        {
            var a = new NeutralAlternator();
            Assert.That(a.Factor, Is.EqualTo(0f));
            Assert.That(a.IsActive, Is.False);
        }

        [Test]
        public void Tick_OnlyWhileTalking_StaysInactiveWhenSilent()
        {
            var a = new NeutralAlternator(
                minInterval: 0.01f, maxInterval: 0.02f, blendDuration: 0.1f, onlyWhileTalking: true);
            a.SetTalkingState(false);

            for (int i = 0; i < 20; i++)
                a.Tick(0.05f);

            Assert.That(a.IsActive, Is.False);
            Assert.That(a.Factor, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Tick_DrivenWhenTalking_ProducesAlternation()
        {
            var a = new NeutralAlternator(
                minInterval: 0.02f, maxInterval: 0.02f, blendDuration: 0.05f, onlyWhileTalking: true);
            a.SetTalkingState(true);

            float maxObserved = 0f;
            for (int i = 0; i < 200; i++)
            {
                a.Tick(0.02f);
                if (a.Factor > maxObserved) maxObserved = a.Factor;
            }

            Assert.That(a.IsActive, Is.True);
            Assert.That(maxObserved, Is.GreaterThan(0.5f), "Alternator should swing toward neutral at least once.");
        }

        [Test]
        public void Reset_RestoresInitialState()
        {
            var a = new NeutralAlternator(
                minInterval: 0.01f, maxInterval: 0.02f, blendDuration: 0.05f, onlyWhileTalking: false);
            for (int i = 0; i < 20; i++) a.Tick(0.02f);

            a.Reset();

            Assert.That(a.Factor, Is.EqualTo(0f));
            Assert.That(a.IsActive, Is.False);
        }

        [Test]
        public void Tick_WithSameSeed_IsDeterministic()
        {
            var a = new NeutralAlternator(
                minInterval: 0.01f, maxInterval: 0.04f, blendDuration: 0.05f, onlyWhileTalking: false, seed: 1234u);
            var b = new NeutralAlternator(
                minInterval: 0.01f, maxInterval: 0.04f, blendDuration: 0.05f, onlyWhileTalking: false, seed: 1234u);

            for (int i = 0; i < 80; i++)
            {
                a.Tick(0.016f);
                b.Tick(0.016f);
                Assert.That(a.Factor, Is.EqualTo(b.Factor).Within(1e-6f));
                Assert.That(a.IsActive, Is.EqualTo(b.IsActive));
            }
        }
    }
}
