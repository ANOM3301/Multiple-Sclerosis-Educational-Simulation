using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Convai.Runtime.Actions
{
    [AddComponentMenu("Convai/Actions/Look At Target Action Executor")]
    public sealed class LookAtTargetActionExecutor : MonoBehaviour, IConvaiActionExecutor
    {
        [SerializeField] private Transform _rotateRoot;
        [SerializeField] private float _duration = 0.5f;

        public async Task<ConvaiActionExecutionResult> ExecuteAsync(
            ConvaiActionInvocation invocation,
            CancellationToken cancellationToken)
        {
            GameObject targetGo = invocation.ResolvedTarget?.GameObjectReference;
            if (targetGo == null)
                return ConvaiActionExecutionResult.Unhandled();

            Transform root = _rotateRoot != null ? _rotateRoot : transform;
            Transform target = targetGo.transform;

            float elapsed = 0f;
            Quaternion startRotation = root.rotation;

            while (elapsed < _duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (root == null || target == null)
                    return ConvaiActionExecutionResult.Failed("Look-at root or target was destroyed.");

                Vector3 direction = target.position - root.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    float t = _duration > 0f ? elapsed / _duration : 1f;
                    root.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                }

                elapsed += Time.deltaTime;
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (root == null || target == null)
                return ConvaiActionExecutionResult.Failed("Look-at root or target was destroyed.");

            Vector3 finalDirection = target.position - root.position;
            if (finalDirection.sqrMagnitude > 0.0001f)
                root.rotation = Quaternion.LookRotation(finalDirection);

            return ConvaiActionExecutionResult.Succeeded();
        }
    }
}
