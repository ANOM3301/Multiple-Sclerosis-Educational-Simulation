using System.Collections.Generic;
using Convai.Runtime.Components;
using Convai.Shared.Actions;
using Convai.Shared.Types;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Convai.Runtime.Actions
{
    /// <summary>
    ///     Small runtime probe for validating action batches and local dispatcher behavior in-scene.
    /// </summary>
    [AddComponentMenu("Convai/Debug/Convai Action Debug Probe")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ConvaiCharacter))]
    public sealed class ConvaiActionDebugProbe : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ConvaiCharacter _character;
        [SerializeField] private ConvaiActionDispatcher _dispatcher;

        [Header("Logging")]
        [SerializeField] private bool _logToConsole = true;

        [Header("Last Observed State")]
        [SerializeField] private int _receivedBatchCount;
        [SerializeField] private int _startedStepCount;
        [SerializeField] private int _succeededStepCount;
        [SerializeField] private int _failedStepCount;
        [SerializeField] private int _unhandledStepCount;
        [SerializeField] private int _completedStepCount;
        [SerializeField] private int _abortedBatchCount;
        [SerializeField] [TextArea(2, 6)] private string _lastReceivedBatch;
        [SerializeField] [TextArea(2, 6)] private string _lastStepStarted;
        [SerializeField] [TextArea(2, 6)] private string _lastStepSucceeded;
        [SerializeField] [TextArea(2, 6)] private string _lastUnhandledStep;
        [SerializeField] [TextArea(2, 6)] private string _lastStepCompleted;
        [SerializeField] [TextArea(2, 6)] private string _lastFailureReason;

        private void Reset() => AutoResolveReferences();
        private void Awake() => AutoResolveReferences();
        private void OnValidate() => AutoResolveReferences();

        private void OnEnable()
        {
            AutoResolveReferences();

            if (_character != null)
                _character.OnActionsReceived += HandleActionsReceived;

            if (_dispatcher == null)
                return;

            _dispatcher.OnBatchStarted.AddListener(HandleBatchStarted);
            _dispatcher.OnStepStarted.AddListener(HandleStepStarted);
            _dispatcher.OnStepSucceeded.AddListener(HandleStepSucceeded);
            _dispatcher.OnStepFailed.AddListener(HandleStepFailed);
            _dispatcher.OnStepUnhandled.AddListener(HandleUnhandledStep);
            _dispatcher.OnStepCompleted.AddListener(HandleStepCompleted);
            _dispatcher.OnBatchCompleted.AddListener(HandleBatchCompleted);
            _dispatcher.OnBatchAborted.AddListener(HandleBatchAborted);
        }

        private void OnDisable()
        {
            if (_character != null)
                _character.OnActionsReceived -= HandleActionsReceived;

            if (_dispatcher == null)
                return;

            _dispatcher.OnBatchStarted.RemoveListener(HandleBatchStarted);
            _dispatcher.OnStepStarted.RemoveListener(HandleStepStarted);
            _dispatcher.OnStepSucceeded.RemoveListener(HandleStepSucceeded);
            _dispatcher.OnStepFailed.RemoveListener(HandleStepFailed);
            _dispatcher.OnStepUnhandled.RemoveListener(HandleUnhandledStep);
            _dispatcher.OnStepCompleted.RemoveListener(HandleStepCompleted);
            _dispatcher.OnBatchCompleted.RemoveListener(HandleBatchCompleted);
            _dispatcher.OnBatchAborted.RemoveListener(HandleBatchAborted);
        }

        [ContextMenu("Inject Test Batch")]
        public void InjectTestBatch()
        {
            AutoResolveReferences();
            if (_dispatcher == null)
            {
                Debug.LogWarning("[ConvaiActionDebugProbe] Cannot inject test batch: dispatcher missing.", this);
                return;
            }

            _dispatcher.EnqueueActions(new[] { new ConvaiActionCommand("Move To", ResolveTestTargetName()) });
        }

        [ContextMenu("Reset Probe State")]
        public void ResetProbeState()
        {
            _receivedBatchCount = 0;
            _startedStepCount = 0;
            _succeededStepCount = 0;
            _failedStepCount = 0;
            _unhandledStepCount = 0;
            _completedStepCount = 0;
            _abortedBatchCount = 0;
            _lastReceivedBatch = string.Empty;
            _lastStepStarted = string.Empty;
            _lastStepSucceeded = string.Empty;
            _lastUnhandledStep = string.Empty;
            _lastStepCompleted = string.Empty;
            _lastFailureReason = string.Empty;
        }

        private void HandleActionsReceived(IReadOnlyList<ConvaiActionCommand> actions)
        {
            _receivedBatchCount++;
            _lastReceivedBatch = FormatBatch(actions);

            if (_logToConsole)
                Debug.Log($"[ConvaiActionDebugProbe] Received action batch #{_receivedBatchCount}: {_lastReceivedBatch}", this);
        }

        private void HandleBatchStarted()
        {
            if (_logToConsole)
                Debug.Log("[ConvaiActionDebugProbe] Dispatcher batch started.", this);
        }

        private void HandleStepStarted(ConvaiActionInvocation invocation)
        {
            _startedStepCount++;
            _lastStepStarted = FormatInvocation(invocation);

            if (_logToConsole)
                Debug.Log($"[ConvaiActionDebugProbe] Step started #{_startedStepCount}: {_lastStepStarted}", this);
        }

        private void HandleStepSucceeded(ConvaiActionInvocation invocation)
        {
            _succeededStepCount++;
            _lastStepSucceeded = FormatInvocation(invocation);

            if (_logToConsole)
                Debug.Log($"[ConvaiActionDebugProbe] Step succeeded #{_succeededStepCount}: {_lastStepSucceeded}", this);
        }

        private void HandleStepFailed(ConvaiActionInvocation invocation)
        {
            _failedStepCount++;

            if (_logToConsole)
                Debug.LogWarning($"[ConvaiActionDebugProbe] Step failed #{_failedStepCount}: {FormatInvocation(invocation)}", this);
        }

        private void HandleUnhandledStep(ConvaiActionInvocation invocation)
        {
            _unhandledStepCount++;
            _lastUnhandledStep = FormatInvocation(invocation);

            if (_logToConsole)
                Debug.LogWarning(
                    $"[ConvaiActionDebugProbe] Step unhandled #{_unhandledStepCount}: {_lastUnhandledStep}",
                    this);
        }

        private void HandleStepCompleted(ConvaiActionStepReport report)
        {
            _completedStepCount++;
            _lastStepCompleted = FormatReport(report);
            _lastFailureReason = report?.FailureMessage ?? string.Empty;

            if (!_logToConsole)
                return;

            if (report == null || report.Result.Status == ConvaiActionExecutionStatus.Succeeded)
            {
                Debug.Log($"[ConvaiActionDebugProbe] Step completed #{_completedStepCount}: {_lastStepCompleted}", this);
                return;
            }

            Debug.LogWarning(
                $"[ConvaiActionDebugProbe] Step completed #{_completedStepCount}: {_lastStepCompleted}",
                this);
        }

        private void HandleBatchCompleted()
        {
            if (_logToConsole)
                Debug.Log("[ConvaiActionDebugProbe] Dispatcher batch completed.", this);
        }

        private void HandleBatchAborted()
        {
            _abortedBatchCount++;

            if (_logToConsole)
                Debug.LogWarning($"[ConvaiActionDebugProbe] Dispatcher batch aborted #{_abortedBatchCount}.", this);
        }

        private void AutoResolveReferences()
        {
            if (_character == null)
                _character = GetComponent<ConvaiCharacter>();

            if (_dispatcher == null)
                _dispatcher = GetComponent<ConvaiActionDispatcher>();
        }

        private string ResolveTestTargetName()
        {
            ConvaiActionConfig actionConfig = _character?.ActionConfig;
            IReadOnlyList<ConvaiActionObjectDefinition> objects = actionConfig?.Objects;
            if (objects == null || objects.Count == 0)
                return string.Empty;

            for (int i = 0; i < objects.Count; i++)
            {
                string objectName = objects[i]?.Name;
                if (!string.IsNullOrWhiteSpace(objectName))
                    return objectName.Trim();
            }

            return string.Empty;
        }

        private static string FormatBatch(IReadOnlyList<ConvaiActionCommand> actions)
        {
            if (actions == null)
                return "<null>";

            if (actions.Count == 0)
                return "[]";

            var batch = new JArray();
            for (int i = 0; i < actions.Count; i++)
            {
                ConvaiActionCommand action = actions[i];
                var actionObject = new JObject
                {
                    ["name"] = action?.Name ?? string.Empty
                };

                if (!string.IsNullOrWhiteSpace(action?.Target))
                    actionObject["target"] = action.Target;

                batch.Add(actionObject);
            }

            return batch.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string FormatInvocation(ConvaiActionInvocation invocation)
        {
            if (invocation == null)
                return "<null>";

            string targetKind = invocation.ResolvedTarget?.Kind.ToString() ?? "None";
            string targetName = invocation.ResolvedTarget?.Name ?? "<none>";
            string defName = invocation.Definition?.ActionName ?? "<unresolved>";
            return $"cmd='{invocation.Command}', def='{defName}', target={targetKind}:{targetName}";
        }

        private static string FormatReport(ConvaiActionStepReport report)
        {
            if (report == null)
                return "<null>";

            string invocation = FormatInvocation(report.Invocation);
            string abort = report.BatchAborted ? "abort" : "continue";
            string failure = string.IsNullOrWhiteSpace(report.FailureMessage)
                ? string.Empty
                : $", reason='{report.FailureMessage}'";
            return $"{invocation}, result={report.Result.Status}, batch={abort}{failure}";
        }
    }
}
