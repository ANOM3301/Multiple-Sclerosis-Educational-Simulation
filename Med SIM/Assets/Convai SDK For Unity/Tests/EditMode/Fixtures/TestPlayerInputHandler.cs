using Convai.Domain.DomainEvents.Session;
using Convai.Runtime.Behaviors;
using UnityEngine;

namespace Convai.Tests.EditMode.Fixtures
{
    public class TestPlayerInputHandler : ConvaiPlayerInputHandlerBase
    {
        [SerializeField] private int priorityOverride = 25;

        public bool ShouldHandle { get; set; } = true;
        public bool Processed { get; private set; }

        public override int Priority => priorityOverride;

        public override bool CanHandle(IConvaiPlayerAgent agent, SessionStateChanged sessionState) => ShouldHandle;

        public override bool ProcessInput(IConvaiPlayerAgent agent, SessionStateChanged sessionState)
        {
            Processed = true;
            return true;
        }
    }
}
