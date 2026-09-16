using System;

namespace Convai.Runtime.Actions
{
    /// <summary>
    ///     Terminal status of a single executed action step.
    /// </summary>
    public enum ConvaiActionExecutionStatus
    {
        /// <summary>The executor completed the action.</summary>
        Succeeded = 0,

        /// <summary>The executor failed or threw.</summary>
        Failed = 1,

        /// <summary>The step was canceled (batch replaced, dispatcher disabled, or destroy).</summary>
        Canceled = 2,

        /// <summary>The step exceeded its definition timeout.</summary>
        TimedOut = 3,

        /// <summary>The executor could not handle the invocation (missing rig, peer, or capability).</summary>
        Unhandled = 4
    }

    /// <summary>
    ///     Result an <see cref="IConvaiActionExecutor" /> returns for one invocation.
    /// </summary>
    public readonly struct ConvaiActionExecutionResult
    {
        /// <summary>Terminal status of the step.</summary>
        public ConvaiActionExecutionStatus Status { get; }

        /// <summary>Optional human-readable detail for the status.</summary>
        public string Message { get; }

        /// <summary>Exception captured when the executor threw.</summary>
        public Exception Exception { get; }

        private ConvaiActionExecutionResult(
            ConvaiActionExecutionStatus status,
            string message = null,
            Exception exception = null)
        {
            Status = status;
            Message = message;
            Exception = exception;
        }

        /// <summary>Creates a success result.</summary>
        public static ConvaiActionExecutionResult Succeeded(string message = null) =>
            new(ConvaiActionExecutionStatus.Succeeded, message);

        /// <summary>Creates a failure result.</summary>
        public static ConvaiActionExecutionResult Failed(string message = null, Exception exception = null) =>
            new(ConvaiActionExecutionStatus.Failed, message, exception);

        /// <summary>Creates a canceled result.</summary>
        public static ConvaiActionExecutionResult Canceled() =>
            new(ConvaiActionExecutionStatus.Canceled);

        /// <summary>Creates a timed-out result.</summary>
        public static ConvaiActionExecutionResult TimedOut() =>
            new(ConvaiActionExecutionStatus.TimedOut);

        /// <summary>Creates an unhandled result (executor cannot service the invocation).</summary>
        public static ConvaiActionExecutionResult Unhandled(string message = null) =>
            new(ConvaiActionExecutionStatus.Unhandled, message);

        /// <inheritdoc />
        public override string ToString() => Exception != null
            ? $"{Status}: {Message ?? Exception.Message}"
            : string.IsNullOrEmpty(Message) ? Status.ToString() : $"{Status}: {Message}";
    }

    /// <summary>
    ///     Completed-step report emitted by <see cref="ConvaiActionDispatcher.OnStepCompleted" />.
    /// </summary>
    [Serializable]
    public sealed class ConvaiActionStepReport
    {
        /// <summary>The invocation the report describes.</summary>
        public ConvaiActionInvocation Invocation { get; }

        /// <summary>Raw executor result for the step.</summary>
        public ConvaiActionExecutionResult Result { get; }

        /// <summary>Whether this step aborted the remaining batch.</summary>
        public bool BatchAborted { get; }

        /// <summary>Success detail, or the failure message for non-success statuses.</summary>
        public string Message { get; }

        /// <summary>Failure detail including the batch consequence; empty on success.</summary>
        public string FailureMessage { get; }

        internal ConvaiActionStepReport(
            ConvaiActionInvocation invocation,
            ConvaiActionExecutionResult result,
            bool batchAborted,
            string failureMessage)
        {
            Invocation = invocation;
            Result = result;
            BatchAborted = batchAborted;
            FailureMessage = failureMessage ?? string.Empty;
            Message = result.Status == ConvaiActionExecutionStatus.Succeeded
                ? result.Message ?? string.Empty
                : FailureMessage;
        }
    }
}
