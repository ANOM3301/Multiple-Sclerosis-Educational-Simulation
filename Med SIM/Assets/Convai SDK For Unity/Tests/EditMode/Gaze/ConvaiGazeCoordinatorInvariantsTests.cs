using Convai.Domain.Embodiment.Modules;
using Convai.Modules.Gaze.Components;
using Convai.Modules.Gaze.Profiles;
using Convai.Runtime.Animation;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Gaze
{
    [TestFixture]
    public sealed class ConvaiGazeCoordinatorInvariantsTests
        : EmbodimentReceiverTestsBase<ConvaiGazeCoordinator, ConvaiGazeCoordinationProfile>
    {
        protected override string ExpectedModuleId => ModuleIds.GazeCoordination;
        protected override EmbodimentTickPhase ExpectedPhase => EmbodimentTickPhase.Cognition;
        protected override ConvaiGazeCoordinationProfile CreateValidProfile() => ConvaiGazeCoordinationProfile.CreateDefault();
    }
}
