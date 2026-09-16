using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Convai.Domain.Logging;
using Convai.Runtime;
using Convai.Runtime.Components;
using Convai.Runtime.Logging;
using Convai.Shared.Actions;
using Convai.Shared.Types;
using UnityEngine;
using UnityEngine.Events;

namespace Convai.Runtime.Actions
{
    /// <summary>How the dispatcher treats a new backend batch while another batch is still executing.</summary>
    public enum ConvaiActionBatchPolicy
    {
        /// <summary>Run the new batch after the current one finishes (default).</summary>
        Queue = 0,

        /// <summary>Cancel the current batch and pending queue, then run the new batch.</summary>
        ReplaceCurrent = 1,

        /// <summary>Ignore the new batch entirely while anything is executing or queued.</summary>
        DropIncoming = 2
    }

    /// <summary>What a non-succeeded step does to the rest of its batch.</summary>
    public enum ConvaiActionBatchFailurePolicy
    {
        /// <summary>Abort the remaining steps of the batch (default).</summary>
        StopBatch = 0,

        /// <summary>Report the step and continue with the next one.</summary>
        ContinueBatch = 1
    }

    /// <summary>Serializable UnityEvent carrying the step's <see cref="ConvaiActionInvocation" />.</summary>
    [Serializable]
    public sealed class ConvaiActionInvocationUnityEvent : UnityEvent<ConvaiActionInvocation>
    {
    }

    /// <summary>Serializable UnityEvent carrying the completed step's <see cref="ConvaiActionStepReport" />.</summary>
    [Serializable]
    public sealed class ConvaiActionStepReportUnityEvent : UnityEvent<ConvaiActionStepReport>
    {
    }

    [AddComponentMenu("Convai/Convai Action Dispatcher")]
    [DisallowMultipleComponent]
    // ExecuteAlways keeps Awake/OnEnable active outside play mode so EditMode tests and editor
    // tooling can inject batches; cross-thread marshaling is play-mode-only (see HandleActionsReceived).
    [ExecuteAlways]
    [RequireComponent(typeof(ConvaiCharacter))]
    public sealed class ConvaiActionDispatcher : MonoBehaviour
    {
        [Header("Dispatch")]
        [SerializeField]
        [Tooltip("How new backend action batches behave while another batch is still executing.")]
        private ConvaiActionBatchPolicy _batchPolicy = ConvaiActionBatchPolicy.Queue;

        [SerializeField]
        [Tooltip("Whether a step failure aborts the remaining batch or allows it to continue.")]
        private ConvaiActionBatchFailurePolicy _failurePolicy = ConvaiActionBatchFailurePolicy.StopBatch;

        [SerializeField]
        [Tooltip("Maximum seconds a first action waits for character speech before running anyway.")]
        private float _speechGateTimeoutSeconds = 2f;

        [Header("Events")]
        [SerializeField] private UnityEvent _onBatchStarted = new();
        [SerializeField] private ConvaiActionInvocationUnityEvent _onStepStarted = new();
        [SerializeField] private ConvaiActionInvocationUnityEvent _onStepSucceeded = new();
        [SerializeField] private ConvaiActionInvocationUnityEvent _onStepFailed = new();
        [SerializeField] private ConvaiActionInvocationUnityEvent _onStepUnhandled = new();
        [SerializeField] private ConvaiActionStepReportUnityEvent _onStepCompleted = new();
        [SerializeField] private UnityEvent _onBatchCompleted = new();
        [SerializeField] private UnityEvent _onBatchAborted = new();

        private readonly object _queueLock = new();
        private readonly Queue<IReadOnlyList<ConvaiActionCommand>> _pendingBatches = new();
        private CancellationTokenSource _processingCts;
        private ConvaiCharacter _character;
        private bool _isProcessing;
        private int _batchCounter;
        private int _mainThreadId;

        /// <summary>Authored policy for batches arriving while another batch is executing.</summary>
        public ConvaiActionBatchPolicy BatchPolicy => _batchPolicy;

        /// <summary>Authored default for what a non-succeeded step does to the rest of its batch.</summary>
        public ConvaiActionBatchFailurePolicy FailurePolicy => _failurePolicy;

        /// <summary>Raised when a batch begins executing, before its first step.</summary>
        public UnityEvent OnBatchStarted => _onBatchStarted;

        /// <summary>Raised before each step executes (after the first step's speech gate).</summary>
        public ConvaiActionInvocationUnityEvent OnStepStarted => _onStepStarted;

