using Convai.Domain.Embodiment.Modules;
using Convai.Modules.Attention.Components;
using Convai.Modules.Attention.Profiles;
using Convai.Runtime.Animation;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Attention
{
    [TestFixture]
    public sealed class ConvaiAttentionControllerInvariantsTests
        : EmbodimentReceiverTestsBase<ConvaiAttentionController, ConvaiAttentionProfile>
    {
        protected override string ExpectedModuleId => ModuleIds.Attention;
        protected override EmbodimentTickPhase ExpectedPhase => EmbodimentTickPhase.Cognition;
        protected override ConvaiAttentionProfile CreateValidProfile() => ConvaiAttentionProfile.CreateDefault();
    }
}
