using Convai.Domain.Embodiment.Interfaces;
using Convai.Domain.Embodiment.Readings;

namespace Convai.Tests.EditMode.Fixtures
{
    /// <summary>Settable <see cref="IGazeIntentProvider" /> for unit tests.</summary>
    public sealed class FakeGazeIntentProvider : IGazeIntentProvider
    {
        public GazeIntent Current { get; set; } = GazeIntent.Relaxed;
    }
}
