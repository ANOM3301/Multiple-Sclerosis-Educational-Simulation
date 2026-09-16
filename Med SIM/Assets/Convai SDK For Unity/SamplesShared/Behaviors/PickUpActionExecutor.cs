using System;
using System.Threading;
using System.Threading.Tasks;
using Convai.Runtime.Actions;
using UnityEngine;

namespace Convai.Sample.Behaviors
{
    /// <summary>
    ///     Sample compound executor: moves to target, plays animation, waits, then reparents the object.
    /// </summary>
    [AddComponentMenu("Convai/Samples/Pick Up Action Executor")]
    public sealed class PickUpActionExecutor : MonoBehaviour, IConvaiActionExecutor
    {
        [SerializeField] private NavMeshMoveToActionExecutor _mover;
        [SerializeField] private Animator _animator;
        [SerializeField] private string _pickUpTrigger = "PickUp";
        [SerializeField] private Transform _attachPoint;
        [SerializeField] private float _animationDuration = 1f;

        public async Task<ConvaiActionExecutionResult> ExecuteAsync(
            ConvaiActionInvocation invocation,
            CancellationToken cancellationToken)
        {
            GameObject targetGo = invocation.ResolvedTarget?.GameObjectReference;
            if (targetGo == null)
                return ConvaiActionExecutionResult.Failed("No target resolved");

            HeldObjectActionState heldState = GetHeldState();
            GameObject heldObject = heldState != null ? heldState.CurrentHeldObject : null;
            if (heldObject == targetGo)
                return ConvaiActionExecutionResult.Succeeded($"Already holding {targetGo.name}.");

            if (heldObject != null)
                return ConvaiActionExecutionResult.Failed($"Already holding {heldObject.name}.");

            if (_mover == null)
                return ConvaiActionExecutionResult.Failed("Mover not assigned");

            ConvaiActionExecutionResult moveResult = await _mover.ExecuteAsync(invocation, cancellationToken);
            if (moveResult.Status != ConvaiActionExecutionStatus.Succeeded)
                return moveResult;

            if (_animator != null && !string.IsNullOrWhiteSpace(_pickUpTrigger))
                _animator.SetTrigger(_pickUpTrigger);

            if (_animationDuration > 0f)
                await Task.Delay(TimeSpan.FromSeconds(_animationDuration), cancellationToken);

            if (heldState != null)
            {
                if (heldState.AttachPoint == null && _attachPoint != null)
                    heldState.SetAttachPoint(_attachPoint);

                if (!heldState.TryAttach(targetGo, out string message))
                    return ConvaiActionExecutionResult.Failed(message);
            }
            else if (_attachPoint != null)
            {
                targetGo.transform.SetParent(_attachPoint, worldPositionStays: false);
                targetGo.transform.localPosition = Vector3.zero;
                targetGo.transform.localRotation = Quaternion.identity;
            }

            return ConvaiActionExecutionResult.Succeeded();
        }

        private HeldObjectActionState GetHeldState()
        {
            HeldObjectActionState heldState = GetComponent<HeldObjectActionState>();
            if (heldState != null && heldState.AttachPoint == null && _attachPoint != null)
                heldState.SetAttachPoint(_attachPoint);

            return heldState;
        }
    }
}
