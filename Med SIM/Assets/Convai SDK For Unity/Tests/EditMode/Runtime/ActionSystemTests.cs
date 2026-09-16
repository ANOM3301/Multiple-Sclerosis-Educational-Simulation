using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Convai.Domain.Abstractions;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.Errors;
using Convai.Infrastructure.Networking;
using Convai.Infrastructure.Networking.Models;
using Convai.Runtime.Actions;
using Convai.Runtime.Adapters.Networking;
using Convai.Runtime.Components;
using Convai.Runtime.Networking.Media;
using Convai.Runtime.Room;
using Convai.Sample.Behaviors;
using Convai.Shared.Actions;
using Convai.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Runtime
{
    [TestFixture]
    public class ActionSystemTests
    {
        // ConvaiLogger only has sinks after Initialize(); the runtime never guarantees that in
        // EditMode, so the LogAssert-based warning tests below bootstrap the default console
        // sink themselves and restore the empty-sink status quo afterwards.
        [OneTimeSetUp]
        public void OneTimeSetUp() => Convai.Runtime.Logging.ConvaiLogger.Initialize();

        [OneTimeTearDown]
        public void OneTimeTearDown() => Convai.Runtime.Logging.ConvaiLogger.ClearSinks();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject != null && gameObject.name.StartsWith("ActionSystemTests_", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        // ── Resolution via dispatcher (ConvaiResolvedAction is internal) ─────────────────

        [Test]
        public async Task ResolvedAction_ResolvesObjectTarget_ByExactName()
        {
            var fixture = CreateDispatcherFixtureWithObjects(
                actionNames: new[] { "Move To" },
                objectNames: new[] { "cube" });

            ConvaiActionInvocation captured = null;
            fixture.Dispatcher.OnStepStarted.AddListener(inv => captured = inv);
            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => captured != null);

            Assert.That(captured.ResolvedTarget?.Kind, Is.EqualTo(ConvaiActionTargetKind.Object));
            Assert.That(captured.ResolvedTarget?.ObjectBinding?.Name, Is.EqualTo("cube"));
            Assert.That(captured.Command.Name, Is.EqualTo("Move To"));
        }

        [Test]
        public async Task ResolvedAction_UsesReferenceParameter_WhenCommandTargetIsEmpty()
        {
            var fixture = CreateDispatcherFixtureWithObjects(
                actionNames: new[] { "Move To" },
                objectNames: new[] { "cube" });
            ConvaiActionConfigSource source = fixture.GameObject.GetComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = fixture.GameObject.GetComponent<RecordingActionExecutor>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Move To",
                    TargetRequirement = ConvaiActionTargetRequirement.Object,
                    Executor = executor,
                    Parameters = new List<ConvaiActionParameterDefinition>
                    {
                        new()
                        {
                            Name = "destination",
                            Type = ConvaiActionParameterType.Reference,
                            Connector = "toward"
                        }
                    }
                }
            });

            ConvaiActionInvocation captured = null;
            fixture.Dispatcher.OnStepStarted.AddListener(inv => captured = inv);
            fixture.Dispatcher.EnqueueActions(new[]
            {
                new ConvaiActionCommand("Move To")
                {
                    Parameters = new Dictionary<string, ConvaiActionParameterValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["destination"] = new()
                        {
                            Type = ConvaiActionParameterType.Reference,
                            RawValue = "cube",
                            StringValue = "cube",
                            ResolvedReference = new ConvaiActionParameterReference("cube", ConvaiActionTargetKind.Object)
                        }
                    }
                }
            });
            await WaitUntilAsync(() => captured != null);

            Assert.That(captured.ResolvedTarget?.Kind, Is.EqualTo(ConvaiActionTargetKind.Object));
            Assert.That(captured.ResolvedTarget?.Name, Is.EqualTo("cube"));
        }

        [Test]
        public async Task ResolvedAction_EnrichesRawNameSuffix_WhenRtviDidNotPreprocessCommand()
        {
            var fixture = CreateDispatcherFixtureWithObjects(
                actionNames: new[] { "Move To" },
                objectNames: new[] { "cube" });
            ConvaiActionConfigSource source = fixture.GameObject.GetComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = fixture.GameObject.GetComponent<RecordingActionExecutor>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Move To",
                    TargetRequirement = ConvaiActionTargetRequirement.Object,
                    Executor = executor,
                    Parameters = new List<ConvaiActionParameterDefinition>
                    {
                        new() { Name = "destination", Type = ConvaiActionParameterType.Reference }
                    }
                }
            });

            ConvaiActionInvocation captured = null;
            fixture.Dispatcher.OnStepStarted.AddListener(inv => captured = inv);
            fixture.Dispatcher.EnqueueActions(new[] { new ConvaiActionCommand("Move To cube") });
            await WaitUntilAsync(() => captured != null);

            Assert.That(captured.Definition?.ActionName, Is.EqualTo("Move To"));
            Assert.That(captured.Command.Name, Is.EqualTo("Move To"));
            Assert.That(captured.GetString("destination"), Is.EqualTo("cube"));
            Assert.That(captured.ResolvedTarget?.Kind, Is.EqualTo(ConvaiActionTargetKind.Object));
        }

        [Test]
        public async Task ResolvedAction_HandlesMissingTarget()
        {
            var fixture = CreateDispatcherFixtureWithObjects(actionNames: new[] { "Dance" });

            ConvaiActionInvocation captured = null;
            fixture.Dispatcher.OnStepStarted.AddListener(inv => captured = inv);
            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Dance") });
            await WaitUntilAsync(() => captured != null);

            Assert.That(captured.ResolvedTarget, Is.Null);
            Assert.That(captured.Command.Name, Is.EqualTo("Dance"));
        }

        [Test]
        public async Task ResolvedAction_LeavesUnresolvedTarget_WhenUnknown()
        {
            var fixture = CreateDispatcherFixtureWithObjects(
                actionNames: new[] { "Move To" },
                objectNames: new[] { "cube" });

            ConvaiActionInvocation captured = null;
            fixture.Dispatcher.OnStepStarted.AddListener(inv => captured = inv);
            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "unknown") });
            await WaitUntilAsync(() => captured != null);

            Assert.That(captured.ResolvedTarget, Is.Null);
            Assert.That(captured.Command.Target, Is.EqualTo("unknown"));
        }

        [Test]
        public async Task ResolvedAction_ResolvesCharacterTarget_ByExactName()
        {
            var fixture = CreateDispatcherFixtureWithCharacters(
                actionNames: new[] { "Follow" },
                characterNames: new[] { "Player" });

            ConvaiActionInvocation captured = null;
            fixture.Dispatcher.OnStepStarted.AddListener(inv => captured = inv);
            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Follow", "Player") });
            await WaitUntilAsync(() => captured != null);

            Assert.That(captured.ResolvedTarget?.Kind, Is.EqualTo(ConvaiActionTargetKind.Character));
            Assert.That(captured.ResolvedTarget?.CharacterBinding?.Name, Is.EqualTo("Player"));
        }

        [Test]
        public async Task ResolvedAction_UsesRequiredCharacterKind_WhenObjectAndCharacterNamesCollide()
        {
            var fixture = CreateDispatcherFixtureWithMixedTargets(
                requirement: ConvaiActionTargetRequirement.Character,
                targetName: "SharedTarget",
                addObject: true,
                addCharacter: true);

            ConvaiActionInvocation captured = null;
            fixture.Dispatcher.OnStepStarted.AddListener(inv => captured = inv);
            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "SharedTarget") });
            await WaitUntilAsync(() => captured != null);

            Assert.That(captured.ResolvedTarget?.Kind, Is.EqualTo(ConvaiActionTargetKind.Character));
            Assert.That(captured.ResolvedTarget?.CharacterBinding?.Name, Is.EqualTo("SharedTarget"));
        }

        [Test]
        public async Task ResolvedAction_UsesRequiredObjectKind_WhenObjectAndCharacterNamesCollide()
        {
            var fixture = CreateDispatcherFixtureWithMixedTargets(
                requirement: ConvaiActionTargetRequirement.Object,
                targetName: "SharedTarget",
                addObject: true,
                addCharacter: true);

            ConvaiActionInvocation captured = null;
            fixture.Dispatcher.OnStepStarted.AddListener(inv => captured = inv);
            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "SharedTarget") });
            await WaitUntilAsync(() => captured != null);

            Assert.That(captured.ResolvedTarget?.Kind, Is.EqualTo(ConvaiActionTargetKind.Object));
            Assert.That(captured.ResolvedTarget?.ObjectBinding?.Name, Is.EqualTo("SharedTarget"));
        }

        // ── Queue / policy / cancellation ─────────────────────────────────────────────────

        [Test]
        public async Task Dispatcher_ExecutesBatchesSequentially_WithQueuePolicy()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.Queue);
            RecordingActionExecutor executor = fixture.Executor;
            executor.DelayMs = 15;

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube"), CreateAction("Pick Up", "cube") });
            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Drop", "cube") });
            await WaitUntilAsync(() => executor.ExecutedActions.Count == 3);

            CollectionAssert.AreEqual(
                new[] { "Move To cube", "Pick Up cube", "Drop cube" },
                executor.ExecutedActions);
        }

        [Test]
        public async Task Dispatcher_DropIncomingPolicy_IgnoresSecondBatchWhileBusy()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.DropIncoming);
            RecordingActionExecutor executor = fixture.Executor;
            executor.DelayMs = 25;

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Drop", "cube") });
            await WaitUntilAsync(() => executor.ExecutedActions.Count == 1);

            CollectionAssert.AreEqual(new[] { "Move To cube" }, executor.ExecutedActions);
        }

        [Test]
        public async Task Dispatcher_ReplaceCurrentPolicy_RunsReplacementBatchAfterCancellingActiveBatch()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.ReplaceCurrent);
            RecordingActionExecutor executor = fixture.Executor;
            // Long enough that the replacement batch reliably arrives while the first step is
            // still in flight, even when the editor hitches between the waits below.
            executor.DelayMs = 1000;

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => executor.ExecutedActions.Contains("Move To cube"), timeoutMs: 2000);
            await Task.Delay(20);
            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Drop", "cube") });
            await WaitUntilAsync(() => executor.ExecutedActions.Contains("Drop cube"), timeoutMs: 4000);

            Assert.That(executor.CancellationObserved, Is.True,
                "ReplaceCurrent should cancel the in-flight step before running the replacement batch.");
            CollectionAssert.AreEqual(new[] { "Move To cube", "Drop cube" }, executor.ExecutedActions);
        }

        [Test]
        public async Task Dispatcher_CancelsRunningBatch_WhenDisabled()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.Queue);
            RecordingActionExecutor executor = fixture.Executor;
            executor.DelayMs = 250;

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await Task.Delay(40);
            fixture.GameObject.SetActive(false);
            await Task.Delay(40);

            Assert.That(executor.CancellationObserved, Is.True);
        }

        [Test]
        public async Task Dispatcher_FailsStep_WhenNoDefinitionMatchesAction()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.Queue);
            ConvaiActionInvocation captured = null;
            fixture.Dispatcher.OnStepFailed.AddListener(inv => captured = inv);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Unknown Action", "cube") });
            await WaitUntilAsync(() => captured != null);

            Assert.That(captured.Command.Name, Is.EqualTo("Unknown Action"));
        }

        // ── Definition lookup ─────────────────────────────────────────────────────────────

        [Test]
        public async Task Dispatcher_FiresFailed_WhenExecutorIsNull()
        {
            var fixture = CreateDispatcherFixtureWithNullExecutor(actionName: "Dance");
            bool failedFired = false;
            fixture.Dispatcher.OnStepFailed.AddListener(_ => failedFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Dance") });
            await WaitUntilAsync(() => failedFired);

            Assert.That(failedFired, Is.True);
        }

        [Test]
        public async Task Dispatcher_FiresFailed_WhenExecutorDoesNotImplementInterface()
        {
            var fixture = CreateDispatcherFixtureWithNonExecutorMonoBehaviour(actionName: "Dance");
            bool failedFired = false;
            fixture.Dispatcher.OnStepFailed.AddListener(_ => failedFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Dance") });
            await WaitUntilAsync(() => failedFired);

            Assert.That(failedFired, Is.True);
        }

        [Test]
        public async Task Dispatcher_LooksUpDefinition_CaseInsensitive()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.Queue);
            ConvaiActionInvocation captured = null;
            fixture.Dispatcher.OnStepSucceeded.AddListener(inv => captured = inv);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("MOVE TO", "cube") });
            await WaitUntilAsync(() => captured != null);

            Assert.That(captured.Definition?.ActionName, Is.EqualTo("Move To"));
        }

        // ── Target validation ─────────────────────────────────────────────────────────────

        [Test]
        public async Task Dispatcher_FiresFailed_WhenObjectRequiredButNoTarget()
        {
            var fixture = CreateDispatcherFixtureWithRequirement(
                ConvaiActionTargetRequirement.Object,
                includeObjectInConfig: false);
            bool failedFired = false;
            fixture.Dispatcher.OnStepFailed.AddListener(_ => failedFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To") });
            await WaitUntilAsync(() => failedFired);

            Assert.That(failedFired, Is.True);
        }

        [Test]
        public async Task Dispatcher_FiresFailed_WhenObjectRequiredButCharacterResolved()
        {
            var fixture = CreateDispatcherFixtureWithMixedTargets(
                requirement: ConvaiActionTargetRequirement.Object,
                targetName: "Player",
                addObject: false,
                addCharacter: true);
            bool failedFired = false;
            fixture.Dispatcher.OnStepFailed.AddListener(_ => failedFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "Player") });
            await WaitUntilAsync(() => failedFired);

            Assert.That(failedFired, Is.True);
        }

        [Test]
        public async Task Dispatcher_Succeeds_WhenNoneRequirementAndNoTarget()
        {
            var fixture = CreateDispatcherFixtureWithRequirement(
                ConvaiActionTargetRequirement.None,
                includeObjectInConfig: false);
            bool succeededFired = false;
            fixture.Dispatcher.OnStepSucceeded.AddListener(_ => succeededFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Dance") });
            await WaitUntilAsync(() => succeededFired);

            Assert.That(succeededFired, Is.True);
        }

        [Test]
        public async Task Dispatcher_Succeeds_WhenEitherRequirementAndObjectTarget()
        {
            var fixture = CreateDispatcherFixtureWithMixedTargets(
                requirement: ConvaiActionTargetRequirement.Either,
                targetName: "cube",
                addObject: true,
                addCharacter: false);
            bool succeededFired = false;
            fixture.Dispatcher.OnStepSucceeded.AddListener(_ => succeededFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => succeededFired);

            Assert.That(succeededFired, Is.True);
        }

        [Test]
        public async Task Dispatcher_Succeeds_WhenEitherRequirementAndCharacterTarget()
        {
            var fixture = CreateDispatcherFixtureWithMixedTargets(
                requirement: ConvaiActionTargetRequirement.Either,
                targetName: "Player",
                addObject: false,
                addCharacter: true);
            bool succeededFired = false;
            fixture.Dispatcher.OnStepSucceeded.AddListener(_ => succeededFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "Player") });
            await WaitUntilAsync(() => succeededFired);

            Assert.That(succeededFired, Is.True);
        }

        [Test]
        public async Task Dispatcher_EmitsStepCompletedReport_WhenTargetRequirementFails()
        {
            var fixture = CreateDispatcherFixtureWithMixedTargets(
                requirement: ConvaiActionTargetRequirement.Object,
                targetName: "Player",
                addObject: false,
                addCharacter: true);
            SetPrivateField(fixture.Dispatcher, "_failurePolicy", ConvaiActionBatchFailurePolicy.StopBatch);
            ConvaiActionStepReport completedReport = null;
            fixture.Dispatcher.OnStepCompleted.AddListener(report => completedReport = report);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "Player") });
            await WaitUntilAsync(() => completedReport != null);

            Assert.That(completedReport.Invocation.Command.Name, Is.EqualTo("Move To"));
            Assert.That(completedReport.Invocation.Command.Target, Is.EqualTo("Player"));
            Assert.That(completedReport.Invocation.ResolvedTarget.Kind, Is.EqualTo(ConvaiActionTargetKind.Character));
            Assert.That(completedReport.Result.Status, Is.EqualTo(ConvaiActionExecutionStatus.Failed));
            Assert.That(completedReport.BatchAborted, Is.True);
            StringAssert.Contains("Action 'Move To'", completedReport.FailureMessage);
            StringAssert.Contains("target 'Player'", completedReport.FailureMessage);
            StringAssert.Contains("required Object", completedReport.FailureMessage);
            StringAssert.Contains("resolved Character", completedReport.FailureMessage);
            StringAssert.Contains("batch will abort", completedReport.FailureMessage);
        }

        [Test]
        public async Task DebugProbe_RecordsFailureReason_WhenTargetRequirementFails()
        {
            var fixture = CreateDispatcherFixtureWithMixedTargets(
                requirement: ConvaiActionTargetRequirement.Object,
                targetName: "Player",
                addObject: false,
                addCharacter: true);
            SetPrivateField(fixture.Dispatcher, "_failurePolicy", ConvaiActionBatchFailurePolicy.StopBatch);
            ConvaiActionDebugProbe probe = fixture.GameObject.AddComponent<ConvaiActionDebugProbe>();
            SetPrivateField(probe, "_logToConsole", false);
            SetPrivateField(probe, "_character", fixture.GameObject.GetComponent<ConvaiCharacter>());
            SetPrivateField(probe, "_dispatcher", fixture.Dispatcher);
            InvokePrivateMethod(probe, "OnEnable");

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "Player") });
            await WaitUntilAsync(() => GetPrivateField<string>(probe, "_lastFailureReason")?.Length > 0);

            string lastFailureReason = GetPrivateField<string>(probe, "_lastFailureReason");
            StringAssert.Contains("Action 'Move To'", lastFailureReason);
            StringAssert.Contains("target 'Player'", lastFailureReason);
            StringAssert.Contains("required Object", lastFailureReason);
            StringAssert.Contains("resolved Character", lastFailureReason);
        }

        // ── Failure policy ────────────────────────────────────────────────────────────────

        [Test]
        public async Task Dispatcher_StopBatch_AbortsBatchOnStepFailed()
        {
            var fixture = CreateDispatcherFixtureWithFailure(
                ConvaiActionBatchPolicy.Queue,
                ConvaiActionBatchFailurePolicy.StopBatch);
            bool batchAborted = false;
            fixture.Dispatcher.OnBatchAborted.AddListener(() => batchAborted = true);

            fixture.Dispatcher.EnqueueActions(new[]
            {
                CreateAction("Move To", "cube"),
                CreateAction("Pick Up", "cube")
            });
            await WaitUntilAsync(() => batchAborted);

            Assert.That(batchAborted, Is.True);
            Assert.That(fixture.Executor.ExecutedActions.Count, Is.EqualTo(1),
                "Only first step should run before abort");
        }

        [Test]
        public async Task Dispatcher_ContinueBatch_ContinuesAfterStepFailed()
        {
            var fixture = CreateDispatcherFixtureWithFailure(
                ConvaiActionBatchPolicy.Queue,
                ConvaiActionBatchFailurePolicy.ContinueBatch);
            bool batchCompleted = false;
            fixture.Dispatcher.OnBatchCompleted.AddListener(() => batchCompleted = true);

            fixture.Dispatcher.EnqueueActions(new[]
            {
                CreateAction("Move To", "cube"),
                CreateAction("Pick Up", "cube")
            });
            await WaitUntilAsync(() => batchCompleted);

            Assert.That(batchCompleted, Is.True);
            Assert.That(fixture.Executor.ExecutedActions.Count, Is.EqualTo(2),
                "Both steps should run under ContinueBatch");
        }

        [Test]
        public async Task Dispatcher_SuccessWithMessage_CompletesBatchWithoutAbort()
        {
            var fixture = CreateDispatcherFixture(
                ConvaiActionBatchPolicy.Queue,
                ConvaiActionBatchFailurePolicy.StopBatch);
            fixture.Executor.ResultToReturn = ConvaiActionExecutionResult.Succeeded("No held object to drop.");
            bool batchAborted = false;
            ConvaiActionStepReport completedReport = null;
            fixture.Dispatcher.OnBatchAborted.AddListener(() => batchAborted = true);
            fixture.Dispatcher.OnStepCompleted.AddListener(report => completedReport = report);

            fixture.Dispatcher.EnqueueActions(new[]
            {
                CreateAction("Drop"),
                CreateAction("Move To", "cube")
            });
            await WaitUntilAsync(() => completedReport != null && fixture.Executor.ExecutedActions.Count == 2);

            Assert.That(batchAborted, Is.False);
            Assert.That(completedReport.Result.Status, Is.EqualTo(ConvaiActionExecutionStatus.Succeeded));
            Assert.That(completedReport.Message, Is.EqualTo("No held object to drop."));
            Assert.That(completedReport.FailureMessage, Is.Empty);
        }

        [Test]
        public async Task Dispatcher_PerActionContinueOverride_ContinuesWhenGlobalStopsBatch()
        {
            var fixture = CreateDispatcherFixtureWithFailure(
                ConvaiActionBatchPolicy.Queue,
                ConvaiActionBatchFailurePolicy.StopBatch);
            ConvaiActionConfigSource source = fixture.GameObject.GetComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = fixture.GameObject.GetComponent<RecordingActionExecutor>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Move To",
                    TargetRequirement = ConvaiActionTargetRequirement.None,
                    Executor = executor,
                    FailurePolicyOverride = ConvaiActionFailurePolicyOverride.ContinueBatch
                },
                new()
                {
                    ActionName = "Pick Up",
                    TargetRequirement = ConvaiActionTargetRequirement.None,
                    Executor = executor
                }
            });
            // Both steps fail; the batch therefore ends aborted (the un-overridden second step
            // stops it) — the point under test is that the ContinueBatch override on the first
            // step let the second step execute at all.
            bool batchEnded = false;
            fixture.Dispatcher.OnBatchCompleted.AddListener(() => batchEnded = true);
            fixture.Dispatcher.OnBatchAborted.AddListener(() => batchEnded = true);

            fixture.Dispatcher.EnqueueActions(new[]
            {
                CreateAction("Move To", "cube"),
                CreateAction("Pick Up", "cube")
            });
            await WaitUntilAsync(() => batchEnded);

            Assert.That(fixture.Executor.ExecutedActions.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task Dispatcher_PerActionStopOverride_AbortsWhenGlobalContinuesBatch()
        {
            var fixture = CreateDispatcherFixtureWithFailure(
                ConvaiActionBatchPolicy.Queue,
                ConvaiActionBatchFailurePolicy.ContinueBatch);
            ConvaiActionConfigSource source = fixture.GameObject.GetComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = fixture.GameObject.GetComponent<RecordingActionExecutor>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Move To",
                    TargetRequirement = ConvaiActionTargetRequirement.None,
                    Executor = executor,
                    FailurePolicyOverride = ConvaiActionFailurePolicyOverride.StopBatch
                },
                new()
                {
                    ActionName = "Pick Up",
                    TargetRequirement = ConvaiActionTargetRequirement.None,
                    Executor = executor
                }
            });
            bool batchAborted = false;
            fixture.Dispatcher.OnBatchAborted.AddListener(() => batchAborted = true);

            fixture.Dispatcher.EnqueueActions(new[]
            {
                CreateAction("Move To", "cube"),
                CreateAction("Pick Up", "cube")
            });
            await WaitUntilAsync(() => batchAborted);

            Assert.That(fixture.Executor.ExecutedActions.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task Dispatcher_StopBatch_AbortsOnUnhandledResult()
        {
            var fixture = CreateDispatcherFixture(
                batchPolicy: ConvaiActionBatchPolicy.Queue,
                failurePolicy: ConvaiActionBatchFailurePolicy.StopBatch);
            fixture.Executor.ResultToReturn = ConvaiActionExecutionResult.Unhandled("test unhandled");
            bool batchAborted = false;
            fixture.Dispatcher.OnBatchAborted.AddListener(() => batchAborted = true);

            fixture.Dispatcher.EnqueueActions(new[]
            {
                CreateAction("Move To", "cube"),
                CreateAction("Move To", "cube")
            });
            await WaitUntilAsync(() => batchAborted);

            Assert.That(batchAborted, Is.True,
                "Unhandled executor result under StopBatch should abort the batch");
        }

        // ── Timeout ───────────────────────────────────────────────────────────────────────

        [Test]
        public async Task Dispatcher_FiresTimedOut_WhenStepExceedsTimeout()
        {
            var fixture = CreateDispatcherFixtureWithTimeout(
                timeoutSeconds: 0.05f,
                executorDelayMs: 200);
            ConvaiActionInvocation failedInvocation = null;
            fixture.Dispatcher.OnStepFailed.AddListener(inv => failedInvocation = inv);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => failedInvocation != null, timeoutMs: 2000);

            Assert.That(failedInvocation, Is.Not.Null);
        }

        [Test]
        public async Task Dispatcher_TimedOut_TriggersBatchAbort_WithStopBatchPolicy()
        {
            var fixture = CreateDispatcherFixtureWithTimeout(
                timeoutSeconds: 0.05f,
                executorDelayMs: 200,
                failurePolicy: ConvaiActionBatchFailurePolicy.StopBatch);
            bool batchAborted = false;
            fixture.Dispatcher.OnBatchAborted.AddListener(() => batchAborted = true);

            fixture.Dispatcher.EnqueueActions(new[]
            {
                CreateAction("Move To", "cube"),
                CreateAction("Pick Up", "cube")
            });
            await WaitUntilAsync(() => batchAborted, timeoutMs: 2000);

            Assert.That(batchAborted, Is.True);
        }

        [Test]
        public async Task Dispatcher_BatchCancellation_NotMistakenForTimeout()
        {
            var fixture = CreateDispatcherFixtureWithTimeout(
                timeoutSeconds: 5f,
                executorDelayMs: 500);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await Task.Delay(40);
            fixture.GameObject.SetActive(false);
            await Task.Delay(40);

            Assert.That(fixture.Executor.CancellationObserved, Is.True,
                "Batch cancellation should be observed by the executor, not silently timed out");
        }

        // ── Result events ─────────────────────────────────────────────────────────────────

        [Test]
        public async Task Dispatcher_FiresOnStepSucceeded_OnSuccess()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.Queue);
            bool succeededFired = false;
            fixture.Dispatcher.OnStepSucceeded.AddListener(_ => succeededFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => succeededFired);

            Assert.That(succeededFired, Is.True);
        }

        [Test]
        public async Task Dispatcher_EmitsStepCompletedReport_OnSuccess()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.Queue);
            ConvaiActionStepReport completedReport = null;
            fixture.Dispatcher.OnStepCompleted.AddListener(report => completedReport = report);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => completedReport != null);

            Assert.That(completedReport.Result.Status, Is.EqualTo(ConvaiActionExecutionStatus.Succeeded));
            Assert.That(completedReport.BatchAborted, Is.False);
            Assert.That(completedReport.Message, Is.Empty);
            Assert.That(completedReport.FailureMessage, Is.Empty);
        }

        [Test]
        public async Task Dispatcher_FiresOnStepFailed_OnFailure()
        {
            var fixture = CreateDispatcherFixtureWithFailure(
                ConvaiActionBatchPolicy.Queue,
                ConvaiActionBatchFailurePolicy.ContinueBatch);
            bool failedFired = false;
            fixture.Dispatcher.OnStepFailed.AddListener(_ => failedFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => failedFired);

            Assert.That(failedFired, Is.True);
        }

        [Test]
        public async Task Dispatcher_EmitsStepCompletedReport_OnFailure()
        {
            var fixture = CreateDispatcherFixtureWithFailure(
                ConvaiActionBatchPolicy.Queue,
                ConvaiActionBatchFailurePolicy.StopBatch);
            ConvaiActionStepReport completedReport = null;
            fixture.Dispatcher.OnStepCompleted.AddListener(report => completedReport = report);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => completedReport != null);

            Assert.That(completedReport.Result.Status, Is.EqualTo(ConvaiActionExecutionStatus.Failed));
            Assert.That(completedReport.BatchAborted, Is.True);
            StringAssert.Contains("test failure", completedReport.FailureMessage);
            StringAssert.Contains("batch will abort", completedReport.FailureMessage);
        }

        [Test]
        public async Task Dispatcher_EmitsStepCompletedReport_OnTimedOut()
        {
            var fixture = CreateDispatcherFixtureWithTimeout(
                timeoutSeconds: 0.05f,
                executorDelayMs: 200,
                failurePolicy: ConvaiActionBatchFailurePolicy.StopBatch);
            ConvaiActionStepReport completedReport = null;
            fixture.Dispatcher.OnStepCompleted.AddListener(report => completedReport = report);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => completedReport != null, timeoutMs: 2000);

            Assert.That(completedReport.Result.Status, Is.EqualTo(ConvaiActionExecutionStatus.TimedOut));
            Assert.That(completedReport.BatchAborted, Is.True);
            StringAssert.Contains("TimedOut", completedReport.FailureMessage);
        }

        [Test]
        public async Task Dispatcher_EmitsStepCompletedReport_OnUnhandled()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.Queue);
            fixture.Executor.ResultToReturn = ConvaiActionExecutionResult.Unhandled("test unhandled");
            ConvaiActionStepReport completedReport = null;
            fixture.Dispatcher.OnStepCompleted.AddListener(report => completedReport = report);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => completedReport != null);

            Assert.That(completedReport.Result.Status, Is.EqualTo(ConvaiActionExecutionStatus.Unhandled));
            Assert.That(completedReport.BatchAborted, Is.True);
            StringAssert.Contains("test unhandled", completedReport.FailureMessage);
        }

        [Test]
        public async Task Dispatcher_EmitsStepCompletedReport_OnCanceled()
        {
            var fixture = CreateDispatcherFixture(
                ConvaiActionBatchPolicy.Queue,
                ConvaiActionBatchFailurePolicy.StopBatch);
            fixture.Executor.DelayMs = 250;
            ConvaiActionStepReport completedReport = null;
            fixture.Dispatcher.OnStepCompleted.AddListener(report => completedReport = report);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await Task.Delay(40);
            fixture.GameObject.SetActive(false);
            await WaitUntilAsync(() => completedReport?.Result.Status == ConvaiActionExecutionStatus.Canceled);

            Assert.That(completedReport.BatchAborted, Is.True);
            StringAssert.Contains("Canceled", completedReport.FailureMessage);
        }

        [Test]
        public void ActionConfigSource_DeduplicatesDefinitions_BeforeSerializingActionConfig()
        {
            GameObject gameObject = CreateCharacterGameObject("char-dedupe", "Dedupe Test", out _);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();

            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new() { ActionName = "Move To", Executor = executor },
                new() { ActionName = "move to", Executor = executor }
            });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate action definition 'move to'|Duplicate action definition 'Move To'"));
            ConvaiActionConfig config = source.BuildActionConfig();

            Assert.That(config.Actions.Count, Is.EqualTo(1));
            Assert.That(config.Actions[0], Is.EqualTo("Move To"));
        }

        [Test]
        public void ActionConfigSource_NormalizesValidInitialAttentionObject()
        {
            GameObject gameObject = CreateCharacterGameObject("char-attention-valid", "Attention Valid Test", out _);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();

            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new() { ActionName = "Move To", Executor = executor }
            });
            source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
            {
                new() { Name = " cube " }
            });
            SetPrivateField(source, "_initialAttentionObject", "CUBE");

            ConvaiActionConfig config = source.BuildActionConfig();

            Assert.That(config, Is.Not.Null);
            Assert.That(config.CurrentAttentionObject, Is.EqualTo("cube"));
        }

        [Test]
        public void ActionConfigSource_OmitsInvalidInitialAttentionObject()
        {
            GameObject gameObject = CreateCharacterGameObject("char-attention-invalid", "Attention Invalid Test", out _);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();

            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new() { ActionName = "Move To", Executor = executor }
            });
            source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
            {
                new() { Name = "cube" }
            });
            SetPrivateField(source, "_initialAttentionObject", "lever");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Initial attention object 'lever'"));
            ConvaiActionConfig config = source.BuildActionConfig();

            Assert.That(config, Is.Not.Null);
            Assert.That(config.CurrentAttentionObject, Is.Null.Or.Empty);
        }

        [Test]
        public void ActionConfigSource_OmitsConfig_WhenOnlyTargetsAndAttentionAreAuthored()
        {
            GameObject gameObject = CreateCharacterGameObject("char-targets-only", "Targets Only Test", out _);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();

            source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
            {
                new() { Name = "cube" }
            });
            source.ReplaceCharacters(new List<ConvaiActionCharacterDefinition>
            {
                new() { Name = "Player" }
            });
            SetPrivateField(source, "_initialAttentionObject", "cube");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("action definitions"));
            ConvaiActionConfig config = source.BuildActionConfig();

            Assert.That(config, Is.Null);
        }

        [Test]
        public void ActionConfigSource_OmitsInvalidExecutorDefinitions_BeforeSerializingActionConfig()
        {
            GameObject gameObject = CreateCharacterGameObject("char-invalid-action", "Invalid Action", out _);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new() { ActionName = "Move To", Executor = executor },
                new() { ActionName = "Dance", Executor = null },
                new() { ActionName = "Look", Executor = gameObject.AddComponent<NonExecutorMonoBehaviour>() }
            });

            ConvaiActionConfig config = source.BuildActionConfig();

            CollectionAssert.AreEqual(new[] { "Move To" }, config.Actions);

            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ActionConfigSource_KeepsValidDuplicate_WhenEarlierDuplicateIsNotExecutable()
        {
            GameObject gameObject = CreateCharacterGameObject("char-valid-duplicate", "Valid Duplicate", out _);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new() { ActionName = "Move To", Executor = null },
                new() { ActionName = "move to", Executor = executor }
            });

            ConvaiActionConfig config = source.BuildActionConfig();
            IReadOnlyList<ConvaiActionDefinition> definitions = source.GetEffectiveDefinitions(requireExecutable: true);

            CollectionAssert.AreEqual(new[] { "move to" }, config.Actions);
            Assert.That(definitions.Count, Is.EqualTo(1));
            Assert.That(definitions[0].Executor, Is.SameAs(executor));

            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ActionResolution_BuildsObjectNameToEntityLookup_FromActionConfigObjectsOnly()
        {
            var cube = new GameObject("Cube Entity");
            var lever = new GameObject("Lever Entity");
            try
            {
                var config = new ConvaiActionConfig
                {
                    Objects = new List<ConvaiActionObjectDefinition>
                    {
                        new() { Name = "cube", GameObjectReference = cube },
                        new() { Name = "lever", GameObjectReference = lever }
                    }
                };

                Dictionary<string, GameObject> lookup = ConvaiResolvedActionTarget.BuildObjectEntityLookup(config);

                Assert.That(lookup.Count, Is.EqualTo(2));
                Assert.That(lookup["cube"], Is.SameAs(cube));
                Assert.That(lookup["lever"], Is.SameAs(lever));
                Assert.That(lookup.ContainsKey("scene-only-object"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cube);
                UnityEngine.Object.DestroyImmediate(lever);
            }
        }

        [Test]
        public void ActionConfigValidator_ReportsAuthoringProblems()
        {
            GameObject gameObject = CreateCharacterGameObject("char-validator", "Validator", out _);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new() { ActionName = "Move To", Executor = executor },
                new() { ActionName = "move to", Executor = executor },
                new() { ActionName = "   ", Executor = executor },
                new() { ActionName = "Dance", Executor = null }
            });
            source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
            {
                new() { Name = "SharedTarget", Description = "", GameObjectReference = null }
            });
            source.ReplaceCharacters(new List<ConvaiActionCharacterDefinition>
            {
                new() { Name = "SharedTarget", Bio = "", GameObjectReference = null }
            });
            SetPrivateField(source, "_initialAttentionObject", "missing_object");

            IReadOnlyList<ConvaiActionConfigDiagnostic> diagnostics =
                ConvaiActionConfigValidator.Validate(source);

            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message.Contains("Duplicate action definition")));
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message.Contains("blank action name")));
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message.Contains("missing a valid executor")));
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message.Contains("Duplicate target name")));
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message.Contains("missing object description")));
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message.Contains("missing character bio")));
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message.Contains("missing GameObject reference")));
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Message.Contains("Initial attention object")));
            Assert.That(diagnostics.Any(diagnostic => diagnostic.Severity == ConvaiActionConfigDiagnosticSeverity.Error));

            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ActionDefinition_ToActionConfigString_RendersTypedParametersDeterministically()
        {
            var definition = new ConvaiActionDefinition
            {
                ActionName = "Put",
                Description = "Put an item into a container.",
                Parameters = new List<ConvaiActionParameterDefinition>
                {
                    new()
                    {
                        Name = "item",
                        Description = "Inventory item.",
                        Type = ConvaiActionParameterType.String
                    },
                    new()
                    {
                        Name = "container",
                        Description = "Destination container.",
                        Type = ConvaiActionParameterType.Reference,
                        Connector = "on"
                    },
                    new()
                    {
                        Name = "speed",
                        Type = ConvaiActionParameterType.Choice,
                        Connector = "at",
                        Choices = new List<string> { "slow", "fast" }
                    }
                }
            };

            Assert.That(definition.ToActionConfigString(),
                Is.EqualTo("Put {item: string} on {container: reference} at {speed: choice [slow|fast]} - Put an item into a container. item: Inventory item. container: Destination container."));
        }

        [Test]
        public void ActionDefinition_ToActionConfigString_UsesAsciiWireText()
        {
            var definition = new ConvaiActionDefinition
            {
                ActionName = "Move To",
                Description = "Move — quickly",
                Parameters = new List<ConvaiActionParameterDefinition>
                {
                    new()
                    {
                        Name = "destination",
                        Description = "Café destination",
                        Type = ConvaiActionParameterType.Reference,
                        Connector = "toward"
                    }
                }
            };

            Assert.That(definition.ToActionConfigString(),
                Is.EqualTo("Move To toward {destination: reference} - Move quickly destination: Cafe destination"));
        }

        [Test]
        public void ActionConfigSource_BuildActionConfig_SerializesRenderedActionTemplates()
        {
            GameObject gameObject = CreateCharacterGameObject("char-rendered-actions", "Rendered Actions", out _);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();

            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Put",
                    Executor = executor,
                    Parameters = new List<ConvaiActionParameterDefinition>
                    {
                        new() { Name = "item", Type = ConvaiActionParameterType.String },
                        new() { Name = "container", Type = ConvaiActionParameterType.Reference, Connector = "on" }
                    }
                }
            });

            ConvaiActionConfig config = source.BuildActionConfig();

            CollectionAssert.AreEqual(new[] { "Put {item: string} on {container: reference}" }, config.Actions);
        }

        [Test]
        public void ActionResponseParser_MapsBraceWrappedValuesToTypedParameters()
        {
            var definitions = new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Put",
                    Parameters = new List<ConvaiActionParameterDefinition>
                    {
                        new() { Name = "item", Type = ConvaiActionParameterType.String },
                        new() { Name = "container", Type = ConvaiActionParameterType.String }
                    }
                }
            };

            ConvaiActionCommand command = ConvaiActionResponseParser.Enrich(
                new ConvaiActionCommand("Put {red key} {wood drawer}"), null, definitions);

            Assert.That(command.Parameters["item"].StringValue, Is.EqualTo("red key"));
            Assert.That(command.Parameters["container"].StringValue, Is.EqualTo("wood drawer"));
            Assert.That(command.ActionString, Is.EqualTo("Put {red key} {wood drawer}"));
        }

        [Test]
        public void ActionResponseParser_SplitsNamedAnchorsAndConnectorValues()
        {
            var definitions = new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Put",
                    Parameters = new List<ConvaiActionParameterDefinition>
                    {
                        new() { Name = "item", Type = ConvaiActionParameterType.String },
                        new() { Name = "container", Type = ConvaiActionParameterType.String, Connector = "on" }
                    }
                }
            };

            ConvaiActionCommand named = ConvaiActionResponseParser.Enrich(
                new ConvaiActionCommand("Put", "item: key container: drawer"), null, definitions);
            ConvaiActionCommand connector = ConvaiActionResponseParser.Enrich(
                new ConvaiActionCommand("Put", "key on drawer"), null, definitions);

            Assert.That(named.Parameters["item"].StringValue, Is.EqualTo("key"));
            Assert.That(named.Parameters["container"].StringValue, Is.EqualTo("drawer"));
            Assert.That(connector.Parameters["item"].StringValue, Is.EqualTo("key"));
            Assert.That(connector.Parameters["container"].StringValue, Is.EqualTo("drawer"));
        }

        [Test]
        public void ActionResponseParser_CoercesReferenceNumberBoolAndChoice()
        {
            GameObject cube = new("ActionSystemTests_cube");
            try
            {
                var config = new ConvaiActionConfig
                {
                    Objects = new List<ConvaiActionObjectDefinition>
                    {
                        new() { Name = "Cube", Description = "A cube.", GameObjectReference = cube }
                    }
                };
                var definitions = new List<ConvaiActionDefinition>
                {
                    new()
                    {
                        ActionName = "Configure",
                        Parameters = new List<ConvaiActionParameterDefinition>
                        {
                            new() { Name = "target", Type = ConvaiActionParameterType.Reference },
                            new() { Name = "seconds", Type = ConvaiActionParameterType.Number },
                            new() { Name = "enabled", Type = ConvaiActionParameterType.Bool },
                            new() { Name = "mode", Type = ConvaiActionParameterType.Choice, Choices = new List<string> { "fast", "slow" } }
                        }
                    }
                };

                ConvaiActionCommand command = ConvaiActionResponseParser.Enrich(
                    new ConvaiActionCommand("Configure", "{Cube} {2.5} {yes} {fast}"), config, definitions);

                Assert.That(command.Parameters["target"].ResolvedReference?.Name, Is.EqualTo("Cube"));
                Assert.That(command.Parameters["target"].ResolvedReference?.Kind, Is.EqualTo(ConvaiActionTargetKind.Object));
                Assert.That(command.Parameters["seconds"].NumberValue, Is.EqualTo(2.5f).Within(0.001f));
                Assert.That(command.Parameters["enabled"].BoolValue, Is.True);
                Assert.That(command.Parameters["mode"].IsConstraintMatch, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void ActionResponseParser_RenderedActionNameUsesTargetForParameters()
        {
            var definitions = new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Put",
                    Parameters = new List<ConvaiActionParameterDefinition>
                    {
                        new() { Name = "item", Type = ConvaiActionParameterType.String },
                        new() { Name = "container", Type = ConvaiActionParameterType.String, Connector = "on" }
                    }
                }
            };
            string rendered = definitions[0].ToActionConfigString();

            ConvaiActionCommand command = ConvaiActionResponseParser.Enrich(
                new ConvaiActionCommand(rendered, "red key on drawer"),
                null,
                definitions);

            Assert.That(command.Name, Is.EqualTo("Put"));
            Assert.That(command.Parameters["item"].StringValue, Is.EqualTo("red key"));
            Assert.That(command.Parameters["container"].StringValue, Is.EqualTo("drawer"));
        }

        [Test]
        public void ActionResponseParser_ChoiceMatchingTrimsAuthoredChoices()
        {
            var definitions = new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Set Beacon",
                    Parameters = new List<ConvaiActionParameterDefinition>
                    {
                        new()
                        {
                            Name = "color",
                            Type = ConvaiActionParameterType.Choice,
                            Choices = new List<string> { " red ", " green " }
                        }
                    }
                }
            };

            ConvaiActionCommand command = ConvaiActionResponseParser.Enrich(
                new ConvaiActionCommand("Set Beacon", "GREEN"),
                null,
                definitions);

            Assert.That(command.Parameters["color"].IsConstraintMatch, Is.True);
        }

        [Test]
        public void ActionResponseParser_UnknownActionPreservesTargetParameter()
        {
            ConvaiActionCommand command = ConvaiActionResponseParser.Enrich(
                new ConvaiActionCommand("Unknown", "raw target"), null, Array.Empty<ConvaiActionDefinition>());

            Assert.That(command.Name, Is.EqualTo("Unknown"));
            Assert.That(command.Target, Is.EqualTo("raw target"));
            Assert.That(command.Parameters["target"].StringValue, Is.EqualTo("raw target"));
        }

        [Test]
        public void ActionInvocation_GetReferencePreservesResolvedReferenceKind()
        {
            GameObject gameObject = CreateCharacterGameObject("char-kind", "Reference Kind", out ConvaiCharacter character);
            GameObject objectTarget = new("ActionSystemTests_SharedObject");
            GameObject characterTarget = new("ActionSystemTests_SharedCharacter");
            try
            {
                ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
                RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();
                source.ReplaceDefinitions(new List<ConvaiActionDefinition>
                {
                    new()
                    {
                        ActionName = "Inspect",
                        Executor = executor,
                        Parameters = new List<ConvaiActionParameterDefinition>
                        {
                            new() { Name = "subject", Type = ConvaiActionParameterType.Reference }
                        }
                    }
                });
                source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
                {
                    new() { Name = "Shared", GameObjectReference = objectTarget }
                });
                source.ReplaceCharacters(new List<ConvaiActionCharacterDefinition>
                {
                    new() { Name = "Shared", GameObjectReference = characterTarget }
                });

                ConvaiActionCommand command = new("Inspect")
                {
                    Parameters = new Dictionary<string, ConvaiActionParameterValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["subject"] = new()
                        {
                            Type = ConvaiActionParameterType.Reference,
                            RawValue = "Shared",
                            StringValue = "Shared",
                            ResolvedReference = new ConvaiActionParameterReference("Shared", ConvaiActionTargetKind.Character)
                        }
                    }
                };
                ConvaiActionInvocation invocation = CreateInvocation(command, null, null, character);

                ConvaiResolvedActionTarget reference = invocation.GetReference("subject");

                Assert.That(reference?.Kind, Is.EqualTo(ConvaiActionTargetKind.Character));
                Assert.That(reference?.GameObjectReference, Is.EqualTo(characterTarget));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(objectTarget);
                UnityEngine.Object.DestroyImmediate(characterTarget);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task TypedExecutor_ReceivesStronglyBoundParameters()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.Queue);
            var typedExecutor = fixture.GameObject.AddComponent<RecordingTypedPutExecutor>();
            ConvaiActionConfigSource source = fixture.GameObject.GetComponent<ConvaiActionConfigSource>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Put",
                    Executor = typedExecutor,
                    Parameters = new List<ConvaiActionParameterDefinition>
                    {
                        new() { Name = "item", Type = ConvaiActionParameterType.String },
                        new() { Name = "container", Type = ConvaiActionParameterType.Reference, Connector = "on" }
                    }
                }
            });
            source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
            {
                new() { Name = "drawer", GameObjectReference = fixture.GameObject }
            });

            ConvaiActionCommand enriched = ConvaiActionResponseParser.Enrich(
                new ConvaiActionCommand("Put", "red key on drawer"),
                fixture.Character.ActionConfig,
                GetRuntimeActionDefinitions(fixture.Character));

            fixture.Dispatcher.EnqueueActions(new[] { enriched });
            await WaitUntilAsync(() => typedExecutor.LastParameters != null);

            Assert.That(typedExecutor.LastParameters.Item, Is.EqualTo("red key"));
            Assert.That(typedExecutor.LastParameters.Container?.Name, Is.EqualTo("drawer"));
        }

        [Test]
        public async Task Dispatcher_WaitsForSpeechGate_BeforeFirstAction()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.Queue);
            SetPrivateField(fixture.Dispatcher, "_speechGateTimeoutSeconds", 1f);
            ConvaiActionConfigSource source = fixture.GameObject.GetComponent<ConvaiActionConfigSource>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Move To",
                    Executor = fixture.Executor,
                    WaitForBotSpeech = true
                }
            });

            fixture.Dispatcher.EnqueueActions(new[]
            {
                new ConvaiActionCommand("Move To")
                {
                    WaitForBotSpeech = true
                }
            });

            await Task.Delay(80);
            Assert.That(fixture.Executor.ExecutedActions, Is.Empty);

            RaiseCharacterSpeechStarted(fixture.Character);
            await WaitUntilAsync(() => fixture.Executor.ExecutedActions.Count == 1);

            CollectionAssert.AreEqual(new[] { "Move To" }, fixture.Executor.ExecutedActions);
        }

        [Test]
        public async Task Dispatcher_FiresOnBatchAborted_WhenAborted()
        {
            var fixture = CreateDispatcherFixtureWithFailure(
                ConvaiActionBatchPolicy.Queue,
                ConvaiActionBatchFailurePolicy.StopBatch);
            bool abortedFired = false;
            fixture.Dispatcher.OnBatchAborted.AddListener(() => abortedFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => abortedFired);

            Assert.That(abortedFired, Is.True);
        }

        [Test]
        public async Task Dispatcher_FiresOnBatchCompleted_WhenNormal()
        {
            var fixture = CreateDispatcherFixture(batchPolicy: ConvaiActionBatchPolicy.Queue);
            bool completedFired = false;
            fixture.Dispatcher.OnBatchCompleted.AddListener(() => completedFired = true);

            fixture.Dispatcher.EnqueueActions(new[] { CreateAction("Move To", "cube") });
            await WaitUntilAsync(() => completedFired);

            Assert.That(completedFired, Is.True);
        }

        // ── Adapter / config source integration ──────────────────────────────────────────

        [Test]
        public async Task RoomConnectionRuntimeAdapter_UsesPerCallActionOverride_BeforeCharacterSource()
        {
            GameObject gameObject = CreateCharacterGameObject("char-override", "Override Test", out ConvaiCharacter character);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Move To",
                    Executor = executor,
                    Parameters = new List<ConvaiActionParameterDefinition>
                    {
                        new() { Name = "destination", Type = ConvaiActionParameterType.Reference }
                    }
                }
            });
            source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
            {
                new() { Name = "cube" }
            });

            var overrideConfig = new ConvaiActionConfig
            {
                Actions = new List<string> { "Wave" },
                Objects = new List<ConvaiActionObjectDefinition> { new() { Name = "lever" } },
                CurrentAttentionObject = "lever"
            };

            CapturingRoomController controller = new();
            RoomConnectionRuntimeAdapter adapter = CreateRuntimeAdapter(character, controller, new RoomSessionConnectOptions
            {
                TurnTaking = TurnTakingOptions.CreateHandsFreeDefault(),
                ActionConfigOverride = overrideConfig
            });

            RoomConnectionAttemptResult result = await adapter.ConnectAsync(CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(controller.LastJoinOptions?.ResolvedActionConfig?.Actions[0], Is.EqualTo("Wave"));
            Assert.That(controller.LastJoinOptions?.ResolvedActionConfig?.Objects[0].Name, Is.EqualTo("lever"));
            Assert.That(controller.LastJoinOptions?.ResolvedActionConfig?.CurrentAttentionObject, Is.EqualTo("lever"));
        }

        [Test]
        public async Task RoomConnectionRuntimeAdapter_UsesPerCallDefinitionOverride_ForRuntimeExecutionBindings()
        {
            GameObject gameObject = CreateCharacterGameObject("char-definition-override", "Definition Override Test", out ConvaiCharacter character);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor sourceExecutor = gameObject.AddComponent<RecordingActionExecutor>();
            RecordingActionExecutor overrideExecutor = gameObject.AddComponent<RecordingActionExecutor>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new() { ActionName = "Move To", Executor = sourceExecutor }
            });

            var overrideConfig = new ConvaiActionConfig
            {
                Actions = new List<string> { "Wave" }
            };

            CapturingRoomController controller = new();
            RoomConnectionRuntimeAdapter adapter = CreateRuntimeAdapter(character, controller, new RoomSessionConnectOptions
            {
                TurnTaking = TurnTakingOptions.CreateHandsFreeDefault(),
                ActionConfigOverride = overrideConfig,
                ActionDefinitionsOverride = new List<ConvaiActionDefinition>
                {
                    new() { ActionName = "Wave", Executor = overrideExecutor }
                }
            });

            RoomConnectionAttemptResult result = await adapter.ConnectAsync(CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            IReadOnlyList<ConvaiActionDefinition> runtimeDefinitions = GetRuntimeActionDefinitions(character);
            Assert.That(runtimeDefinitions.Count, Is.EqualTo(1));
            Assert.That(runtimeDefinitions[0].ActionName, Is.EqualTo("Wave"));
            Assert.That(runtimeDefinitions[0].Executor, Is.EqualTo(overrideExecutor));
        }

        [Test]
        public async Task RoomConnectionRuntimeAdapter_FallsBackToCharacterActionSource_WhenNoOverride()
        {
            GameObject gameObject = CreateCharacterGameObject("char-source", "Source Test", out ConvaiCharacter character);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();
            source.ReplaceDefinitions(new List<ConvaiActionDefinition>
            {
                new()
                {
                    ActionName = "Move To",
                    Executor = executor,
                    Parameters = new List<ConvaiActionParameterDefinition>
                    {
                        new()
                        {
                            Name = "destination",
                            Type = ConvaiActionParameterType.Reference,
                            Connector = "toward"
                        }
                    }
                }
            });
            source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
            {
                new() { Name = "cube" }
            });
            SetPrivateField(source, "_initialAttentionObject", "cube");

            CapturingRoomController controller = new();
            RoomConnectionRuntimeAdapter adapter = CreateRuntimeAdapter(character, controller, null);

            RoomConnectionAttemptResult result = await adapter.ConnectAsync(CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(controller.LastJoinOptions?.ResolvedActionConfig?.Actions[0], Is.EqualTo("Move To toward {destination: reference}"));
            Assert.That(controller.LastJoinOptions?.ResolvedActionConfig?.Objects[0].Name, Is.EqualTo("cube"));
            Assert.That(controller.LastJoinOptions?.ResolvedActionConfig?.CurrentAttentionObject, Is.EqualTo("cube"));

            IReadOnlyList<ConvaiActionDefinition> runtimeDefinitions = GetRuntimeActionDefinitions(character);
            Assert.That(runtimeDefinitions.Count, Is.EqualTo(1));
            Assert.That(runtimeDefinitions[0].ActionName, Is.EqualTo("Move To"));
        }

        [Test]
        public async Task RoomConnectionRuntimeAdapter_UsesCharacterSessionId_WhenResumeEnabled()
        {
            GameObject gameObject = CreateCharacterGameObject("char-session", "Session Test", out ConvaiCharacter character);
            SetPrivateField(character, "_enableSessionResume", true);
            character.SetCharacterSessionId("manual-character-session");

            CapturingRoomController controller = new();
            RoomConnectionRuntimeAdapter adapter = CreateRuntimeAdapter(character, controller, null);

            RoomConnectionAttemptResult result = await adapter.ConnectAsync(CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(controller.LastStoredSessionId, Is.EqualTo("manual-character-session"));
            Assert.That(controller.LastJoinOptions?.CharacterSessionId, Is.EqualTo("manual-character-session"));
            Assert.That(character.CharacterSessionId, Is.EqualTo("character-session-id"));
        }

        [Test]
        public async Task RoomConnectionRuntimeAdapter_DoesNotLoadStoredSession_WhenCharacterSessionIdBlank()
        {
            GameObject gameObject = CreateCharacterGameObject("char-blank-session", "Blank Session Test", out ConvaiCharacter character);
            SetPrivateField(character, "_enableSessionResume", true);
            character.ClearCharacterSessionId();

            CapturingRoomController controller = new();
            InMemorySessionPersistence persistence = new();
            persistence.SaveSession("char-blank-session", "hidden-stored-session");
            RoomConnectionRuntimeAdapter adapter = CreateRuntimeAdapter(character, controller, null, persistence);

            RoomConnectionAttemptResult result = await adapter.ConnectAsync(CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(controller.LastStoredSessionId, Is.Null);
            Assert.That(controller.LastJoinOptions?.CharacterSessionId, Is.Null);
            Assert.That(character.CharacterSessionId, Is.EqualTo("character-session-id"));
        }

        [Test]
        public async Task NavMeshMoveToExecutor_Fails_WhenAgentMissing()
        {
            GameObject gameObject = CreateCharacterGameObject("char-nav-no-agent", "Nav No Agent", out _);
            GameObject target = new("ActionSystemTests_Target");
            try
            {
                ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
                NavMeshMoveToActionExecutor mover = gameObject.AddComponent<NavMeshMoveToActionExecutor>();
                source.ReplaceDefinitions(new List<ConvaiActionDefinition>
                {
                    new()
                    {
                        ActionName = "Move To",
                        TargetRequirement = ConvaiActionTargetRequirement.Object,
                        Executor = mover
                    }
                });
                source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
                {
                    new() { Name = "target", GameObjectReference = target }
                });
                ConvaiActionDispatcher dispatcher = gameObject.AddComponent<ConvaiActionDispatcher>();
                ConvaiActionStepReport report = null;
                dispatcher.OnStepCompleted.AddListener(stepReport => report = stepReport);

                dispatcher.EnqueueActions(new[] { CreateAction("Move To", "target") });
                await WaitUntilAsync(() => report != null);

                Assert.That(report.Result.Status, Is.EqualTo(ConvaiActionExecutionStatus.Failed));
                StringAssert.Contains("NavMeshAgent", report.FailureMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task NavMeshMoveToExecutor_Fails_WhenAgentIsNotOnNavMesh()
        {
            GameObject gameObject = CreateCharacterGameObject("char-nav-off-mesh", "Nav Off Mesh", out _);
            GameObject target = new("ActionSystemTests_Target");
            try
            {
                ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
                NavMeshAgent agent = gameObject.AddComponent<NavMeshAgent>();
                NavMeshMoveToActionExecutor mover = gameObject.AddComponent<NavMeshMoveToActionExecutor>();
                source.ReplaceDefinitions(new List<ConvaiActionDefinition>
                {
                    new()
                    {
                        ActionName = "Move To",
                        TargetRequirement = ConvaiActionTargetRequirement.Object,
                        Executor = mover,
                        TimeoutSeconds = 0.05f
                    }
                });
                source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
                {
                    new() { Name = "target", GameObjectReference = target }
                });
                ConvaiActionDispatcher dispatcher = gameObject.AddComponent<ConvaiActionDispatcher>();
                ConvaiActionStepReport report = null;
                dispatcher.OnStepCompleted.AddListener(stepReport => report = stepReport);

                dispatcher.EnqueueActions(new[] { CreateAction("Move To", "target") });
                await WaitUntilAsync(() => report != null, timeoutMs: 2000);

                Assert.That(agent.isOnNavMesh, Is.False);
                Assert.That(report.Result.Status, Is.EqualTo(ConvaiActionExecutionStatus.Failed));
                StringAssert.Contains("not on a NavMesh", report.FailureMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task Dispatcher_TargetRequirementNone_DoesNotPromoteReferenceParameterToResolvedTarget()
        {
            GameObject gameObject = CreateCharacterGameObject("char-param-only", "Param Only", out _);
            GameObject item = new("ActionSystemTests_ParamOnlyItem");
            GameObject container = new("ActionSystemTests_ParamOnlyContainer");
            try
            {
                ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
                RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();
                source.ReplaceDefinitions(new List<ConvaiActionDefinition>
                {
                    new()
                    {
                        ActionName = "Put",
                        TargetRequirement = ConvaiActionTargetRequirement.None,
                        Executor = executor,
                        Parameters = new List<ConvaiActionParameterDefinition>
                        {
                            new() { Name = "item", Type = ConvaiActionParameterType.Reference },
                            new() { Name = "container", Type = ConvaiActionParameterType.Reference, Connector = "on" }
                        }
                    }
                });
                source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
                {
                    new() { Name = "silver_key", GameObjectReference = item },
                    new() { Name = "display_pedestal", GameObjectReference = container }
                });
                ConvaiActionDispatcher dispatcher = gameObject.AddComponent<ConvaiActionDispatcher>();
                ConvaiActionInvocation started = null;
                dispatcher.OnStepStarted.AddListener(invocation => started = invocation);

                ConvaiActionCommand command = ConvaiActionResponseParser.Enrich(
                    new ConvaiActionCommand("Put", "silver_key on display_pedestal"),
                    gameObject.GetComponent<ConvaiCharacter>().ActionConfig,
                    GetRuntimeActionDefinitions(gameObject.GetComponent<ConvaiCharacter>()));

                dispatcher.EnqueueActions(new[] { command });
                await WaitUntilAsync(() => started != null);

                Assert.That(started.ResolvedTarget, Is.Null);
                Assert.That(started.GetReference("item")?.Name, Is.EqualTo("silver_key"));
                Assert.That(started.GetReference("container")?.Name, Is.EqualTo("display_pedestal"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(item);
                UnityEngine.Object.DestroyImmediate(container);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task Dispatcher_ParameterFallbackTargetResolutionPreservesReferenceKind()
        {
            GameObject gameObject = CreateCharacterGameObject("char-kind-fallback", "Kind Fallback", out _);
            GameObject objectTarget = new("ActionSystemTests_SharedObject");
            GameObject characterTarget = new("ActionSystemTests_SharedCharacter");
            try
            {
                ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
                RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();
                source.ReplaceDefinitions(new List<ConvaiActionDefinition>
                {
                    new()
                    {
                        ActionName = "Follow",
                        TargetRequirement = ConvaiActionTargetRequirement.Character,
                        Executor = executor,
                        Parameters = new List<ConvaiActionParameterDefinition>
                        {
                            new() { Name = "character", Type = ConvaiActionParameterType.Reference }
                        }
                    }
                });
                source.ReplaceObjects(new List<ConvaiActionObjectDefinition>
                {
                    new() { Name = "Shared", GameObjectReference = objectTarget }
                });
                source.ReplaceCharacters(new List<ConvaiActionCharacterDefinition>
                {
                    new() { Name = "Shared", GameObjectReference = characterTarget }
                });
                ConvaiActionDispatcher dispatcher = gameObject.AddComponent<ConvaiActionDispatcher>();
                ConvaiActionInvocation started = null;
                dispatcher.OnStepStarted.AddListener(invocation => started = invocation);
                ConvaiActionCommand command = new("Follow")
                {
                    Parameters = new Dictionary<string, ConvaiActionParameterValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["character"] = new()
                        {
                            Type = ConvaiActionParameterType.Reference,
                            RawValue = "Shared",
                            StringValue = "Shared",
                            ResolvedReference = new ConvaiActionParameterReference("Shared", ConvaiActionTargetKind.Character)
                        }
                    }
                };

                dispatcher.EnqueueActions(new[] { command });
                await WaitUntilAsync(() => started != null);

                Assert.That(started.ResolvedTarget?.Kind, Is.EqualTo(ConvaiActionTargetKind.Character));
                Assert.That(started.ResolvedTarget?.GameObjectReference, Is.EqualTo(characterTarget));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(objectTarget);
                UnityEngine.Object.DestroyImmediate(characterTarget);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        // ── Fixture helpers ───────────────────────────────────────────────────────────────

        private static ConvaiActionCommand CreateAction(string name, string target = null) => new(name, target);

        private static ConvaiActionInvocation CreateInvocation(
            ConvaiActionCommand command,
            ConvaiActionDefinition definition,
            ConvaiResolvedActionTarget resolvedTarget,
            ConvaiCharacter character) =>
            new(command, definition, resolvedTarget, character, 0, 0);

        /// <summary>
        ///     One ceremony for every dispatcher fixture: character rig + config source + recording
        ///     executor + dispatcher. Authored lists go through the internal Replace* seams; the
        ///     dispatcher's inspector knobs go through <see cref="SetPrivateField" />.
        /// </summary>
        private static (GameObject GameObject, ConvaiCharacter Character, ConvaiActionDispatcher Dispatcher,
            RecordingActionExecutor Executor) BuildDispatcherFixture(
            string characterId,
            string characterName,
            Func<RecordingActionExecutor, GameObject, List<ConvaiActionDefinition>> actions,
            List<ConvaiActionObjectDefinition> objects = null,
            List<ConvaiActionCharacterDefinition> characters = null,
            ConvaiActionBatchPolicy batchPolicy = ConvaiActionBatchPolicy.Queue,
            ConvaiActionBatchFailurePolicy failurePolicy = ConvaiActionBatchFailurePolicy.StopBatch,
            Action<RecordingActionExecutor> configureExecutor = null)
        {
            GameObject gameObject = CreateCharacterGameObject(characterId, characterName, out ConvaiCharacter character);
            ConvaiActionConfigSource source = gameObject.AddComponent<ConvaiActionConfigSource>();
            RecordingActionExecutor executor = gameObject.AddComponent<RecordingActionExecutor>();
            configureExecutor?.Invoke(executor);

            source.ReplaceDefinitions(actions(executor, gameObject));
            if (objects != null)
                source.ReplaceObjects(objects);
            if (characters != null)
                source.ReplaceCharacters(characters);

            ConvaiActionDispatcher dispatcher = gameObject.AddComponent<ConvaiActionDispatcher>();
            SetPrivateField(dispatcher, "_batchPolicy", batchPolicy);
            SetPrivateField(dispatcher, "_failurePolicy", failurePolicy);
            return (gameObject, character, dispatcher, executor);
        }

        private static List<ConvaiActionDefinition> TargetlessActions(
            RecordingActionExecutor executor,
            IReadOnlyList<string> actionNames,
            float timeoutSeconds = 0f)
        {
            var definitions = new List<ConvaiActionDefinition>();
            foreach (string name in actionNames)
                definitions.Add(new ConvaiActionDefinition
                {
                    ActionName = name,
                    TargetRequirement = ConvaiActionTargetRequirement.None,
                    Executor = executor,
                    TimeoutSeconds = timeoutSeconds
                });
            return definitions;
        }

        private static List<ConvaiActionObjectDefinition> ObjectsNamed(params string[] names) =>
            ObjectsNamed((IReadOnlyList<string>)names);

        private static List<ConvaiActionObjectDefinition> ObjectsNamed(IReadOnlyList<string> names)
        {
            var objects = new List<ConvaiActionObjectDefinition>();
            foreach (string name in names)
                objects.Add(new ConvaiActionObjectDefinition { Name = name });
            return objects;
        }

        private static List<ConvaiActionCharacterDefinition> CharactersNamed(params string[] names) =>
            CharactersNamed((IReadOnlyList<string>)names);

        private static List<ConvaiActionCharacterDefinition> CharactersNamed(IReadOnlyList<string> names)
        {
            var characters = new List<ConvaiActionCharacterDefinition>();
            foreach (string name in names)
                characters.Add(new ConvaiActionCharacterDefinition { Name = name });
            return characters;
        }

        private static (GameObject GameObject, ConvaiCharacter Character, ConvaiActionDispatcher Dispatcher,
            RecordingActionExecutor Executor) CreateDispatcherFixture(
            ConvaiActionBatchPolicy batchPolicy,
            ConvaiActionBatchFailurePolicy failurePolicy = ConvaiActionBatchFailurePolicy.StopBatch) =>
            BuildDispatcherFixture(
                "char-dispatcher", "Dispatcher Test",
                (executor, _) => TargetlessActions(executor, new[] { "Move To", "Pick Up", "Drop" }),
                objects: ObjectsNamed("cube"),
                batchPolicy: batchPolicy,
                failurePolicy: failurePolicy);

        private static (GameObject GameObject, ConvaiActionDispatcher Dispatcher)
            CreateDispatcherFixtureWithObjects(
            IReadOnlyList<string> actionNames,
            IReadOnlyList<string> objectNames = null)
        {
            var fixture = BuildDispatcherFixture(
                "char-resolver", "Resolver Test",
                (executor, _) => TargetlessActions(executor, actionNames),
                objects: objectNames == null ? new List<ConvaiActionObjectDefinition>() : ObjectsNamed(objectNames));
            return (fixture.GameObject, fixture.Dispatcher);
        }

        private static (GameObject GameObject, ConvaiActionDispatcher Dispatcher)
            CreateDispatcherFixtureWithCharacters(
            IReadOnlyList<string> actionNames,
            IReadOnlyList<string> characterNames)
        {
            var fixture = BuildDispatcherFixture(
                "char-char-resolver", "CharResolver Test",
                (executor, _) => TargetlessActions(executor, actionNames),
                characters: CharactersNamed(characterNames));
            return (fixture.GameObject, fixture.Dispatcher);
        }

        private static (GameObject GameObject, ConvaiActionDispatcher Dispatcher)
            CreateDispatcherFixtureWithNullExecutor(string actionName)
        {
            var fixture = BuildDispatcherFixture(
                "char-null-exec", "NullExec Test",
                (_, _) => new List<ConvaiActionDefinition> { new() { ActionName = actionName, Executor = null } });
            return (fixture.GameObject, fixture.Dispatcher);
        }

        private static (GameObject GameObject, ConvaiActionDispatcher Dispatcher)
            CreateDispatcherFixtureWithNonExecutorMonoBehaviour(string actionName)
        {
            var fixture = BuildDispatcherFixture(
                "char-bad-exec", "BadExec Test",
                (_, gameObject) => new List<ConvaiActionDefinition>
                {
                    new() { ActionName = actionName, Executor = gameObject.AddComponent<NonExecutorMonoBehaviour>() }
                });
            return (fixture.GameObject, fixture.Dispatcher);
        }

        private static (GameObject GameObject, ConvaiActionDispatcher Dispatcher)
            CreateDispatcherFixtureWithRequirement(
            ConvaiActionTargetRequirement requirement,
            bool includeObjectInConfig)
        {
            var fixture = BuildDispatcherFixture(
                "char-req", "Requirement Test",
                (executor, _) => new List<ConvaiActionDefinition>
                {
                    new() { ActionName = "Move To", TargetRequirement = requirement, Executor = executor },
                    new() { ActionName = "Dance", TargetRequirement = requirement, Executor = executor }
                },
                objects: includeObjectInConfig ? ObjectsNamed("cube") : null,
                failurePolicy: ConvaiActionBatchFailurePolicy.ContinueBatch);
            return (fixture.GameObject, fixture.Dispatcher);
        }

        private static (GameObject GameObject, ConvaiActionDispatcher Dispatcher)
            CreateDispatcherFixtureWithMixedTargets(
            ConvaiActionTargetRequirement requirement,
            string targetName,
            bool addObject,
            bool addCharacter)
        {
            var fixture = BuildDispatcherFixture(
                "char-mixed", "MixedTarget Test",
                (executor, _) => new List<ConvaiActionDefinition>
                {
                    new() { ActionName = "Move To", TargetRequirement = requirement, Executor = executor }
                },
                objects: addObject ? ObjectsNamed(targetName) : null,
                characters: addCharacter ? CharactersNamed(targetName) : null,
                failurePolicy: ConvaiActionBatchFailurePolicy.ContinueBatch);
            return (fixture.GameObject, fixture.Dispatcher);
        }

        private static (GameObject GameObject, ConvaiActionDispatcher Dispatcher, RecordingActionExecutor Executor)
            CreateDispatcherFixtureWithFailure(
            ConvaiActionBatchPolicy batchPolicy,
            ConvaiActionBatchFailurePolicy failurePolicy)
        {
            var fixture = BuildDispatcherFixture(
                "char-fail", "Failure Test",
                (executor, _) => TargetlessActions(executor, new[] { "Move To", "Pick Up" }),
                batchPolicy: batchPolicy,
                failurePolicy: failurePolicy,
                configureExecutor: executor => executor.ResultToReturn = ConvaiActionExecutionResult.Failed("test failure"));
            return (fixture.GameObject, fixture.Dispatcher, fixture.Executor);
        }

        private static (GameObject GameObject, ConvaiActionDispatcher Dispatcher, RecordingActionExecutor Executor)
            CreateDispatcherFixtureWithTimeout(
            float timeoutSeconds,
            int executorDelayMs,
            ConvaiActionBatchFailurePolicy failurePolicy = ConvaiActionBatchFailurePolicy.StopBatch)
        {
            var fixture = BuildDispatcherFixture(
                "char-timeout", "Timeout Test",
                (executor, _) => TargetlessActions(executor, new[] { "Move To", "Pick Up" }, timeoutSeconds),
                failurePolicy: failurePolicy,
                configureExecutor: executor => executor.DelayMs = executorDelayMs);
            return (fixture.GameObject, fixture.Dispatcher, fixture.Executor);
        }

        private static RoomConnectionRuntimeAdapter CreateRuntimeAdapter(
            ConvaiCharacter character,
            CapturingRoomController controller,
            RoomSessionConnectOptions invocationOptions,
            ISessionPersistence sessionPersistence = null)
        {
            RoomDisconnectRuntimeAdapter disconnectAdapter = new(
                () => null,
                () => controller,
                (_, _) => { },
                (_, _) => { },
                () => { });

            return new RoomConnectionRuntimeAdapter(
                () => SessionState.Disconnected,
                () => false,
                () => true,
                () => 1000,
                () => true,
                () => character,
                () => ConnectionContext.Empty,
                _ => { },
                () => ReconnectPolicy.Default,
                _ => { },
                _ => { },
                () => controller,
                () => ConvaiConnectionType.Audio,
                () => "https://core.convai.com/connect",
                TurnTakingOptions.CreateHandsFreeDefault,
                UserVadSettings.CreateDefault,
                () => null,
                () => null,
                () => invocationOptions,
                (_, _) => { },
                _ => { },
                () => sessionPersistence ?? new InMemorySessionPersistence(),
                disconnectAdapter,
                (_, _) => { },
                (_, _, _, _) => { },
                _ => { });
        }

        private static GameObject CreateCharacterGameObject(string characterId, string characterName,
            out ConvaiCharacter character)
        {
            GameObject gameObject = new($"ActionSystemTests_{characterName}");
            character = gameObject.AddComponent<ConvaiCharacter>();
            SetPrivateField(character, "_characterId", characterId);
            SetPrivateField(character, "_characterName", characterName);
            return gameObject;
        }

        private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs = 1000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!predicate())
            {
                if (DateTime.UtcNow >= deadline)
                    throw new AssertionException("Timed out waiting for condition.");

                await Task.Delay(10);
            }
        }

        /// <summary>
        ///     Reflection escape hatch for serialized inspector knobs only (dispatcher policies,
        ///     speech-gate timeout, character ids). Authored action/object/character lists go
        ///     through the internal Replace* seams on <see cref="ConvaiActionConfigSource" /> instead.
        /// </summary>
        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(instance.GetType().FullName, fieldName);

            field.SetValue(instance, value);
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(instance.GetType().FullName, fieldName);

            return (T)field.GetValue(instance);
        }

        private static void InvokePrivateMethod(object instance, string methodName)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new MissingMethodException(instance.GetType().FullName, methodName);

            method.Invoke(instance, null);
        }

        private static void RaiseCharacterSpeechStarted(ConvaiCharacter character)
        {
            FieldInfo field = typeof(ConvaiCharacter).GetField(
                "OnSpeechStarted",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "ConvaiCharacter.OnSpeechStarted backing field should exist.");
            (field.GetValue(character) as Action)?.Invoke();
        }

        private static IReadOnlyList<ConvaiActionDefinition> GetRuntimeActionDefinitions(ConvaiCharacter character)
        {
            MethodInfo method = typeof(ConvaiCharacter).GetMethod(
                "GetRuntimeActionDefinitions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "GetRuntimeActionDefinitions should exist.");
            return method.Invoke(character, null) as IReadOnlyList<ConvaiActionDefinition>;
        }

        // ── Inner test types ──────────────────────────────────────────────────────────────

        private sealed class RecordingActionExecutor : MonoBehaviour, IConvaiActionExecutor
        {
            public readonly List<string> ExecutedActions = new();
            public int DelayMs { get; set; }
            public bool CancellationObserved { get; private set; }
            public ConvaiActionExecutionResult ResultToReturn { get; set; } = ConvaiActionExecutionResult.Succeeded();

            public async Task<ConvaiActionExecutionResult> ExecuteAsync(
                ConvaiActionInvocation invocation,
                CancellationToken cancellationToken)
            {
                ExecutedActions.Add(invocation.Command.ToString());

                try
                {
                    if (DelayMs > 0)
                        await Task.Delay(DelayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    CancellationObserved = true;
                    throw;
                }

                return ResultToReturn;
            }
        }

        private sealed class PutParameters
        {
            [ConvaiActionParameter("item")]
            public string Item { get; set; }

            [ConvaiActionParameter("container")]
            public ConvaiResolvedActionTarget Container { get; set; }
        }

        private sealed class RecordingTypedPutExecutor : ConvaiActionExecutor<PutParameters>
        {
            public PutParameters LastParameters { get; private set; }

            protected override Task<ConvaiActionExecutionResult> ExecuteAsync(
                ConvaiActionInvocation invocation,
                PutParameters parameters,
                CancellationToken cancellationToken)
            {
                LastParameters = parameters;
                return Task.FromResult(ConvaiActionExecutionResult.Succeeded());
            }
        }

        private sealed class NonExecutorMonoBehaviour : MonoBehaviour
        {
        }

        private sealed class InMemorySessionPersistence : ISessionPersistence
        {
            private readonly Dictionary<string, string> _sessions = new();

            public string LoadSession(string characterId) =>
                _sessions.TryGetValue(characterId ?? string.Empty, out string sessionId) ? sessionId : null;

            public void SaveSession(string characterId, string sessionId) =>
                _sessions[characterId ?? string.Empty] = sessionId;

            public void ClearSession(string characterId) => _sessions.Remove(characterId ?? string.Empty);
            public void ClearAllSessions() => _sessions.Clear();
            public bool HasSession(string characterId) => _sessions.ContainsKey(characterId ?? string.Empty);
        }

        private sealed class CapturingRoomController : IConvaiRoomController
        {
            public RoomJoinOptions LastJoinOptions { get; private set; }
            public string LastStoredSessionId { get; private set; }
            public bool HasRoomDetails => true;
            public bool IsConnectedToRoom => true;
            public bool IsMicMuted => false;
            public string SessionID => "session-id";
            public string CharacterSessionID => "character-session-id";
            public string RoomName => "room-name";
            public string RoomURL => "wss://room-url";
            public string Token => "token";
            public string ResolvedSpeakerId => string.Empty;
            public string RequestTraceId => string.Empty;
            public string ResolvedEndUserId => string.Empty;
            public IReadOnlyDictionary<string, object> ResolvedEndUserMetadata => null;
            public RTVIHandler RTVIHandler => null;
            public IRoomFacade CurrentRoom => null;

            public event Action OnRoomConnectionSuccessful
            {
                add { }
                remove { }
            }

            public event Action OnRoomConnectionFailed
            {
                add { }
                remove { }
            }

            public event Action<bool> OnMicMuteChanged
            {
                add { }
                remove { }
            }

            public event Action OnRoomReconnecting
            {
                add { }
                remove { }
            }

            public event Action OnRoomReconnected
            {
                add { }
                remove { }
            }

            public event Action OnUnexpectedRoomDisconnected
            {
                add { }
                remove { }
            }

            public event Action<IRemoteAudioTrack, string, string> OnRemoteAudioTrackSubscribed
            {
                add { }
                remove { }
            }

            public event Action<string, string> OnRemoteAudioTrackUnsubscribed
            {
                add { }
                remove { }
            }

            public Task<RoomConnectionAttemptResult> InitializeAsync(
                string connectionType,
                string coreServerUrl,
                string characterId,
                string storedSessionId,
                bool enableSessionResume,
                string dynamicInfoText,
                bool keepDynamicInfoInContext) =>
                InitializeAsync(
                    connectionType,
                    coreServerUrl,
                    characterId,
                    storedSessionId,
                    enableSessionResume,
                    dynamicInfoText,
                    keepDynamicInfoInContext,
                    null,
                    CancellationToken.None);

            public Task<RoomConnectionAttemptResult> InitializeAsync(
                string connectionType,
                string coreServerUrl,
                string characterId,
                string storedSessionId,
                bool enableSessionResume,
                string dynamicInfoText,
                bool keepDynamicInfoInContext,
                RoomJoinOptions joinOptions,
                CancellationToken cancellationToken = default)
            {
                LastStoredSessionId = storedSessionId;
                LastJoinOptions = joinOptions;
                return Task.FromResult(RoomConnectionAttemptResult.Success());
            }

            public void DisconnectFromRoom()
            {
            }

            public Task DisconnectFromRoomAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void SetMicMuted(bool mute) { }
            public void ToggleMicMute() { }
            public bool SetCharacterAudioMuted(string characterId, bool mute) => true;
            public bool MuteCharacter(string characterId) => true;
            public bool UnmuteCharacter(string characterId) => true;
            public bool IsCharacterAudioMuted(string characterId) => false;
            public void SetAudioSubscriptionPolicy(Func<string, bool> policy) { }
            public void ApplyRemoteAudioPreference(string characterId, bool enabled) { }
            public void Dispose() { }
        }
    }
}
