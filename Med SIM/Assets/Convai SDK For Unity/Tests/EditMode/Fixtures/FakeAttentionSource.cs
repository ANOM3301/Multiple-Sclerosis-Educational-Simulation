using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;
using UnityEngine;

namespace Convai.Tests.EditMode.Fixtures
{
    /// <summary>Settable <see cref="IAttentionSource" /> for unit tests.</summary>
    public sealed class FakeAttentionSource : IAttentionSource
    {
        public AttentionReading Current { get; set; } = AttentionReading.Empty;

        public void SetValid(Transform target, Vector3 smoothedPoint, float commitment = 1f, int generationId = 1)
        {
            Current = new AttentionReading(true, target, smoothedPoint, commitment, generationId);
        }

        public void SetInvalid() => Current = AttentionReading.Empty;
    }
}
