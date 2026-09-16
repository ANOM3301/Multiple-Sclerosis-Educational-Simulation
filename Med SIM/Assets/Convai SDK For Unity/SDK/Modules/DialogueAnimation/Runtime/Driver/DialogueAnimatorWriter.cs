using Convai.Modules.DialogueAnimation.Core;
using UnityEngine;

namespace Convai.Modules.DialogueAnimation.Runtime.Driver
{
    /// <summary>
    ///     Single sink that translates <see cref="DialogueAnimationCommands" /> into animator
    ///     layer-weight writes. Owns the talk- and idle-layer blenders plus the conductor
    ///     registration so the controller never touches them directly.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The writer is a thin coordinator over
    ///         <see cref="DialogueTalkLayerWeightDriver" /> and
    ///         <see cref="DialogueIdleLayerWeightDriver" />. It carries the
    ///         <see cref="DialogueConductorBinding" /> instance because every weight write
    ///         flows through it; that keeps registration and writes co-located.
    ///     </para>
    /// </remarks>
    internal sealed class DialogueAnimatorWriter
    {
        private readonly DialogueTalkLayerWeightDriver _talkLayerDriver = new();
        private readonly DialogueIdleLayerWeightDriver _idleLayerDriver = new();
        private readonly DialogueConductorBinding _conductorBinding = new();

        private DialogueAnimatorLayerSet _layers;
        private bool _bound;

        public float CurrentHeadTalkLayerWeight => _talkLayerDriver.CurrentHeadWeight;
        public float CurrentBodyTalkLayerWeight => _talkLayerDriver.CurrentBodyWeight;
        public float CurrentBaseIdleLayerWeight => _idleLayerDriver.CurrentBaseWeight;
        public float CurrentIdleOverlayLayerWeight => _idleLayerDriver.CurrentIdleOverlayWeight;

        /// <summary>Conductor-aware writer for layer weights (exposed for diagnostics).</summary>
        public DialogueConductorBinding ConductorBinding => _conductorBinding;

        /// <summary>
        ///     Initializes the talk + idle blenders, captures the layer set, and registers all
        ///     four driven layers with the conductor.
        /// </summary>
        public void Bind(
            Animator animator,
            Convai.Runtime.Animation.AnimatorConductor conductor,
            in DialogueAnimatorLayerSet layers,
            Object owner,
            bool initialSpeaking,
            DialogueAnimationRuntimeConfig config,
            bool hasValidIdleLibrary)
        {
            _layers = layers;
            _talkLayerDriver.Initialize();
            _idleLayerDriver.Initialize(initialSpeaking, config, hasValidIdleLibrary);
            _conductorBinding.Bind(conductor, animator, in layers);
            _conductorBinding.Register(owner);
            _bound = true;
        }

        /// <summary>
        ///     Re-registers driven layers when the conductor becomes available after the
        ///     initial bind (e.g. lazily-spawned through <c>EnsureAnimatorConductor</c>).
        /// </summary>
        public void RebindConductor(
            Animator animator,
            Convai.Runtime.Animation.AnimatorConductor conductor,
            Object owner)
        {
            if (conductor == null || !_bound) return;
            _conductorBinding.Bind(conductor, animator, in _layers);
            _conductorBinding.Register(owner);
        }

        /// <summary>Writes 0 weight to both talk layers (used immediately after bind).</summary>
        public void WriteZeroedTalkWeights(Object owner)
        {
            if (!_bound) return;
            _talkLayerDriver.WriteZeroedWeights(_conductorBinding, owner, _layers.HeadTalk, _layers.BodyTalk);
        }

        /// <summary>
        ///     Applies the per-frame command payload: updates talk targets, advances both
        ///     blenders, and writes idle weights. Caller passes the same <paramref name="owner" />
        ///     used at <see cref="Bind" /> time so conductor ownership stays consistent.
        /// </summary>
        public void Apply(
            in DialogueAnimationCommands commands,
            float deltaTime,
            Object owner,
            DialogueAnimationRuntimeConfig config)
        {
            if (!_bound) return;

            _talkLayerDriver.UpdateTargets(in commands.Talk, commands.Speaking, commands.SpeechLayerScale, config);
            _talkLayerDriver.FlushAndWrite(deltaTime, _conductorBinding, owner, _layers.HeadTalk, _layers.BodyTalk);
            _idleLayerDriver.Tick(
                deltaTime,
                commands.Speaking,
                config,
                _conductorBinding,
                owner,
                _layers.BaseIdle,
                _layers.IdleOverlay,
                commands.HasValidIdleLibrary);
        }

        /// <summary>Releases conductor registration and clears blender state.</summary>
        public void Unbind(Object owner)
        {
            _conductorBinding.Unregister(owner);
            _conductorBinding.Reset();
            _talkLayerDriver.Reset();
            _idleLayerDriver.Reset();
            _layers = default;
            _bound = false;
        }
    }
}
