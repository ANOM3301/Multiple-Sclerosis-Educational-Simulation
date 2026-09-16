using Convai.Runtime.Behaviors;
using UnityEngine;

namespace Convai.Tests.EditMode.Fixtures
{
    public sealed class TestCharacterResponseHandler : MonoBehaviour, IConvaiResponseHandler
    {
        [SerializeField] private int priority = 50;

        public bool Intercepted { get; private set; }

        public int Priority => priority;

        public bool CanHandle(IConvaiCharacterAgent agent, string text, bool isFinal) => true;

        public bool ProcessResponse(IConvaiCharacterAgent agent, string text, bool isFinal)
        {
            Intercepted = true;
            return true;
        }
    }
}
