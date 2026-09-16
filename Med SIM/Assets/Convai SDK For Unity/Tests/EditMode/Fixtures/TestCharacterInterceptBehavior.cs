using Convai.Runtime.Behaviors;
using UnityEngine;

namespace Convai.Tests.EditMode.Fixtures
{
    public class TestCharacterInterceptBehavior : ConvaiCharacterBehaviorBase
    {
        [SerializeField] private int priorityOverride = 100;

        public bool Intercepted { get; private set; }

        public override int Priority => priorityOverride;

        public override bool OnTranscriptReceived(IConvaiCharacterAgent agent, string transcript, bool isFinal)
        {
            Intercepted = true;
            return true;
        }
    }
}
