using System.Collections.Generic;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime
{
    /// <summary>
    ///     Thin wrapper around <see cref="AnimatorOverrideController" /> that caches the
    ///     override list once and exposes index-based clip swaps keyed by placeholder
    ///     clip name.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="AnimatorOverrideController.ApplyOverrides" /> takes an ordered
    ///         list of <c>KeyValuePair</c> entries. Rebuilding that list and scanning it
    ///         by name every swap is wasteful in the hot path, so this class pre-indexes
    ///         the placeholder names into the list positions once and lets callers swap
    ///         clips in O(1).
    ///     </para>
    /// </remarks>
    public sealed class AnimatorSlotOverrider
    {
        private readonly AnimatorOverrideController _overrideController;
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _overrides;
        private readonly Dictionary<string, int> _placeholderNameToIndex;

        private bool _dirty;

        public AnimatorOverrideController OverrideController => _overrideController;

        public AnimatorSlotOverrider(AnimatorOverrideController overrideController)
        {
            _overrideController = overrideController;
            _overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            _placeholderNameToIndex = new Dictionary<string, int>(overrideController.overridesCount);

            _overrideController.GetOverrides(_overrides);
            for (int i = 0; i < _overrides.Count; i++)
            {
                AnimationClip key = _overrides[i].Key;
                if (key == null) continue;
                _placeholderNameToIndex[key.name] = i;
            }
        }

        /// <summary>
        ///     <c>true</c> when the override controller contains a placeholder whose name
        ///     equals <paramref name="placeholderName" />.
        /// </summary>
        public bool HasSlot(string placeholderName)
        {
            return !string.IsNullOrEmpty(placeholderName)
                   && _placeholderNameToIndex.ContainsKey(placeholderName);
        }

        /// <summary>
        ///     Sets the override clip for the slot keyed by <paramref name="placeholderName" />.
        ///     Does not flush the override list ; call <see cref="ApplyPending" /> once per
        ///     frame after all slots have been set.
        /// </summary>
        public bool SetOverride(string placeholderName, AnimationClip clip)
        {
            if (string.IsNullOrEmpty(placeholderName)) return false;
            if (!_placeholderNameToIndex.TryGetValue(placeholderName, out int index)) return false;

            KeyValuePair<AnimationClip, AnimationClip> existing = _overrides[index];
            if (ReferenceEquals(existing.Value, clip)) return true;

            _overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(existing.Key, clip);
            _dirty = true;
            return true;
        }

        /// <summary>Pushes pending override changes to the animator.</summary>
        public void ApplyPending()
        {
            if (!_dirty) return;
            _overrideController.ApplyOverrides(_overrides);
            _dirty = false;
        }
    }
}
