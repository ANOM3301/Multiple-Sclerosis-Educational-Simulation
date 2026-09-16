using System.Threading;
using System.Threading.Tasks;
using Convai.Runtime.Actions;
using UnityEngine;

namespace Convai.Sample.Behaviors
{
    /// <summary>
    ///     Sample typed executor that places one authored object on another authored object.
    /// </summary>
    [AddComponentMenu("Convai/Samples/Put On Action Executor")]
    public sealed class PutOnActionExecutor : ConvaiActionExecutor<PutOnActionParameters>
    {
        [SerializeField] private Vector3 _placementOffset = new(0f, 0.5f, 0f);

        protected override Task<ConvaiActionExecutionResult> ExecuteAsync(
            ConvaiActionInvocation invocation,
            PutOnActionParameters parameters,
            CancellationToken cancellationToken)
        {
            GameObject item = parameters.Item?.GameObjectReference;
            GameObject container = parameters.Container?.GameObjectReference;

            if (item == null)
                return Task.FromResult(ConvaiActionExecutionResult.Failed("Item was not resolved."));

            if (container == null)
                return Task.FromResult(ConvaiActionExecutionResult.Failed("Container was not resolved."));

            HeldObjectActionState heldState = GetComponent<HeldObjectActionState>();
            heldState?.ClearIfHeld(item);
            item.transform.position = container.transform.position + _placementOffset;
            return Task.FromResult(ConvaiActionExecutionResult.Succeeded());
        }
    }

    public sealed class PutOnActionParameters
    {
        public ConvaiResolvedActionTarget Item { get; set; }
        public ConvaiResolvedActionTarget Container { get; set; }
    }
}