        /// <summary>Raised when a step's executor reports success.</summary>
        public ConvaiActionInvocationUnityEvent OnStepSucceeded => _onStepSucceeded;

        /// <summary>Raised when a step fails: no definition, no executor, unmet target requirement, error, or timeout.</summary>
        public ConvaiActionInvocationUnityEvent OnStepFailed => _onStepFailed;

        /// <summary>Raised when the executor declines the step (<see cref="ConvaiActionExecutionStatus.Unhandled" />).</summary>
        public ConvaiActionInvocationUnityEvent OnStepUnhandled => _onStepUnhandled;

        /// <summary>Raised after every step, success or not, with the full <see cref="ConvaiActionStepReport" />.</summary>
        public ConvaiActionStepReportUnityEvent OnStepCompleted => _onStepCompleted;

        /// <summary>Raised when a batch runs all its steps without aborting.</summary>
        public UnityEvent OnBatchCompleted => _onBatchCompleted;

        /// <summary>Raised when a failing step aborts the remainder of its batch.</summary>
        public UnityEvent OnBatchAborted => _onBatchAborted;

        /// <summary>
        ///     Injects a batch exactly as if the backend had sent it — same policies, cloning,
        ///     enrichment, and events. This is the entry point for local/manual triggering
        ///     (used by the Action Debug Window).
        /// </summary>
        public void EnqueueActions(IReadOnlyList<ConvaiActionCommand> actions) => HandleActionsReceived(actions);

