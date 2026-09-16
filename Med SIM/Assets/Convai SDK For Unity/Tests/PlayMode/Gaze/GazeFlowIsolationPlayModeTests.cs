using System.Collections;
using System.Text.RegularExpressions;
using Convai.Modules.ConversationFlow.Components;
using Convai.Modules.Gaze.Components;
using Convai.Runtime.Embodiment;
using Convai.Runtime.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Convai.Tests.PlayMode.Gaze
{
    /// <summary>
    ///     Locks P0/P1 behavior: gaze bootstrap must not imply conversation-flow driver provisioning.
    /// </summary>
    public sealed class EmbodimentGazeFlowIsolationPlayModeTests
    {
        [UnityTest]
        public IEnumerator GazeRuntimeBootstrap_EnsureCoordinator_DoesNotDemandConversationFlowDriver()
        {
            GameObject root = new(nameof(EmbodimentGazeFlowIsolationPlayModeTests));

            try
            {
                // ConvaiCharacter.Awake always calls ValidateSDKSetup which logs an error
                // when ConvaiManager is absent. Expected in headless play-mode tests.
                LogAssert.Expect(LogType.Error, new Regex("Convai SDK Setup Error"));
                root.AddComponent<ConvaiCharacter>();
                Assert.IsTrue(
                    EmbodimentContext.TryResolve(
                        root.GetComponent<ConvaiCharacter>(),
                        out EmbodimentContext context));
                Assert.NotNull(context);

                GazeRuntimeBootstrap.EnsureCoordinator(context, context);

                yield return null;

                Assert.IsFalse(
                    context.IsConversationFlowDriverDemanded,
                    "Gaze coordinator provisioning must not flag conversation-flow auto-create.");
                Assert.IsNotNull(root.GetComponentInChildren<ConvaiGazeCoordinator>(true));
                Assert.IsNull(
                    root.GetComponentInChildren<ConvaiConversationFlowController>(true),
                    "Eye/head gaze path must not spawn ConvaiConversationFlowController.");
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator ConversationFlowDriver_Appears_WhenExplicitlyDemandedAndTryEnsureRuns()
        {
            GameObject root = new("FlowDriverProvisioningPlayModeTest");

            try
            {
                // ConvaiCharacter.Awake always calls ValidateSDKSetup which logs an error
                // when ConvaiManager is absent. Expected in headless play-mode tests.
                LogAssert.Expect(LogType.Error, new Regex("Convai SDK Setup Error"));
                root.AddComponent<ConvaiCharacter>();
                Assert.IsTrue(
                    EmbodimentContext.TryResolve(
                        root.GetComponent<ConvaiCharacter>(),
                        out EmbodimentContext context));

                context.MarkConversationFlowDriverDemanded();
                Assert.IsTrue(context.TryEnsureConversationFlowSource());

                yield return null;

                Assert.IsNotNull(root.GetComponentInChildren<ConvaiConversationFlowController>(true));
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
