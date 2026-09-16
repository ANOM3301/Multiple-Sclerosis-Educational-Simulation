using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Owns the lifecycle of a runtime-installed <see cref="AnimatorOverrideController" />
    ///     so the controller can swap an animator's runtime controller for the duration of a
    ///     dialogue session and restore the original on teardown without leaking memory.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The pattern is intentionally simple: capture the original
    ///         <see cref="RuntimeAnimatorController" />, install the override, and on restore
    ///         confirm the animator still references the override we own before swapping back.
    ///         If the user (or another component) replaced the controller in the meantime we
    ///         leave it alone and only destroy the override asset we created.
    ///     </para>
    /// </remarks>
    internal sealed class DialogueOverrideControllerOwnership
    {
        private Animator _animator;
        private AnimatorOverrideController _overrideController;
        private RuntimeAnimatorController _originalController;
        private bool _installed;

        public AnimatorOverrideController OverrideController => _overrideController;

        public bool IsInstalled => _installed;

        /// <summary>
        ///     Installs <paramref name="overrideController" /> on <paramref name="animator" />
        ///     and remembers the previous controller so it can be restored on teardown.
        /// </summary>
        public void Install(Animator animator, AnimatorOverrideController overrideController)
        {
            if (_installed)
                RestoreAndDestroyIfOwned();

            _animator = animator;
            _overrideController = overrideController;
            _originalController = animator != null ? animator.runtimeAnimatorController : null;

            if (animator != null)
                animator.runtimeAnimatorController = overrideController;

            _installed = true;
        }

        /// <summary>
        ///     Restores the original controller (only if the animator still references the
        ///     override we installed) and destroys the override asset. Safe to call if
        ///     <see cref="Install" /> was never called.
        /// </summary>
        public void RestoreAndDestroyIfOwned()
        {
            if (!_installed) return;

            if (_animator != null
                && _overrideController != null
                && ReferenceEquals(_animator.runtimeAnimatorController, _overrideController))
            {
                _animator.runtimeAnimatorController = _originalController;
            }

            DestroyOverrideController(_overrideController);

            _animator = null;
            _overrideController = null;
            _originalController = null;
            _installed = false;
        }

        public static void DestroyOverrideController(AnimatorOverrideController overrideController)
        {
            if (overrideController == null) return;

            if (UnityEngine.Application.isPlaying)
                Object.Destroy(overrideController);
            else
                Object.DestroyImmediate(overrideController);
        }
    }
}
