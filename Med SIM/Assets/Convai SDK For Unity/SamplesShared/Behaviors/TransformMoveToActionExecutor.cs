using System.Threading;
using System.Threading.Tasks;
using Convai.Runtime.Actions;
using UnityEngine;

namespace Convai.Sample.Behaviors
{
    /// <summary>
    ///     Sample executor that moves a transform to the resolved action target immediately.
    /// </summary>
    [AddComponentMenu("Convai/Samples/Transform Move To Action Executor")]
    public sealed class TransformMoveToActionExecutor : MonoBehaviour, IConvaiActionExecutor
    {
        [SerializeField] private Transform _moveRoot;
        [SerializeField] private Vector3 _offset;

        public Task<ConvaiActionExecutionResult> ExecuteAsync(
            ConvaiActionInvocation invocation,
            CancellationToken cancellationToken)
        {
            GameObject targetGo = invocation.ResolvedTarget?.GameObjectReference;
            if (targetGo == null)
                return Task.FromResult(ConvaiActionExecutionResult.Failed("No target resolved"));

            Transform moveRoot = _moveRoot != null ? _moveRoot : transform;
            moveRoot.position = targetGo.transform.position + _offset;
            return Task.FromResult(ConvaiActionExecutionResult.Succeeded());
        }
    }
}
