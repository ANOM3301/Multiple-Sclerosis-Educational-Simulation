using Convai.Runtime.Animation;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Encapsulates the four-layer registration state with an
    ///     <see cref="AnimatorConductor" /> and centralizes the conductor-vs-direct write
    ///     fallback so callers issue a single <see cref="WriteWeight" /> per layer.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When a conductor is present, the binding registers each driven layer once and
    ///         routes weight writes through the conductor (single-owner serialization). When
    ///         no conductor is present the binding writes directly to the animator after a
    ///         range check. Either way the controller calls <see cref="WriteWeight" /> with no
    ///         conditional logic.
    ///     </para>
    /// </remarks>
    internal sealed class DialogueConductorBinding
    {
        private const int LayerCount = 4;
        private const int IndexBase = 0;
        private const int IndexIdleOverlay = 1;
        private const int IndexBodyTalk = 2;
        private const int IndexHeadTalk = 3;

        private AnimatorConductor _conductor;
        private Animator _animator;
        private DialogueAnimatorLayerSet _layers;
        private readonly bool[] _registered = new bool[LayerCount];
        private bool _bound;

        public Animator Animator => _animator;

        public AnimatorConductor Conductor => _conductor;

        public bool HasConductor => _conductor != null;

        /// <summary>
        ///     Captures the conductor, animator, and layer set used by subsequent
        ///     <see cref="Register" />, <see cref="Unregister" />, and <see cref="WriteWeight" />
        ///     calls. Must be called before any other method.
        /// </summary>
        public void Bind(AnimatorConductor conductor, Animator animator, in DialogueAnimatorLayerSet layers)
        {
            _conductor = conductor;
            _animator = animator;
            _layers = layers;
            _bound = true;
        }

        /// <summary>
        ///     Registers all four driven layers with the bound conductor (no-op when no
        ///     conductor was bound). Idempotent — already-registered layers are skipped.
        /// </summary>
        public void Register(Object owner)
        {
            if (!_bound || _conductor == null) return;

            TryRegister(owner, IndexBase, _layers.BaseIdle);
            TryRegister(owner, IndexIdleOverlay, _layers.IdleOverlay);
            TryRegister(owner, IndexHeadTalk, _layers.HeadTalk);
            TryRegister(owner, IndexBodyTalk, _layers.BodyTalk);
        }

        /// <summary>
        ///     Releases all registered layers from the bound conductor. Idempotent.
        /// </summary>
        public void Unregister(Object owner)
        {
            if (_conductor == null)
            {
                ResetRegistrationFlags();
                return;
            }

            if (_registered[IndexBase])
                _conductor.UnregisterLayer(owner, _layers.BaseIdle);
            if (_registered[IndexIdleOverlay])
                _conductor.UnregisterLayer(owner, _layers.IdleOverlay);
            if (_registered[IndexHeadTalk])
                _conductor.UnregisterLayer(owner, _layers.HeadTalk);
            if (_registered[IndexBodyTalk])
                _conductor.UnregisterLayer(owner, _layers.BodyTalk);

            ResetRegistrationFlags();
        }

        /// <summary>
        ///     Writes <paramref name="weight" /> to <paramref name="layerIndex" /> through the
        ///     conductor when registered, otherwise directly to the animator if the index is
        ///     in range.
        /// </summary>
        public void WriteWeight(Object owner, int layerIndex, float weight)
        {
            int slot = LookupSlot(layerIndex);
            if (slot >= 0 && _conductor != null && _registered[slot])
            {
                _conductor.WriteLayerWeight(owner, layerIndex, weight);
                return;
            }

            if (_animator == null) return;
            if (layerIndex < 0 || layerIndex >= _animator.layerCount) return;

            _animator.SetLayerWeight(layerIndex, weight);
        }

        public void Reset()
        {
            ResetRegistrationFlags();
            _conductor = null;
            _animator = null;
            _layers = default;
            _bound = false;
        }

        private void TryRegister(Object owner, int slot, int layerIndex)
        {
            if (_registered[slot]) return;
            _registered[slot] = _conductor.RegisterLayer(owner, layerIndex);
        }

        private void ResetRegistrationFlags()
        {
            for (int i = 0; i < LayerCount; i++)
                _registered[i] = false;
        }

        private int LookupSlot(int layerIndex)
        {
            if (layerIndex == _layers.BaseIdle) return IndexBase;
            if (layerIndex == _layers.IdleOverlay) return IndexIdleOverlay;
            if (layerIndex == _layers.BodyTalk) return IndexBodyTalk;
            if (layerIndex == _layers.HeadTalk) return IndexHeadTalk;
            return -1;
        }
    }
}
