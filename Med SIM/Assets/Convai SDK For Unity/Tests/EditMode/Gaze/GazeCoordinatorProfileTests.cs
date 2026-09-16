using Convai.Domain.Embodiment.Semantics;
using Convai.Modules.Gaze.Profiles;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Convai.Tests.EditMode.Gaze
{
    public sealed class GazeCoordinatorProfileTests
    {
        [Test]
        public void CreateConversationalPreset_IdleSuppressesAttention_EngagedStatesRampToSpeaking()
        {
            ConvaiGazeCoordinationProfile profile = ConvaiGazeCoordinationProfile.CreateConversationalPreset();

            try
            {
                profile.GetStatePolicy(
                    DialogueState.Idle,
                    out float idleWeight,
                    out float idleEyeShare,
                    out bool idleSuppressAttentionTarget);
                profile.GetStatePolicy(
                    DialogueState.Listening,
                    out float listeningWeight,
                    out float listeningEyeShare,
                    out bool listeningSuppressAttentionTarget);

                Assert.That(idleWeight, Is.GreaterThan(0f));
                Assert.That(idleEyeShare, Is.GreaterThan(0f));
                Assert.That(idleSuppressAttentionTarget, Is.True);
                Assert.That(listeningWeight, Is.GreaterThan(idleWeight));
                Assert.That(listeningEyeShare, Is.GreaterThan(0f));
                Assert.That(listeningSuppressAttentionTarget, Is.False);

                profile.GetStatePolicy(DialogueState.Attending, out float attendingWeight, out _, out _);
                profile.GetStatePolicy(DialogueState.Thinking, out float thinkingWeight, out _, out _);
                profile.GetStatePolicy(DialogueState.Speaking, out float speakingWeight, out _, out bool speakingSuppress);

                Assert.That(attendingWeight, Is.EqualTo(0.9f));
                Assert.That(listeningWeight, Is.GreaterThan(attendingWeight));
                Assert.That(thinkingWeight, Is.GreaterThan(listeningWeight));
                Assert.That(speakingWeight, Is.EqualTo(1f));
                Assert.That(speakingSuppress, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void GetStatePolicy_FallsBackToIdleEntry_WhenStateIsUnknown()
        {
            ConvaiGazeCoordinationProfile profile = ConvaiGazeCoordinationProfile.CreateConversationalPreset();

            try
            {
                profile.GetStatePolicy(DialogueState.Idle, out float idleWeight, out float idleEyeShare, out bool idleSuppressAttentionTarget);
                profile.GetStatePolicy((DialogueState)999, out float unknownWeight, out float unknownEyeShare, out bool unknownSuppressAttentionTarget);

                Assert.That(unknownWeight, Is.EqualTo(idleWeight));
                Assert.That(unknownEyeShare, Is.EqualTo(idleEyeShare));
                Assert.That(unknownSuppressAttentionTarget, Is.EqualTo(idleSuppressAttentionTarget));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }
    }
}