        private void Awake()
        {
            _character = GetComponent<ConvaiCharacter>();
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        private void OnEnable()
        {
            if (_character == null)
            {
                enabled = false;
                return;
            }

            _character.OnActionsReceived += HandleActionsReceived;
        }

        private void OnDisable()
        {
            if (_character != null)
                _character.OnActionsReceived -= HandleActionsReceived;

            CancelAllWork();
        }

        private void OnDestroy() => CancelAllWork();

        private void HandleActionsReceived(IReadOnlyList<ConvaiActionCommand> actions)
        {
            if (actions == null || actions.Count == 0)
                return;

            // Snapshot: EnqueueActions is public API and the caller may keep mutating its list.
            IReadOnlyList<ConvaiActionCommand> batch = ConvaiActionCommand.CloneBatch(actions);
            if (UnityEngine.Application.isPlaying && !IsOnDispatcherThread())
            {
                UnityScheduler.Post(() => EnqueueBatchOnDispatcherThread(batch));
                return;
            }

            EnqueueBatchOnDispatcherThread(batch);
        }

        private void EnqueueBatchOnDispatcherThread(IReadOnlyList<ConvaiActionCommand> batch)
        {
            if (!EnsureCharacter())
                return;

            if (!isActiveAndEnabled)
                return;

            bool shouldStartProcessing = false;

            lock (_queueLock)
            {
                switch (_batchPolicy)
                {
                    case ConvaiActionBatchPolicy.DropIncoming when _isProcessing || _pendingBatches.Count > 0:
                        return;
                    case ConvaiActionBatchPolicy.ReplaceCurrent:
                        CancelAllWorkLocked();
                        break;
                }

                _pendingBatches.Enqueue(batch);
                if (!_isProcessing)
                {
                    _isProcessing = true;
                    shouldStartProcessing = true;
                }
            }

            if (shouldStartProcessing)
                _ = ProcessQueueAsync();
        }

        private async Task ProcessQueueAsync()
        {
            // Each drain owns one CTS; a ReplaceCurrent cancel ends the drain, and EndDrain decides
            // whether a fresh drain (with a fresh CTS) should pick up batches enqueued meanwhile.
            bool continueDraining = true;
            while (continueDraining && TryBeginDrain(out CancellationTokenSource processingCts))
            {
                try
                {
                    while (TryDequeueBatch(out IReadOnlyList<ConvaiActionCommand> batch, out int batchIndex))
                    {
                        await ExecuteBatchAsync(batch, processingCts.Token, batchIndex);
                        if (processingCts.IsCancellationRequested)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    ConvaiLogger.Error($"Batch processing failed on '{name}': {ex}",
                        LogCategory.Character);
                }
                finally
                {
                    continueDraining = EndDrain(processingCts);
                }
            }
        }

        private bool TryBeginDrain(out CancellationTokenSource processingCts)
        {
            lock (_queueLock)
            {
                if (_pendingBatches.Count == 0)
                {
                    _isProcessing = false;
                    processingCts = null;
                    return false;
                }

                _processingCts = new CancellationTokenSource();
                processingCts = _processingCts;
                return true;
            }
        }

        private bool TryDequeueBatch(out IReadOnlyList<ConvaiActionCommand> batch, out int batchIndex)
        {
            lock (_queueLock)
            {
                if (_pendingBatches.Count == 0)
                {
                    batch = null;
                    batchIndex = 0;
                    return false;
                }

                batch = _pendingBatches.Dequeue();
                batchIndex = _batchCounter++;
                return true;
            }
        }

        private bool EndDrain(CancellationTokenSource processingCts)
        {
            bool shouldContinueProcessing;
            lock (_queueLock)
            {
                if (ReferenceEquals(_processingCts, processingCts))
                    _processingCts = null;

                shouldContinueProcessing = _pendingBatches.Count > 0 && isActiveAndEnabled;
                if (!shouldContinueProcessing)
                    _isProcessing = false;
            }

            processingCts.Dispose();
            return shouldContinueProcessing;
        }

        private async Task ExecuteBatchAsync(
            IReadOnlyList<ConvaiActionCommand> actions,
            CancellationToken batchCt,
            int batchIndex)
        {
            _onBatchStarted?.Invoke();

            ConvaiActionConfig actionConfig = _character.GetRuntimeActionConfig();
            IReadOnlyList<ConvaiActionDefinition> definitions = _character.GetRuntimeActionDefinitionCatalog();
            Dictionary<string, ConvaiActionDefinition> lookup = ConvaiActionDefinition.BuildLookup(definitions);

            bool batchAborted = false;

            for (int stepIndex = 0; stepIndex < actions.Count; stepIndex++)
            {
                batchCt.ThrowIfCancellationRequested();

                ConvaiActionInvocation invocation = ResolveStep(
                    actions[stepIndex], actionConfig, definitions, lookup, batchIndex, stepIndex);

                if (stepIndex == 0)
                    await WaitForSpeechGateAsync(invocation.Command, invocation.Definition, batchCt);

                _onStepStarted?.Invoke(invocation);

                ConvaiActionExecutionResult result = await RunStepAsync(invocation, batchCt);

                bool stepWillAbort = result.Status != ConvaiActionExecutionStatus.Succeeded &&
                                     ShouldAbortNonSuccess(invocation.Definition);
                string failureMessage = result.Status == ConvaiActionExecutionStatus.Succeeded
                    ? string.Empty
                    : BuildFailureMessage(result, stepWillAbort);
                _onStepCompleted?.Invoke(new ConvaiActionStepReport(invocation, result, stepWillAbort, failureMessage));

                if (stepWillAbort)
                {
                    batchAborted = true;
                    break;
                }
            }

            if (batchAborted)
                _onBatchAborted?.Invoke();
            else
                _onBatchCompleted?.Invoke();
        }

        /// <summary>
        ///     Enriches the command (once), matches its definition, and resolves its target
        ///     into the immutable invocation handed to executors and events.
        /// </summary>
        private ConvaiActionInvocation ResolveStep(
            ConvaiActionCommand command,
            ConvaiActionConfig actionConfig,
            IReadOnlyList<ConvaiActionDefinition> definitions,
            Dictionary<string, ConvaiActionDefinition> lookup,
            int batchIndex,
            int stepIndex)
        {
            lookup.TryGetValue(command.Name ?? string.Empty, out ConvaiActionDefinition definition);
            if (definition == null && command is { Enriched: false })
            {
                command = ConvaiActionResponseParser.Enrich(command, actionConfig, definitions);
                lookup.TryGetValue(command.Name ?? string.Empty, out definition);
            }

            ConvaiResolvedActionTarget resolvedTarget = ResolveTarget(command, actionConfig, definition);
            return new ConvaiActionInvocation(command, definition, resolvedTarget, _character, batchIndex, stepIndex);
        }

        /// <summary>
        ///     Checks step preconditions (definition, executor, target requirement), executes the
        ///     step, and fires exactly one of the succeeded/unhandled/failed step events.
        /// </summary>
        private async Task<ConvaiActionExecutionResult> RunStepAsync(
            ConvaiActionInvocation invocation,
            CancellationToken batchCt)
        {
            ConvaiActionDefinition definition = invocation.Definition;

            if (definition == null)
            {
                _onStepFailed?.Invoke(invocation);
                return ConvaiActionExecutionResult.Failed(
                    $"No local action definition found for action '{invocation.Command?.Name ?? string.Empty}'.");
            }

            if (definition.Executor is not IConvaiActionExecutor executor)
            {
                _onStepFailed?.Invoke(invocation);
                return ConvaiActionExecutionResult.Failed(
                    $"Action '{definition.ActionName}' is missing a valid executor.");
            }

            if (!ValidateTargetRequirement(definition.TargetRequirement, invocation.ResolvedTarget))
            {
                _onStepFailed?.Invoke(invocation);
                return ConvaiActionExecutionResult.Failed(
                    BuildTargetRequirementFailureMessage(invocation.Command, definition, invocation.ResolvedTarget));
            }

            ConvaiActionExecutionResult result = await ExecuteStepAsync(executor, invocation, definition, batchCt);

            switch (result.Status)
            {
                case ConvaiActionExecutionStatus.Succeeded:
                    _onStepSucceeded?.Invoke(invocation);
                    break;
                case ConvaiActionExecutionStatus.Unhandled:
                    _onStepUnhandled?.Invoke(invocation);
                    break;
                default:
                    _onStepFailed?.Invoke(invocation);
                    break;
            }

            return result;
        }

        private static async Task<ConvaiActionExecutionResult> ExecuteStepAsync(
            IConvaiActionExecutor executor,
            ConvaiActionInvocation invocation,
            ConvaiActionDefinition definition,
            CancellationToken batchCt)
        {
            bool hasTimeout = definition.TimeoutSeconds > 0f;
            CancellationTokenSource stepCts = null;

            try
            {
                CancellationToken stepCt = batchCt;

                if (hasTimeout)
                {
                    stepCts = CancellationTokenSource.CreateLinkedTokenSource(batchCt);
                    stepCts.CancelAfter(TimeSpan.FromSeconds(definition.TimeoutSeconds));
                    stepCt = stepCts.Token;
                }

                return await executor.ExecuteAsync(invocation, stepCt);
            }
            catch (OperationCanceledException) when (!batchCt.IsCancellationRequested)
            {
                return ConvaiActionExecutionResult.TimedOut();
            }
            catch (OperationCanceledException)
            {
                return ConvaiActionExecutionResult.Canceled();
            }
            catch (Exception ex)
            {
                return ConvaiActionExecutionResult.Failed(ex.Message, ex);
            }
            finally
            {
                stepCts?.Dispose();
            }
        }

        private async Task WaitForSpeechGateAsync(
            ConvaiActionCommand command,
            ConvaiActionDefinition definition,
            CancellationToken batchCt)
        {
            bool shouldWait = command?.WaitForBotSpeech == true || definition?.WaitForBotSpeech == true;
            if (!shouldWait || _character == null)
                return;

            var gate = new TaskCompletionSource<bool>();
            void Release() => gate.TrySetResult(true);
            void ReleaseTurn(bool _) => gate.TrySetResult(true);

            _character.OnSpeechStarted += Release;
            _character.OnSpeechStopped += Release;
            _character.OnTurnCompleted += ReleaseTurn;
            try
            {
                float timeout = Mathf.Max(0f, _speechGateTimeoutSeconds);
                Task timeoutTask = timeout <= 0f
                    ? Task.CompletedTask
                    : Task.Delay(TimeSpan.FromSeconds(timeout), batchCt);

                await Task.WhenAny(gate.Task, timeoutTask);
                batchCt.ThrowIfCancellationRequested();

                float delay = command?.WaitForBotSpeech == true
                    ? Mathf.Max(0f, command.DelayAfterBotSpeechSeconds)
                    : Mathf.Max(0f, definition?.DelayAfterBotSpeechSeconds ?? 0f);
                if (delay > 0f)
                    await Task.Delay(TimeSpan.FromSeconds(delay), batchCt);
            }
            finally
            {
                _character.OnSpeechStarted -= Release;
                _character.OnSpeechStopped -= Release;
                _character.OnTurnCompleted -= ReleaseTurn;
            }
        }

        private static bool ValidateTargetRequirement(
            ConvaiActionTargetRequirement requirement,
            ConvaiResolvedActionTarget target)
        {
            return requirement switch
            {
                ConvaiActionTargetRequirement.None => true,
                ConvaiActionTargetRequirement.Object => target?.Kind == ConvaiActionTargetKind.Object,
                ConvaiActionTargetRequirement.Character => target?.Kind == ConvaiActionTargetKind.Character,
                ConvaiActionTargetRequirement.Either =>
                    target?.Kind == ConvaiActionTargetKind.Object ||
                    target?.Kind == ConvaiActionTargetKind.Character,
                _ => false
            };
        }

        private static ConvaiResolvedActionTarget ResolveTarget(
            ConvaiActionCommand command,
            ConvaiActionConfig actionConfig,
            ConvaiActionDefinition definition)
        {
            ConvaiResolvedActionTarget resolvedTarget =
                ConvaiResolvedActionTarget.Resolve(command?.Target, actionConfig, definition?.TargetRequirement);
            if (resolvedTarget != null)
                return resolvedTarget;

            // Target-less actions still resolve an explicit backend target opportunistically
            // (above), but never promote one of their reference parameters to the step target.
            if (definition?.TargetRequirement == ConvaiActionTargetRequirement.None)
                return null;

            if (command?.Parameters == null || command.Parameters.Count == 0 || definition?.Parameters == null)
                return null;

            for (int i = 0; i < definition.Parameters.Count; i++)
            {
                ConvaiActionParameterDefinition parameter = definition.Parameters[i];
                if (parameter == null ||
                    parameter.Type is not (ConvaiActionParameterType.Auto or ConvaiActionParameterType.Reference))
                    continue;

                string parameterName = ConvaiActionParameterDefinition.Normalize(parameter.Name);
                if (string.IsNullOrEmpty(parameterName) ||
                    !command.Parameters.TryGetValue(parameterName, out ConvaiActionParameterValue value))
                    continue;

                resolvedTarget = ConvaiActionTargetReferenceResolver.Resolve(
                    value, actionConfig, definition.TargetRequirement);
                if (resolvedTarget != null)
                    return resolvedTarget;
            }

            return null;
        }

        private bool ShouldAbortNonSuccess(ConvaiActionDefinition definition)
        {
            ConvaiActionBatchFailurePolicy policy = ResolveFailurePolicy(definition);
            return policy == ConvaiActionBatchFailurePolicy.StopBatch;
        }

        private ConvaiActionBatchFailurePolicy ResolveFailurePolicy(ConvaiActionDefinition definition)
        {
            return definition?.FailurePolicyOverride switch
            {
                ConvaiActionFailurePolicyOverride.StopBatch => ConvaiActionBatchFailurePolicy.StopBatch,
                ConvaiActionFailurePolicyOverride.ContinueBatch => ConvaiActionBatchFailurePolicy.ContinueBatch,
                _ => _failurePolicy
            };
        }

        private static string BuildTargetRequirementFailureMessage(
            ConvaiActionCommand command,
            ConvaiActionDefinition definition,
            ConvaiResolvedActionTarget resolvedTarget)
        {
            string actionName = definition?.ActionName ?? command?.Name ?? string.Empty;
            string targetName = command?.Target ?? string.Empty;
            string resolvedKind = resolvedTarget?.Kind.ToString() ?? "None";
            return $"Action '{actionName}' target '{targetName}' required {definition.TargetRequirement} but resolved {resolvedKind}.";
        }

        private static string BuildFailureMessage(
            ConvaiActionExecutionResult result,
            bool batchWillAbort)
        {
            string message = result.Message;
            if (string.IsNullOrWhiteSpace(message) && result.Exception != null)
                message = result.Exception.Message;

            if (string.IsNullOrWhiteSpace(message))
                message = result.Status.ToString();

            return AppendBatchAbortSuffix(message, batchWillAbort);
        }

        private static string AppendBatchAbortSuffix(string message, bool batchWillAbort) =>
            batchWillAbort ? $"{message} Remaining batch will abort." : $"{message} Remaining batch will continue.";

        private void CancelAllWork()
        {
            lock (_queueLock)
                CancelAllWorkLocked();
        }

        private void CancelAllWorkLocked()
        {
            _processingCts?.Cancel();
            _pendingBatches.Clear();
        }

        private bool EnsureCharacter()
        {
            if (_character == null)
                _character = GetComponent<ConvaiCharacter>();

            return _character != null;
        }

        private bool IsOnDispatcherThread() => Thread.CurrentThread.ManagedThreadId == _mainThreadId;
    }
}
