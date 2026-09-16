using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Convai.Runtime.Actions
{
    /// <summary>
    ///     Simplest possible executor: fires an inspector-authored <see cref="UnityEvent" /> and
    ///     immediately reports success. Use it to bind a backend action to existing scene logic
    ///     without writing code; write an <see cref="IConvaiActionExecutor" /> when the action
    ///     needs parameters, duration, failure reporting, or cancellation.
    /// </summary>
    [AddComponentMenu("Convai/Actions/Unity Event Action Executor")]
    public sealed class UnityEventActionExecutor : MonoBehaviour, IConvaiActionExecutor
    {
        [SerializeField]
        [Tooltip("Invoked once when the bound action dispatches.")]
        private UnityEvent _onExecute;

        /// <summary>Invokes the authored event and completes synchronously with success.</summary>
        public Task<ConvaiActionExecutionResult> ExecuteAsync(
            ConvaiActionInvocation invocation,
            CancellationToken cancellationToken)
        {
            _onExecute?.Invoke();
            return Task.FromResult(ConvaiActionExecutionResult.Succeeded());
        }
    }
}
