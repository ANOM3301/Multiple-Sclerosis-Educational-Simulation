using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime
{
    /// <summary>
    ///     Crossfades between two sibling animator states ("A" and "B") on the same layer,
    ///     swapping the clip in the inactive slot before every fade so the controller can play
    ///     a fresh animation without authoring an explicit state per variant.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Each slot corresponds to a state in the animator controller whose motion is
    ///         a uniquely named placeholder clip. The controller overrides that placeholder at
    ///         runtime via <see cref="AnimatorSlotOverrider" /> and then issues
    ///         <see cref="Animator.CrossFadeInFixedTime(string,float,int,float)" /> on the
    ///         freshly loaded state.
    ///     </para>
    ///     <para>
    ///         Ping-pong is the cleanest way to keep authored content minimal (two states
    ///         per layer) while preserving smooth Unity Animator crossfades. It avoids the
    ///         complexity of hand-rolling a <c>PlayableGraph</c>.
    ///     </para>
    /// </remarks>
    public sealed class AnimatorStatePingPong
    {
        private readonly Animator _animator;
        private readonly AnimatorSlotOverrider _overrider;
        private readonly int _layerIndex;
        private readonly SlotDefinition _slotA;
        private readonly SlotDefinition _slotB;

        private bool _activeIsA;
        private bool _hasPlayedOnce;

        public AnimatorStatePingPong(
            Animator animator,
            AnimatorSlotOverrider overrider,
            int layerIndex,
            SlotDefinition slotA,
            SlotDefinition slotB)
        {
            _animator = animator;
            _overrider = overrider;
            _layerIndex = layerIndex;
            _slotA = slotA;
            _slotB = slotB;
            _activeIsA = true;
        }

        /// <summary>
        ///     <c>true</c> when both slots were successfully resolved in the override
        ///     controller. When <c>false</c> the component should log a validation warning and
        ///     stop writing to this ping-pong.
        /// </summary>
        public bool IsWired =>
            _animator != null
            && _overrider != null
            && _overrider.HasSlot(_slotA.PlaceholderName)
            && _overrider.HasSlot(_slotB.PlaceholderName)
            && _layerIndex >= 0;

        /// <summary>Last clip played through this ping-pong, or <c>null</c> before the first fade.</summary>
        public AnimationClip CurrentClip { get; private set; }

        /// <summary>
        ///     Crossfades to <paramref name="clip" /> on the slot opposite the currently
        ///     active one, overriding that slot's placeholder first.
        /// </summary>
        /// <param name="clip">Clip to play. When null the call is a no-op.</param>
        /// <param name="fadeSeconds">Fixed fade duration in seconds.</param>
        /// <param name="normalizedOffset">Normalized time to enter the new state at.</param>
        public bool CrossFadeTo(AnimationClip clip, float fadeSeconds, float normalizedOffset = 0f)
        {
            if (clip == null || !IsWired) return false;

            SlotDefinition nextSlot = _activeIsA ? _slotB : _slotA;
            string placeholderName = nextSlot.PlaceholderName;

            if (!_overrider.SetOverride(placeholderName, clip))
                return false;

            _overrider.ApplyPending();

            float safeFade = Mathf.Max(0f, fadeSeconds);
            if (!_hasPlayedOnce)
            {
                _animator.Play(nextSlot.StateNameHash, _layerIndex, normalizedOffset);
                _hasPlayedOnce = true;
            }
            else
            {
                _animator.CrossFadeInFixedTime(
                    nextSlot.StateNameHash,
                    safeFade,
                    _layerIndex,
                    normalizedOffset);
            }

            _activeIsA = !_activeIsA;
            CurrentClip = clip;
            return true;
        }

        /// <summary>
        ///     Immutable description of a single animator state that participates in the
        ///     ping-pong pair.
        /// </summary>
        public readonly struct SlotDefinition
        {
            /// <summary>Animator state name exactly as authored on the controller.</summary>
            public string StateName { get; }

            /// <summary>Cached <see cref="Animator.StringToHash" /> of <see cref="StateName" />.</summary>
            public int StateNameHash { get; }

            /// <summary>
            ///     Name of the placeholder clip assigned as the state's motion in the
            ///     controller. Used as the override key.
            /// </summary>
            public string PlaceholderName { get; }

            public SlotDefinition(string stateName, string placeholderName)
            {
                StateName = stateName ?? string.Empty;
                PlaceholderName = placeholderName ?? string.Empty;
                StateNameHash = string.IsNullOrEmpty(stateName) ? 0 : Animator.StringToHash(stateName);
            }
        }
    }
}
