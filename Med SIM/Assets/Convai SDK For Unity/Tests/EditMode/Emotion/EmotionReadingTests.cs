using System.Collections.Generic;
using Convai.Domain.Embodiment.Readings;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Emotion
{
    [TestFixture]
    public sealed class EmotionReadingTests
    {
        [Test]
        public void Constructor_DefensivelyCopiesScoreTable()
        {
            var scores = new Dictionary<string, float>
            {
                ["joy"] = 0.75f
            };

            var reading = new EmotionReading("joy", 0.75f, scores, 0.4f, 1.2f);
            scores["joy"] = 0.1f;
            scores["anger"] = 1f;

            Assert.That(reading.GetScore("joy"), Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(reading.GetScore("anger"), Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void CopyScoresTo_UsesCallerOwnedDictionary()
        {
            var reading = new EmotionReading(
                "joy",
                0.75f,
                new Dictionary<string, float> { ["joy"] = 0.75f },
                0.4f,
                1.2f);
            var destination = new Dictionary<string, float> { ["stale"] = 1f };

            reading.CopyScoresTo(destination);

            Assert.That(destination.ContainsKey("stale"), Is.False);
            Assert.That(destination["joy"], Is.EqualTo(0.75f).Within(1e-6f));
        }
    }
}
