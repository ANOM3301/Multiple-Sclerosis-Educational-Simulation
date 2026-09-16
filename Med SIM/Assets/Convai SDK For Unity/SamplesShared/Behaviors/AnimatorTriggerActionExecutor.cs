using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Convai.Runtime.Actions;
using UnityEngine;

namespace Convai.Sample.Behaviors
{
    [Serializable]
    public sealed class AnimatorTriggerActionBinding
    {
        public string ActionName;
        public string TriggerName;
    }

    /// <summary>
    ///     Sample executor that maps backend action names to Animator triggers.
    /// </summary>
    [AddComponentMenu("Convai/Samples/Animator Trigger Action Executor")]
    public sealed class AnimatorTriggerActionExecutor : MonoBehaviour, IConvaiActionExecutor
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private List<AnimatorTriggerActionBinding> _bindings = new();

        public Task<ConvaiActionExecutionResult> ExecuteAsync(
            ConvaiActionInvocation invocation,
            CancellationToken cancellationToken)
        {
            if (_animator == null)
                return Task.FromResult(ConvaiActionExecutionResult.Failed("Animator not assigned"));

            string actionName = invocation.Definition?.ActionName ?? invocation.Command?.Name;
            if (!TryGetBinding(actionName, out AnimatorTriggerActionBinding binding))
                return Task.FromResult(ConvaiActionExecutionResult.Failed($"No binding for '{actionName}'"));

            _animator.SetTrigger(binding.TriggerName);
            return Task.FromResult(ConvaiActionExecutionResult.Succeeded());
        }

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();
        }

        private bool TryGetBinding(string verb, out AnimatorTriggerActionBinding binding)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                AnimatorTriggerActionBinding candidate = _bindings[i];
                if (candidate == null ||
                    !string.Equals(candidate.ActionName, verb, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(candidate.TriggerName))
                    continue;

                binding = candidate;
                return true;
            }

            binding = null;
            return false;
        }
    }
}
