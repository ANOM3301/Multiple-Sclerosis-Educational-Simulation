using Convai.Runtime.Animation;
using NUnit.Framework;
using UnityEngine;

namespace Convai.Tests.EditMode.DialogueAnimation
{
    /// <summary>
    ///     Verifies that the <see cref="SpeechEnergyWindow" /> produces expected RMS values
    ///     for known inputs and handles ring-buffer wrap-around correctly.
    /// </summary>
    [TestFixture]
    public sealed class SpeechEnergyWindowTests
    {
        [Test]
        public void Empty_Rms_IsZero()
        {
            var w = new SpeechEnergyWindow(8);
            Assert.That(w.ComputeRms(), Is.EqualTo(0f));
            Assert.That(w.Count, Is.EqualTo(0));
        }

        [Test]
        public void ConstantAmplitude_Rms_EqualsAmplitude()
        {
            var w = new SpeechEnergyWindow(4);
            for (int i = 0; i < 4; i++) w.Push(0.5f);
            Assert.That(w.ComputeRms(), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void SineLikeSequence_Rms_MatchesAnalyticValue()
        {
            var w = new SpeechEnergyWindow(32);
            for (int i = 0; i < 32; i++)
                w.Push(Mathf.Sin(i * Mathf.PI / 16f));

            // RMS of a zero-mean sine is amplitude / sqrt(2), about 0.7071.
            Assert.That(w.ComputeRms(), Is.EqualTo(0.7071f).Within(0.01f));
        }

        [Test]
        public void Wraparound_DiscardsOldestSamples()
        {
            var w = new SpeechEnergyWindow(3);
            w.Push(0f);
            w.Push(0f);
            w.Push(0f);
            w.Push(1f);
            w.Push(1f);
            w.Push(1f);

            Assert.That(w.Count, Is.EqualTo(3));
            Assert.That(w.ComputeRms(), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Clear_ResetsWindow()
        {
            var w = new SpeechEnergyWindow(4);
            for (int i = 0; i < 4; i++) w.Push(0.9f);

            w.Clear();
            Assert.That(w.Count, Is.EqualTo(0));
            Assert.That(w.ComputeRms(), Is.EqualTo(0f));
        }
    }
}
