using Convai.Domain.Models;
using Convai.Runtime.Behaviors;
using UnityEngine;

namespace Convai.Tests.EditMode.Fixtures
{
    public class TestPlayerBehavior : ConvaiPlayerBehaviorBase
    {
        [SerializeField] private int priorityOverride = 100;

        public bool TranscriptIntercepted { get; private set; }
        public bool InputIntercepted { get; private set; }

        public override int Priority => priorityOverride;

        public override bool OnTranscriptReceived(IConvaiPlayerAgent agent, string transcript, TranscriptionPhase phase)
        {
            TranscriptIntercepted = true;
            return true;
        }

        public override bool OnInputStarted(IConvaiPlayerAgent agent)
        {
            InputIntercepted = true;
            return true;
        }
    }
}
