using Convai.Domain.Embodiment.Modules;
using Convai.Modules.ConversationFlow.Components;
using Convai.Modules.ConversationFlow.Profiles;
using Convai.Runtime.Animation;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace Convai.Tests.EditMode.ConversationFlow
{
    [TestFixture]
    public sealed class ConvaiConversationFlowControllerInvariantsTests
        : EmbodimentReceiverTestsBase<ConvaiConversationFlowController, ConvaiConversationFlowProfile>
    {
        protected override string ExpectedModuleId => ModuleIds.ConversationFlow;
        protected override EmbodimentTickPhase ExpectedPhase => EmbodimentTickPhase.Cognition;
        protected override ConvaiConversationFlowProfile CreateValidProfile() => ConvaiConversationFlowProfile.CreateDefault();
    }
}
