using System;
using System.Threading;
using System.Threading.Tasks;
using Convai.Runtime.Actions;
using UnityEngine;
using UnityEngine.AI;

namespace Convai.Sample.Behaviors
{
    /// <summary>
    ///     Sample executor that moves a NavMeshAgent to the resolved action target.
    /// </summary>
    [AddComponentMenu("Convai/Samples/NavMesh Move To Action Executor")]
    public sealed class NavMeshMoveToActionExecutor : MonoBehaviour, IConvaiActionExecutor
    {
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private float _stoppingDistance = 0.5f;

        public async Task<ConvaiActionExecutionResult> ExecuteAsync(
            ConvaiActionInvocation invocation,
            CancellationToken cancellationToken)
        {
            GameObject targetGo = invocation.ResolvedTarget?.GameObjectReference;
            if (targetGo == null)
                return ConvaiActionExecutionResult.Failed("No target resolved");

            NavMeshAgent agent = _agent != null ? _agent : GetComponent<NavMeshAgent>();
            if (agent == null)
                return ConvaiActionExecutionResult.Failed("NavMeshAgent not found");

            if (!agent.enabled)
                return ConvaiActionExecutionResult.Failed("NavMeshAgent is disabled");

            if (!agent.isOnNavMesh)
                return ConvaiActionExecutionResult.Failed("NavMeshAgent is not on a NavMesh");

            bool completed = false;
            try
            {
                agent.stoppingDistance = _stoppingDistance;
                agent.isStopped = false;
                if (!agent.SetDestination(targetGo.transform.position))
                    return ConvaiActionExecutionResult.Failed("NavMeshAgent could not set destination");

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (targetGo == null)
                        return ConvaiActionExecutionResult.Failed("Move target was destroyed");

                    if (!agent.enabled)
                        return ConvaiActionExecutionResult.Failed("NavMeshAgent was disabled");

                    if (!agent.isOnNavMesh)
                        return ConvaiActionExecutionResult.Failed("NavMeshAgent left the NavMesh");

                    if (!agent.pathPending)
                    {
                        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                            return ConvaiActionExecutionResult.Failed("NavMeshAgent path is invalid");

                        if (agent.remainingDistance <= _stoppingDistance &&
                            (!agent.hasPath || agent.velocity.sqrMagnitude <= 0.01f))
                        {
                            completed = true;
                            return ConvaiActionExecutionResult.Succeeded();
                        }
                    }

                    await Task.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                StopAgent(agent);
                throw;
            }
            finally
            {
                if (!completed)
                    StopAgent(agent);
            }
        }

        private void Awake()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();
        }

        private static void StopAgent(NavMeshAgent agent)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            agent.isStopped = true;
            if (agent.hasPath)
                agent.ResetPath();
        }
    }
}
