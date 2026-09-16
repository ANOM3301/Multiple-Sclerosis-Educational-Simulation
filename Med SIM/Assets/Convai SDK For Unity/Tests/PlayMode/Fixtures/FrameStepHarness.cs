using System.Collections;
using UnityEngine;

namespace Convai.Tests.PlayMode.Fixtures
{
    /// <summary>
    ///     Advances Unity's frame clock by a fixed delta for deterministic <c>[UnityTest]</c>
    ///     coroutines. Sets <see cref="Time.captureDeltaTime" /> on construction and restores it
    ///     on <see cref="Dispose" />. Use <see cref="StepFrames" /> to yield N frames.
    /// </summary>
    public sealed class FrameStepHarness : System.IDisposable
    {
        private readonly float _savedCaptureDeltaTime;

        public float Delta { get; }

        public FrameStepHarness(float delta = 1f / 60f)
        {
            Delta = delta;
            _savedCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = delta;
        }

        public IEnumerator StepFrames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        public void Dispose()
        {
            Time.captureDeltaTime = _savedCaptureDeltaTime;
        }
    }
}
